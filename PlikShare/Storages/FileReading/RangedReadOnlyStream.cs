namespace PlikShare.Storages.FileReading;

/// <summary>
/// A read-only stream wrapper that exposes a bounded window [start, start+length)
/// of an underlying seekable stream. Behaves like an HTTP ranged response:
/// once `length` bytes have been read, further reads return 0.
/// </summary>
public sealed class RangedReadOnlyStream : Stream
{
    private readonly Stream _inner;
    private readonly long _length;
    private readonly bool _leaveOpen;
    private long _position; // within the window, 0.._length

    public RangedReadOnlyStream(
        Stream inner,
        long start,
        long length,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(inner);
        if (!inner.CanRead) throw new ArgumentException("Stream must be readable.", nameof(inner));
        if (!inner.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(inner));
        if (start < 0) throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

        _inner = inner;
        _length = length;
        _leaveOpen = leaveOpen;

        inner.Seek(start, SeekOrigin.Begin);
        _position = 0;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var remaining = _length - _position;
        if (remaining <= 0) return 0;

        var toRead = (int)Math.Min(buffer.Length, remaining);
        var read = _inner.Read(buffer[..toRead]);
        _position += read;
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer, 
        int offset, 
        int count, 
        CancellationToken cancellationToken)
    {
        return ReadAsync(
            buffer.AsMemory(offset, count), 
            cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, 
        CancellationToken cancellationToken = default)
    {
        var remaining = _length - _position;
        if (remaining <= 0) return 0;

        var toRead = (int) Math.Min(
            buffer.Length, 
            remaining);

        var read = await _inner.ReadAsync(
            buffer[..toRead], 
            cancellationToken);

        _position += read;

        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_leaveOpen) _inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_leaveOpen) await _inner.DisposeAsync();

        await base.DisposeAsync();
    }
}