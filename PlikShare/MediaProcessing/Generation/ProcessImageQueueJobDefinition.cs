using PlikShare.Core.Encryption;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Users.Id;

namespace PlikShare.MediaProcessing.Generation;

public class ProcessImageQueueJobDefinitionV2
{
    public required int WorkspaceId { get; init; }
    public required BatchItem[] Files { get; init; }
    public required UserExtId TriggeredByUserExternalId { get; init; }

    public class BatchItem
    {
        public required FileExtId ParentFileExternalId { get; init; }
        public required List<VariantItem> VariantItems { get; init; }
        public required bool IsVideo { get; init; }
        public required FullEncryptionSeedEphemeral? EncryptionSeed { get; init; }
    }

    public class VariantItem
    {
        public required ThumbnailVariant Variant { get; init; }
        public required FullEncryptionSeedEphemeral? EncryptionSeed { get; init; }
    }
}

public static class ProcessImageQueueJobDefinitionV2Extensions
{
    extension(ProcessImageQueueJobDefinitionV2.BatchItem item)
    {
        public List<ThumbnailVariant> GetVariants()
        {
            return item
                .VariantItems
                .Select(i => i.Variant)
                .ToList();
        }
    }
}