namespace PlikShare.Workspaces.UpdateMaxSize.Contracts;

public class UpdateWorkspaceMaxSizeDto
{
    public required long? MaxSizeInBytes { get; init; }
}