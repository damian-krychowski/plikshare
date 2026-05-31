namespace PlikShare.MediaProcessing.Generation;

/// <summary>
/// One generated thumbnail variant — its bytes, exposed as a <see cref="Stream"/>, plus the size.
/// Ownership transfers to the caller; <c>await using</c> releases the underlying buffer.
/// </summary>
public interface IThumbnail : IDisposable, IAsyncDisposable
{
    long SizeInBytes { get; }
    Stream Content { get; }
}
