using System.Text.Json.Serialization;
using PlikShare.Integrations.Aws.Textract;

namespace PlikShare.Files.Metadata;

[JsonDerivedType(derivedType: typeof(TextractResultFileMetadata), typeDiscriminator: TextractResultFileMetadata.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ThumbnailFileMetadata), typeDiscriminator: ThumbnailFileMetadata.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ImageDimensionsFileMetadata), typeDiscriminator: ImageDimensionsFileMetadata.TypeDiscriminator)]
public abstract class FileMetadata
{
}