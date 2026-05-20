using Amazon.S3;
using Amazon.S3.Model;
using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Storages.AzureBlob;
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
using PlikShare.Storages.S3.GoogleCloudStorage;
using PlikShare.Trash;
using Serilog;

namespace PlikShare.Storages;

public static class StorageStartupExtensions
{

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
        
        var config = app
            .Services
            .GetRequiredService<IConfig>();
        
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
                        s_encryption_details_encrypted,
                        s_default_trash_policy_json
                    FROM s_storages
                ",
                readRowFunc: reader =>
                {
                    var storageId = reader.GetInt32(0);
                    var externalId = reader.GetExtId<StorageExtId>(1);
                    var name = reader.GetString(2);
                    var type = reader.GetEnum<StorageType>(3);
                    var detailsJson = masterDataEncryption.DecryptString(
                        reader.GetFieldValue<byte[]>(4));

                    var encryptionType = StorageEncryptionExtensions.FromDbValue(
                        dbValue: reader.GetStringOrNull(5));

                    var encryptionDetailsJson = encryptionType == StorageEncryptionType.None
                        ? null
                        : masterDataEncryption.DecryptString(reader.GetFieldValue<byte[]>(6));

                    var defaultTrashPolicy = reader.GetFromJson<TrashPolicy>(7);

                    return new StorageRow(
                        StorageId: storageId,
                        ExternalId: externalId,
                        Name: name,
                        Type: type,
                        DetailsJson: detailsJson,

                        Encryption: StorageEncryptionExtensions.GetStorageEncryption(
                            encryptionType: encryptionType,
                            encryptionDetailsJson: encryptionDetailsJson),
                        
                        DefaultTrashPolicy: defaultTrashPolicy);
                })
            .Execute();

        foreach (var storage in storages)
        {
            var client = storage.Type switch
            {
                StorageType.HardDrive => BuildHardDriveStorageClient(
                    storage),

                StorageType.BackblazeB2 or
                    StorageType.CloudflareR2 or
                    StorageType.AwsS3 or
                    StorageType.DigitalOceanSpaces or
                    StorageType.GoogleCloudStorage => BuildS3StorageClient(
                        storage,
                        config),

                StorageType.AzureBlob => BuildAzureBlobStorageClient(
                    storage, 
                    config),

                _ => throw new InvalidOperationException(
                    $"Unsupported storage type '{storage.Type}'.")
            };

            clientStore.RegisterClient(client);

        }

        Log.Information("[INITIALIZATION] S3 Client Store initialization finished.");
    }

    private static IStorageClient BuildHardDriveStorageClient(
        StorageRow storage)
    {
        var hdStorageClient = new HardDriveStorageClient(
            details: Json.Deserialize<HardDriveDetailsEntity>(
                storage.DetailsJson)!,
            storageId: storage.StorageId,
            externalId: storage.ExternalId,
            name: storage.Name,
            encryption: storage.Encryption,
            defaultTrashPolicy: storage.DefaultTrashPolicy);

        return hdStorageClient;
    }

    private static IStorageClient BuildAzureBlobStorageClient(
        StorageRow storage,
        IConfig config)
    {
        var details = Json.Deserialize<AzureBlobDetailsEntity>(storage.DetailsJson)!;

        var blobServiceClient = AzureBlobClient.BuildClientOrThrow(
            details: details);

        return new AzureBlobStorageClient(
            appUrl: config.AppUrl,
            blobServiceClient: blobServiceClient,
            storageId: storage.StorageId,
            externalId: storage.ExternalId,
            name: storage.Name,
            encryption: storage.Encryption,
            defaultTrashPolicy: storage.DefaultTrashPolicy);
    }

    private static IStorageClient BuildS3StorageClient(
        StorageRow storage,
        IConfig config)
    {
        var (s3Client, lifecycleRules, customCorsConfigurator) = BuildS3ClientForStorage(
            storage,
            config);

        var s3StorageClient = new S3StorageClient(
            appUrl: config.AppUrl,
            s3Client: s3Client,
            storageId: storage.StorageId,
            externalId: storage.ExternalId,
            name: storage.Name,
            encryption: storage.Encryption,
            defaultTrashPolicy: storage.DefaultTrashPolicy,
            lifecycleRules: lifecycleRules,
            customCorsConfigurator: customCorsConfigurator);

        return s3StorageClient;
    }

    private static (IAmazonS3, LifecycleRule[], Func<string, CancellationToken, Task>?) BuildS3ClientForStorage(
        StorageRow storage,
        IConfig config)
    {
        if (storage.Type == StorageType.BackblazeB2)
        {
            var details = Json.Deserialize<BackblazeB2DetailsEntity>(storage.DetailsJson)!;

            var client = S3Client.BuildBackblazeClientOrThrow(
                keyId: details.KeyId,
                applicationKey: details.ApplicationKey,
                url: details.Url);

            return (client, [
                S3LifecycleRules.AbortIncompleteMultipartUploadsAfter7Days,
                S3LifecycleRules.DeleteNoncurrentVersionsAfter1Day
            ], null);
        }

        if (storage.Type == StorageType.CloudflareR2)
        {
            var details = Json.Deserialize<CloudflareR2DetailsEntity>(storage.DetailsJson)!;

            var client = S3Client.BuildCloudflareClientOrThrow(
                accessKeyId: details.AccessKeyId,
                secretAccessKey: details.SecretAccessKey,
                url: details.Url);

            return (client, [
                S3LifecycleRules.AbortIncompleteMultipartUploadsAfter7Days,
            ], null);
        }

        if (storage.Type == StorageType.AwsS3)
        {
            var details = Json.Deserialize<AwsS3DetailsEntity>(storage.DetailsJson)!;

            var client = S3Client.BuildAwsClientOrThrow(
                accessKey: details.AccessKey,
                secretAccessKey: details.SecretAccessKey,
                region: details.Region);

            return (client, [
                S3LifecycleRules.AbortIncompleteMultipartUploadsAfter7Days,
            ], null);
        }

        if (storage.Type == StorageType.DigitalOceanSpaces)
        {
            var details = Json.Deserialize<DigitalOceanSpacesDetailsEntity>(storage.DetailsJson)!;

            var client = S3Client.BuildDigitalOceanSpacesClientOrThrow(
                accessKey: details.AccessKey,
                secretKey: details.SecretKey,
                url: details.Url);

            return (client, [
                S3LifecycleRules.AbortIncompleteMultipartUploadsAfter7Days,
            ], null);
        }

        if (storage.Type == StorageType.GoogleCloudStorage)
        {
            var details = Json.Deserialize<GoogleCloudStorageDetailsEntity>(storage.DetailsJson)!;

            var client = S3Client.BuildGoogleCloudStorageClientOrThrow(
                accessKey: details.AccessKey,
                secretKey: details.SecretKey);

            Func<string, CancellationToken, Task> corsConfigurator =
                (bucketName, ct) => GcsCorsConfigurer.PutBucketCorsAsync(
                    accessKey: details.AccessKey,
                    secretKey: details.SecretKey,
                    bucketName: bucketName,
                    allowedOrigin: config.AppUrl,
                    cancellationToken: ct);

            // No lifecycle rules — GCS XML interop doesn't support
            // AbortIncompleteMultipartUpload. See GoogleCloudStorageClientFactory
            // for the gsutil-based operator workaround.
            return (client, [], corsConfigurator);
        }

        throw new InvalidOperationException(
            $"Unsupported storage type '{storage.Type}'.");
    }
    private sealed record StorageRow(
        int StorageId,
        StorageExtId ExternalId,
        string Name,
        StorageType Type,
        string DetailsJson,
        StorageEncryption Encryption,
        TrashPolicy DefaultTrashPolicy);
}