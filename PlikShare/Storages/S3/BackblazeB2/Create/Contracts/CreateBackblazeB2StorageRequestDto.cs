using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.S3.BackblazeB2.Create.Contracts;

public class CreateBackblazeB2StorageRequestDto
{
    public required string Name { get; init; }
    public required string KeyId { get; init; }
    public required string ApplicationKey { get; init; }
    public required string Url { get; init; }
    public required StorageEncryptionType EncryptionType { get; init; }
}

public class CreateBackblazeB2StorageResponseDto
{
    public required StorageExtId ExternalId { get; init; }
};