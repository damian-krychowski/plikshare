using System.IO.Hashing;

namespace PlikShare.Core.Utils;

/// <summary>
/// A read-only, forward-only <see cref="Stream"/> wrapper that transparently computes
/// an XxHash128 hash of all bytes read from the underlying <paramref name="inner"/> stream.
/// </summary>
/// <remarks>
/// Every successful read is fed into an incremental <see cref="XxHash128"/> instance, so by the
/// time the stream is fully consumed the hash covers the exact byte sequence that was read —
/// without buffering the content or making an extra pass over the data. This is intended for
/// "hash while you stream" scenarios, e.g. computing a content ETag for a thumbnail while it is
/// being uploaded to storage.
///
/// After the stream has been read to the end, retrieve the result via <see cref="Hash"/>
/// (e.g. <c>Hash.GetCurrentHash(...)</c>). The hash reflects only the bytes actually read; if the
/// stream is consumed partially, the hash will cover just that prefix.
///
/// Only reading is supported. Seeking, writing, length and position operations all throw
/// <see cref="NotSupportedException"/>. The wrapper does not take ownership of the inner stream
/// and does not dispose it.
/// </remarks>
public sealed class XxHashingReadStream(Stream inner) : Stream
{
    /// <summary>
    /// The incremental hash accumulated from all bytes read so far. Read its final value
    /// (e.g. via <see cref="XxHash128.GetCurrentHash(Span{byte})"/>) once the stream has been
    /// fully consumed.
    /// </summary>
    public XxHash128 Hash { get; } = new();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        
        if (read > 0)
        {
            Hash.Append(buffer.AsSpan(offset, read));
        }

        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        
        if (read > 0)
        {
            Hash.Append(buffer[..read]);
        }

        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, 
        CancellationToken cancellationToken = default)
    {
        var read = await inner.ReadAsync(buffer, cancellationToken);
        if (read > 0) Hash.Append(buffer.Span[..read]);
        return read;
    }

    public override Task<int> ReadAsync(
        byte[] buffer, 
        int offset, 
        int count, 
        CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}