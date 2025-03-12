using System.Text.Json.Serialization;
using PlikShare.Files.Id;

namespace PlikShare.ArtificialIntelligence.AiIncludes;

[JsonDerivedType(derivedType: typeof(AiFileInclude), typeDiscriminator: "file")]
[JsonDerivedType(derivedType: typeof(AiNotesInclude), typeDiscriminator: "notes")]
[JsonDerivedType(derivedType: typeof(AiCommentsInclude), typeDiscriminator: "comments")]
public abstract record AiInclude;

public record AiFileInclude(FileExtId ExternalId) : AiInclude;
public record AiNotesInclude(FileExtId ExternalId) : AiInclude;
public record AiCommentsInclude(FileExtId ExternalId) : AiInclude;