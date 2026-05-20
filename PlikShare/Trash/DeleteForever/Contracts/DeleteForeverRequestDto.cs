using PlikShare.Files.Id;

namespace PlikShare.Trash.DeleteForever.Contracts;

public class DeleteForeverRequestDto
{
    public required List<FileExtId> FileExternalIds { get; init; }
}

public class DeleteForeverResponseDto
{
    public required int DeletedCount { get; init; }
    public required long NewWorkspaceSizeInBytes { get; init; }
}
