namespace PlikShare.Workspaces.UpdateTrashPolicy.Contracts;

public class UpdateWorkspaceTrashPolicyDto
{
    public required bool Enabled { get; init; }
    public required int? RetentionDays { get; init; }
}
