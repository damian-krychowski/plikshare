namespace PlikShare.Files.Created;

public class FileCreatedDispatcher(IEnumerable<IFileCreatedHandler> handlers)
{
    public void OnFilesCreated(FileCreatedBatch batch)
    {
        if (batch.Files.Count == 0)
            return;

        foreach (var handler in handlers)
            handler.Handle(batch);
    }
}