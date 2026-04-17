using Amazon;
using Amazon.S3;
using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive;
using PlikShare.Storages.HardDrive.StorageClient;
using PlikShare.Storages.Id;
using PlikShare.Storages.S3;
using PlikShare.Storages.S3.AwsS3;
using PlikShare.Storages.S3.BackblazeB2;
using PlikShare.Storages.S3.CloudflareR2;
using PlikShare.Storages.S3.DigitalOcean;
using Serilog;

namespace PlikShare.Storages;

public static class StorageStartupExtensions
{
    private sealed record StorageRow(
        int StorageId,
        StorageExtId ExternalId,
        string Name,
        string Type,
        string DetailsJson,
        StorageEncryption Encryption);

    public static void UseStorage(this WebApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        
        app.Services.AddSingleton<StorageClientStore>();

        Log.Information("[SETUP] S3 Storage setup finished.");
    }

    public static void InitializeStorageClientStore(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clientStore = app
            .Services
            .GetRequiredService<StorageClientStore>();

        var plikShareDb = app
            .Services
            .GetRequiredService<PlikShareDb>();

        var masterDataEncryption = app
            .Services
            .GetRequiredService<IMasterDataEncryption>();

        var clock = app
            .Services
            .GetRequiredService<IClock>();

        var config = app
            .Services
            .GetRequiredService<IConfig>();

        var preSignedUrlsService = app
            .Services
            .GetRequiredService<PreSignedUrlsService>();

        using var connection = plikShareDb.OpenConnection();

        var storages = connection
            .Cmd(
                sql: @"
                    SELECT
                        s_id,
                        s_external_id,
                        s_name,
                        s_type,
                        s_details_encrypted,
                        s_encryption_type,
                        s_encryption_details_encrypted
                    FROM s_storages
                ",
                readRowFunc: reader =>
                {
                    var storageId = reader.GetInt32(0);
                    var externalId = reader.GetExtId<StorageExtId>(1);
                    var name = reader.GetString(2);
                    var type = reader.GetString(3);
                    var detailsJson = masterDataEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(4));

                    var encryptionType = StorageEncryptionExtensions.FromDbValue(
                        dbValue: reader.GetStringOrNull(5));

                    var encryptionDetailsJson = encryptionType == StorageEncryptionType.None
                        ? null
                        : masterDataEncryption.Decrypt(reader.GetFieldValue<byte[]>(6));

                    return new StorageRow(
                        StorageId: storageId,
                        ExternalId: externalId,
                        Name: name,
                        Type: type,
                        DetailsJson: detailsJson,

                        Encryption: StorageEncryptionExtensions.GetStorageEncryption(
                            encryptionType: encryptionType,
                            encryptionDetailsJson: encryptionDetailsJson));
                })
            .Execute();

        foreach (var storage in storages)
        {
            var client = storage.Type switch
            {
                StorageType.HardDrive => (IStorageClient)BuildHardDriveStorageClient(
                    storage, clock, preSignedUrlsService),

                StorageType.BackblazeB2 or
                    StorageType.CloudflareR2 or
                    StorageType.AwsS3 or
                    StorageType.DigitalOceanSpaces => BuildS3StorageClient(
                        storage, clock, preSignedUrlsService, config),

                _ => throw new InvalidOperationException(
                    $"Unsupported storage type '{storage.Type}'.")
            };

            clientStore.RegisterClient(client);

        }

        Log.Information("[INITIALIZATION] S3 Client Store initialization finished.");
    }

    private static HardDriveStorageClient BuildHardDriveStorageClient(
        StorageRow storage,
        IClock clock,
        PreSignedUrlsService preSignedUrlsService)
    {
        var hdStorageClient = new HardDriveStorageClient(
            preSignedUrlsService: preSignedUrlsService,
            details: Json.Deserialize<HardDriveDetailsEntity>(
                storage.DetailsJson)!,
            storageId: storage.StorageId,
            externalId: storage.ExternalId,
            name: storage.Name,
            clock: clock,
            encryption: storage.Encryption);

        return hdStorageClient;
    }

    private static S3StorageClient BuildS3StorageClient(
        StorageRow storage, 
        IClock clock,
        PreSignedUrlsService preSignedUrlsService,
        IConfig config)
    {
        var s3Client = BuildS3ClientForStorage(storage);

        var s3StorageClient = new S3StorageClient(
            appUrl: config.AppUrl,
            clock: clock,
            s3Client: s3Client,
            storageId: storage.StorageId,
            externalId: storage.ExternalId,
            name: storage.Name,
            preSignedUrlsService: preSignedUrlsService,
            encryption: storage.Encryption);

        return s3StorageClient;
    }

    private static IAmazonS3 BuildS3ClientForStorage(StorageRow storage)
    {
        if (storage.Type == StorageType.BackblazeB2)
        {
            var details = Json.Deserialize<BackblazeB2DetailsEntity>(storage.DetailsJson)!;

            return S3Client.BuildBackblazeClientOrThrow(
                keyId: details.KeyId,
                applicationKey: details.ApplicationKey,
                url: details.Url);
        }

        if (storage.Type == StorageType.CloudflareR2)
        {
            var details = Json.Deserialize<CloudflareR2DetailsEntity>(storage.DetailsJson)!;

            return S3Client.BuildCloudflareClientOrThrow(
                accessKeyId: details.AccessKeyId,
                secretAccessKey: details.SecretAccessKey,
                url: details.Url);
        }

        if (storage.Type == StorageType.AwsS3)
        {
            var details = Json.Deserialize<AwsS3DetailsEntity>(storage.DetailsJson)!;

            return S3Client.BuildAwsClientOrThrow(
                accessKey: details.AccessKey,
                secretAccessKey: details.SecretAccessKey,
                region: details.Region);
        }

        if (storage.Type == StorageType.DigitalOceanSpaces)
        {
            var details = Json.Deserialize<DigitalOceanSpacesDetailsEntity>(storage.DetailsJson)!;

            return S3Client.BuildDigitalOceanSpacesClientOrThrow(
                accessKey: details.AccessKey,
                secretKey: details.SecretKey,
                url: details.Url);
        }

        throw new InvalidOperationException(
            $"Unsupported storage type '{storage.Type}'.");
    }
}