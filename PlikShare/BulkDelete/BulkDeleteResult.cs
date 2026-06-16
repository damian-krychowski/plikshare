namespace PlikShare.BulkDelete;

public class BulkDeleteResult
{
    public required long? NewWorkspaceSizeInBytes { get; init; }
    public required int DeletedFileCount { get; init; }
    public required long DeletedSizeInBytes { get; init; }
}
