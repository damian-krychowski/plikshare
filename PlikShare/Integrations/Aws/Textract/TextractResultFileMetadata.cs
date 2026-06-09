using PlikShare.Files.Metadata;

namespace PlikShare.Integrations.Aws.Textract;

public class TextractResultFileMetadata: FileMetadata
{
    public const string TypeDiscriminator = "aws-textract-result";

    public required TextractFeature[] Features { get; init; }
}