using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.Workspaces.ListAll.Contracts;

public class GetAllWorkspacesResponseDto
{
    public required List<GetAllWorkspacesItemDto> Items { get; init; }
}

public class GetAllWorkspacesItemDto
{
    public required WorkspaceExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required long CurrentSizeInBytes { get; init; }
    public required long? MaxSizeInBytes { get; init; }
    public required bool IsBucketCreated { get; init; }
    public required GetAllWorkspacesStorageDto Storage { get; init; }
    public required GetAllWorkspacesOwnerDto Owner { get; init; }
}

public class GetAllWorkspacesStorageDto
{
    public required StorageExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string EncryptionType { get; init; }
}

public class GetAllWorkspacesOwnerDto
{
    public required UserExtId ExternalId { get; init; }
    public required string Email { get; init; }
}
