namespace PlikShare.Workspaces.UpdateThumbnailsPolicy.Contracts;

// Returned after updating the policy. When enabling (or widening the variant set) kicks off a
// backfill of existing images, BatchId/TotalFiles describe it so the UI can start tracking
// progress immediately. Both are null/0 when no backfill was started (disabling, or nothing
// missing the selected variants).
public class UpdateWorkspaceThumbnailsPolicyResponseDto
{
    public required string? BatchId { get; init; }
    public required int TotalFiles { get; init; }
}
