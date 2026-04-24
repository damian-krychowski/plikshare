using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Serilog;

namespace PlikShare.Core.Encryption;

/// <summary>
/// A disposable wrapper around a sensitive byte buffer with defense-in-depth memory protections:
/// 
/// 1. PINNED via <see cref="GCHandle"/> — the GC cannot move or copy the buffer during compaction,
///    so there are no zombie plaintext copies left at old addresses.
/// 2. LOCKED via mlock (Linux) / VirtualLock (Windows) — the OS kernel guarantees these pages
///    never get written to swap / pagefile. This survives any subsequent memory pressure.
/// 3. EXCLUDED FROM CORE DUMPS via madvise(MADV_DONTDUMP) on Linux — if the process crashes,
///    the resulting core dump will not contain these pages.
/// 4. ZEROED ON DISPOSE via <see cref="CryptographicOperations.ZeroMemory"/> — deterministic
///    wipe, guaranteed not optimized away by the compiler.
/// 
/// Use with <c>using</c>. Prefer <see cref="CopyTo"/> into a stackalloc'd span at the crypto
/// call site to minimize how long the plaintext lives even in locked memory.
/// </summary>
public sealed class SecureBytes : IDisposable
{
    private byte[]? _buffer;
    private GCHandle _handle;
    private bool _mlocked;
    private bool _dontDumpSet;
    private readonly int _length;

    private SecureBytes(int length)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        _length = length;

        // Pinned allocation: the GC will never move this buffer. Allocating as pinned
        // from the start (instead of allocating then pinning) avoids a window where the
        // buffer could be moved before the GCHandle is created.
        _buffer = GC.AllocateUninitializedArray<byte>(length, pinned: true);
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

        var addr = _handle.AddrOfPinnedObject();
        var len = (nuint)length;

        // Lock the pages so the kernel cannot swap them to disk.
        _mlocked = TryLockMemory(addr, len);

