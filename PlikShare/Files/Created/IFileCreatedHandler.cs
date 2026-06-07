namespace PlikShare.Files.Created;

public interface IFileCreatedHandler
{
    void Handle(FileCreatedBatch batch);
}