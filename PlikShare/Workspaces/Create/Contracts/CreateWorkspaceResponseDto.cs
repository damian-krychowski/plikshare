using PlikShare.Workspaces.Id;

namespace PlikShare.Workspaces.Create.Contracts;

public class CreateWorkspaceResponseDto
{
    public required WorkspaceExtId ExternalId { get; init; }
    public required long? MaxSizeInBytes { get; init; }
}