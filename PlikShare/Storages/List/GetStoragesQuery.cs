using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;
using PlikShare.Storages.S3.AwsS3;
using PlikShare.Storages.S3.CloudflareR2;
using PlikShare.Storages.S3.DigitalOcean;

namespace PlikShare.Storages.List;

public class GetStoragesQuery(
    IMasterDataEncryption masterDataEncryption,
    PlikShareDb plikShareDb)
{
    public List<GetStorageItemResponseDto> Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd<GetStorageItemResponseDto>(
                sql: @"
                    SELECT 
                        s_external_id,
                        s_name,
                        s_type,
                        s_details_encrypted,
                        (SELECT COUNT(*) FROM w_workspaces WHERE w_storage_id = s_id) AS s_workspaces_count,
                        s_encryption_type
                    FROM s_storages
                    ORDER BY s_id ASC
                ",
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<StorageExtId>(0);
                    var name = reader.GetString(1);
                    var type = reader.GetString(2);
                    var encryptedDetails = reader.GetFieldValue<byte[]>(3);
                    var workspacesCount = reader.GetInt32(4);
                    var encryptionType = StorageEncryptionExtensions.FromDbValue(
                        dbValue: reader.GetStringOrNull(5));

                    if (type == StorageType.HardDrive)
                    {
                        var details = GetStorageDetails<HardDriveDetailsEntity>(
                            encryptedDetails);

                        return new GetHardDriveStorageItemResponseDto
                        {
                            ExternalId = externalId,
                            EncryptionType = encryptionType,
                            Name = name,
                            WorkspacesCount = workspacesCount,

                            FolderPath = details!.FolderPath,
                            FullPath = details.FullPath,
                            VolumePath = details.VolumePath
                        };
                    }

                    if (type == StorageType.AwsS3)
                    {
                        var details = GetStorageDetails<AwsS3DetailsEntity>(
                            encryptedDetails);

                        return new GetAwsS3StorageItemResponseDto
                        {
                            ExternalId = externalId,
                            EncryptionType = encryptionType,
                            Name = name,
                            WorkspacesCount = workspacesCount,

                            AccessKey = Obfuscate(details!.AccessKey),
                            Region = details.Region
                        };
                    }

                    if (type == StorageType.CloudflareR2)
                    {
                        var details = GetStorageDetails<CloudflareR2DetailsEntity>(
                            encryptedDetails);

                        return new GetCloudflareR2StorageItemResponseDto()
                        {
                            ExternalId = externalId,
                            EncryptionType = encryptionType,
                            Name = name,
                            WorkspacesCount = workspacesCount,

                            AccessKeyId = Obfuscate(details!.AccessKeyId),
                            Url = details.Url
                        };
                    }
                    
                    if (type == StorageType.DigitalOceanSpaces)
                    {
                        var details = GetStorageDetails<DigitalOceanSpacesDetailsEntity>(
                            encryptedDetails);

                        return new GetDigitalOceanSpacesItemResponseDto()
                        {
                            ExternalId = externalId,
                            EncryptionType = encryptionType,
                            Name = name,
                            WorkspacesCount = workspacesCount,

                            Url = details!.Url,
                            AccessKey = Obfuscate(details.AccessKey)
                        };
                    }

                    throw new ArgumentOutOfRangeException(
                        paramName: nameof(type),
                        message: $"Unknown storage type: '{type}'");
                })
            .Execute();
    }

    public static string Obfuscate(string accessKey)
    {
        if (string.IsNullOrEmpty(accessKey) || accessKey.Length < 8)
        {
            return accessKey;
        }

        var charsToShowAtEnds = 4;
        var charsToObfuscate = accessKey.Length - (charsToShowAtEnds * 2);

        if (charsToObfuscate < 1)
        {
            charsToShowAtEnds = accessKey.Length / 4;
            charsToObfuscate = accessKey.Length - (charsToShowAtEnds * 2);
        }

        var beginning = accessKey.Substring(0, charsToShowAtEnds);
        var asterisks = new string('*', charsToObfuscate);
        var ending = accessKey.Substring(accessKey.Length - charsToShowAtEnds);

        return beginning + asterisks + ending;
    }

    private TDetails? GetStorageDetails<TDetails>(byte[] encryptedDetails)
    {
        var decryptedDetails = masterDataEncryption.Decrypt(
            encryptedDetails);

        return Json.Deserialize<TDetails>(
            decryptedDetails);
    }
}