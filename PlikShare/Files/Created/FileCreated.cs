using PlikShare.Core.Encryption;
using PlikShare.Files.Id;

namespace PlikShare.Files.Created;

public sealed record CreatedFile(
    int Id,
    FileExtId ExternalId,
    long SizeInBytes,
    EncodedMetadataValue ContentType,
    string UploaderIdentityType,
    string UploaderIdentity,
    FileEncryptionMetadata? EncryptionMetadata,
    FullEncryptionSeedEphemeral? EncryptionSeed);
