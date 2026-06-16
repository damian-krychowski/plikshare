namespace PlikShare.Mcp.Workspaces.Content.Contracts;

public class ListWorkspaceContentResponseDto
{
    public required List<WorkspaceContentFolderDto> Path { get; init; }
    public required List<WorkspaceContentEntryDto> Entries { get; init; }
    public required string? NextCursor { get; init; }
    public required bool HasMore { get; init; }
}

public class WorkspaceContentFolderDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
}

public class WorkspaceContentEntryDto
{
    public required string Type { get; init; }
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string? Extension { get; init; }
    public required string? ContentType { get; init; }
    public required long? SizeInBytes { get; init; }
    public required DateTime? CreatedAt { get; init; }
}
