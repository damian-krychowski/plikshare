using PlikShare.Core.Encryption;

namespace PlikShare.MediaProcessing.Dimensions;

public class ExtractImageDimensionsQueueJobDefinition
{
    public required int WorkspaceId { get; init; }
    public required int[] FileIds { get; init; }
    public required Dictionary<int, FullEncryptionSeedEphemeral>? EncryptionSeeds { get; init; }
}
