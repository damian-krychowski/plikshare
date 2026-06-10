using PlikShare.Files.Metadata;

namespace PlikShare.Workspaces.UpdateThumbnailsPolicy.Contracts;

public class UpdateWorkspaceThumbnailsPolicyDto
{
    public required bool GenerateOnUpload { get; init; }
    public required ThumbnailVariant[] Variants { get; init; }
}
