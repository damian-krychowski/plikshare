using System.Text.Json.Serialization;
using PlikShare.Integrations.Aws.Textract;

namespace PlikShare.Files.Metadata;

[JsonDerivedType(derivedType: typeof(TextractResultFileMetadata), typeDiscriminator: "aws-textract-result")]
public abstract class FileMetadata
{
}