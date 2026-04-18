using System.IO.Pipelines;

namespace PlikShare.Storages.FileReading;

public interface IStorageFile: IDisposable, IAsyncDisposable
{
    ValueTask ReadTo(
        PipeWriter output,
        CancellationToken cancellationToken);
}