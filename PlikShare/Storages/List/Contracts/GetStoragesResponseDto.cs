using System.Text.Json.Serialization;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;

namespace PlikShare.Storages.List.Contracts;

public class GetStoragesResponseDto
{
    public required List<GetStorageItemResponseDto> Items { get; init; }
};

[JsonDerivedType(derivedType: typeof(GetHardDriveStorageItemResponseDto), typeDiscriminator: "hard-drive")]
[JsonDerivedType(derivedType: typeof(GetCloudflareR2StorageItemResponseDto), typeDiscriminator: "cloudflare-r2")]
[JsonDerivedType(derivedType: typeof(GetDigitalOceanSpacesItemResponseDto), typeDiscriminator: "digitalocean-spaces")]
[JsonDerivedType(derivedType: typeof(GetAwsS3StorageItemResponseDto), typeDiscriminator: "aws-s3")]
public abstract class GetStorageItemResponseDto
{
    public required string Name { get; init; }
    public required StorageExtId ExternalId { get; init; }
    public required int WorkspacesCount { get; init; }
    public required StorageEncryptionType EncryptionType { get; init; }
}

public class GetHardDriveStorageItemResponseDto : GetStorageItemResponseDto
{
    public required string VolumePath { get; init; }
    public required string FolderPath { get; init; }
    public required string FullPath { get; init; }
}

public class GetCloudflareR2StorageItemResponseDto : GetStorageItemResponseDto
{
    public required string AccessKeyId { get; init; }
    public required string Url { get;init; }
}

public class GetDigitalOceanSpacesItemResponseDto : GetStorageItemResponseDto
{
    public required string AccessKey { get; init; }
    public required string Url { get; init; }
}

public class GetAwsS3StorageItemResponseDto : GetStorageItemResponseDto
{
    public required string AccessKey { get; init; }
    public required string Region { get; init; }
}
