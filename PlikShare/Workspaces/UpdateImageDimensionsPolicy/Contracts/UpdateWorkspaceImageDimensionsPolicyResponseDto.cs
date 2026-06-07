namespace PlikShare.Workspaces.UpdateImageDimensionsPolicy.Contracts;

// Returned after toggling the policy. When enabling kicks off a backfill of existing images,
// BatchId/TotalFiles describe it so the UI can start tracking progress immediately. Both are
// null/0 when no backfill was started (disabling, or nothing to backfill).
public class UpdateWorkspaceImageDimensionsPolicyResponseDto
{
    public required string? BatchId { get; init; }
    public required int TotalFiles { get; init; }
}
