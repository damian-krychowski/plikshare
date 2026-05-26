using System.Text.Json.Serialization;
using PlikShare.Integrations.Aws.Textract;

namespace PlikShare.Files.Metadata;

[JsonDerivedType(derivedType: typeof(TextractResultFileMetadata), typeDiscriminator: "aws-textract-result")]
[JsonDerivedType(derivedType: typeof(ThumbnailFileMetadata), typeDiscriminator: "thumbnail")]
public abstract class FileMetadata
{
}