        // On Linux, also exclude from core dumps.
        if (_mlocked && OperatingSystem.IsLinux())
        {
            _dontDumpSet = TryExcludeFromCoreDump(addr, len);
        }
    }
    public SecureBytes Clone()
    {
        ThrowIfDisposed();
        return CopyFrom(_buffer);
    }

    public static SecureBytes Create<TState>(
        int length,
        TState state,
        Action<Span<byte>, TState> initializer)
        where TState : allows ref struct
    {
        var secure = new SecureBytes(length);
        try
        {
            initializer(secure._buffer!, state);
            return secure;
        }
        catch
        {
            secure.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Allocates a secure buffer and copies from the source span.
    /// Caller is responsible for zeroing the source afterward if needed.
    /// </summary>
    public static SecureBytes CopyFrom(ReadOnlySpan<byte> source)
    {
        var secure = new SecureBytes(source.Length);
        source.CopyTo(secure._buffer!);
        return secure;
    }

    public int Length
    {
        get
        {
            ThrowIfDisposed();
            return _length;
        }
    }

    public TResult Use<TResult>(Func<ReadOnlySpan<byte>, TResult> action)
    {
        ThrowIfDisposed();
        return action(_buffer);
    }

    public void Use<TState>(in TState state, Action<ReadOnlySpan<byte>, TState> action)
        where TState : allows ref struct
    {
        ThrowIfDisposed();
        action(_buffer, state);
    }

    public TResult Use<TState, TResult>(in TState state, Func<ReadOnlySpan<byte>, TState, TResult> action)
        where TState : allows ref struct
    {
        ThrowIfDisposed();
        return action(_buffer, state);
    }

    public void Use(Action<ReadOnlySpan<byte>> action)
    {
        ThrowIfDisposed();
        action(_buffer);
    }

    public static TResult UseBoth<TResult>(
        SecureBytes first,
        SecureBytes second,
        Func<ReadOnlySpan<byte>, ReadOnlySpan<byte>, TResult> action)
    {
        first.ThrowIfDisposed();
        second.ThrowIfDisposed();
        return action(first._buffer, second._buffer);
    }

    /// <summary>
    /// Copies the secret into the caller-provided destination span.
    /// Preferred usage: destination is a stackalloc'd buffer at the crypto call site,
    /// zeroed in a finally block immediately after use.
    /// </summary>
    public void CopyTo(Span<byte> destination)
    {
        ThrowIfDisposed();
        _buffer.AsSpan().CopyTo(destination);
    }

    /// <summary>
    /// Write directly into the secure buffer. Used when the source of the secret is
    /// itself a Span (e.g. an HKDF output).
    /// </summary>
    public void WriteFrom(ReadOnlySpan<byte> source)
    {
        ThrowIfDisposed();
        if (source.Length != _length)
            throw new ArgumentException(
                $"Expected source of length {_length}, got {source.Length}.",
                nameof(source));
        source.CopyTo(_buffer);
    }

    public void Dispose()
    {
        if (_buffer is null) return;

        // Always zero before unlocking or releasing the pin. This is the critical step
        // that runs regardless of whether mlock succeeded.
        CryptographicOperations.ZeroMemory(_buffer);

        var addr = _handle.AddrOfPinnedObject();
        var len = (nuint)_length;

        if (_dontDumpSet)
            TryIncludeInCoreDump(addr, len);

        if (_mlocked)
            TryUnlockMemory(addr, len);

        _handle.Free();
        _buffer = null;
    }

    private void ThrowIfDisposed()
    {
        if (_buffer is null)
            throw new ObjectDisposedException(nameof(SecureBytes));
    }

    /// <summary>Never expose the buffer in diagnostics.</summary>
    public override string ToString() => "[REDACTED]";

    // -----------------------------------------------------------------------
    // Platform interop
    // -----------------------------------------------------------------------

    private static bool TryLockMemory(IntPtr addr, nuint len)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                var rc = Posix.mlock(addr, len);
                if (rc == 0) return true;

                var errno = Marshal.GetLastPInvokeError();
                LogMlockFailure(errno);
                return false;
            }

            if (OperatingSystem.IsWindows())
            {
                if (Windows.VirtualLock(addr, len)) return true;

                var err = Marshal.GetLastPInvokeError();
                Log.Warning(
                    "VirtualLock failed (error {Error}). Sensitive pages may be swapped to pagefile.",
                    err);
                return false;
            }

            Log.Warning("Memory locking not implemented for this platform. " +
                        "Sensitive pages may be swapped to disk.");
            return false;
        }
        catch (DllNotFoundException ex)
        {
            Log.Warning(ex, "Memory locking syscall unavailable. Continuing without mlock.");
            return false;
        }
    }

    private static void TryUnlockMemory(IntPtr addr, nuint len)
    {
        try
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                _ = Posix.munlock(addr, len);
            else if (OperatingSystem.IsWindows())
                _ = Windows.VirtualUnlock(addr, len);
        }
        catch
        {
            // Best-effort; the buffer is already zeroed.
        }
    }

    private static bool TryExcludeFromCoreDump(IntPtr addr, nuint len)
    {
        try
        {
            var rc = Posix.madvise(addr, len, Posix.MADV_DONTDUMP);
            return rc == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryIncludeInCoreDump(IntPtr addr, nuint len)
    {
        try
        {
            _ = Posix.madvise(addr, len, Posix.MADV_DODUMP);
        }
        catch
        {
            // Best-effort; process is probably shutting down anyway.
        }
    }

    private static void LogMlockFailure(int errno)
    {
        // ENOMEM (12) is the common case: RLIMIT_MEMLOCK exceeded. On Linux, the default
        // per-process limit is 64 KiB unless the process has CAP_IPC_LOCK or the limit is
        // raised via /etc/security/limits.conf or systemd's LimitMEMLOCK.
        const int ENOMEM = 12;
        const int EPERM = 1;

        if (errno == ENOMEM)
        {
            Log.Warning(
                "mlock failed with ENOMEM. RLIMIT_MEMLOCK is likely too low. " +
                "Sensitive pages may be swapped to disk. " +
                "Raise the limit via systemd (LimitMEMLOCK=infinity) or limits.conf, " +
                "or grant the process CAP_IPC_LOCK.");
        }
        else if (errno == EPERM)
        {
            Log.Warning(
                "mlock failed with EPERM. The process lacks permission to lock memory. " +
                "Sensitive pages may be swapped to disk.");
        }
        else
        {
            Log.Warning("mlock failed with errno {Errno}. Sensitive pages may be swapped.", errno);
        }
    }

    private static class Posix
    {
        // madvise advice codes are Linux-specific for DONTDUMP. macOS does not support
        // MADV_DONTDUMP; calls there will simply fail and we continue.
        public const int MADV_DONTDUMP = 16;
        public const int MADV_DODUMP = 17;

        [DllImport("libc", SetLastError = true)]
        public static extern int mlock(IntPtr addr, nuint len);

        [DllImport("libc", SetLastError = true)]
        public static extern int munlock(IntPtr addr, nuint len);

        [DllImport("libc", SetLastError = true)]
        public static extern int madvise(IntPtr addr, nuint len, int advice);
    }

    private static class Windows
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualLock(IntPtr lpAddress, nuint dwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool VirtualUnlock(IntPtr lpAddress, nuint dwSize);
    }
}