using PlikShare.Files.Metadata;

namespace PlikShare.Integrations.Aws.Textract;

public class TextractResultFileMetadata: FileMetadata
{
    public required TextractFeature[] Features { get; init; }
}