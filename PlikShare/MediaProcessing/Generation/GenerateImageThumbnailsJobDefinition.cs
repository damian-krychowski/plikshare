using PlikShare.Core.Encryption;
using PlikShare.Files.Metadata;

namespace PlikShare.MediaProcessing.Generation;

public class GenerateImageThumbnailsJobDefinition
{
    public required int WorkspaceId { get; init; }
    public required List<int> ImageFileIds { get; init; }
    public required List<int> VideoFileIds { get; init; }
    public required ThumbnailVariant[] Variants { get; init; }
    public required string UploaderIdentityType { get; init; }
    public required string UploaderIdentity { get; init; }

    public required Dictionary<string, FullEncryptionSeedEphemeral>? EncryptionSeeds { get; init; }

}

public static class GenerateImageThumbnailsJobDefinitionExtensions
{
    extension(GenerateImageThumbnailsJobDefinition)
    {
        public static string GetFileEncryptionSeedsKey(int fileId)
        {
            return fileId.ToString();
        }

        public static string GetVariantEncryptionSeedsKey(int fileId, ThumbnailVariant variant)
        {
            return $"{fileId}:{(int)variant}";
        }
    }

    extension(GenerateImageThumbnailsJobDefinition definition)
    {
        public int FilesCount => definition.ImageFileIds.Count + definition.VideoFileIds.Count;
    }
}