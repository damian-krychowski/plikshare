using Amazon;
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
    public static void UseStorage(this WebApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        
        //without that presigned urls does not work
        //AWSConfigsS3.
        
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
                    var type = reader.GetString(2);
                    var detailsJson = masterDataEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(3));

                    var encryptionType = StorageEncryptionExtensions.FromDbValue(
                        dbValue: reader.GetStringOrNull(4));
                    
                    return new
                    {
                        StorageId = storageId,
                        ExternalId = externalId,
                        Type = type,
                        DetailsJson = detailsJson,
                        EncryptionType = encryptionType,
                        EncryptionDetails = encryptionType == StorageEncryptionType.None
                            ? null
                            : StorageEncryptionExtensions.GetEncryptionDetails(
                                encryptionType: encryptionType,
                                encryptionDetailsJson: masterDataEncryption.Decrypt(
                                    reader.GetFieldValue<byte[]>(5)))
                    };
                })
            .Execute();
        
        foreach (var storage in storages)
        {
            if (storage.Type == StorageType.BackblazeB2)
            {
                var details = Json.Deserialize<BackblazeB2DetailsEntity>(
                    storage.DetailsJson);

                var client = S3Client.BuildBackblazeClientOrThrow(
                    keyId: details!.KeyId,
                    applicationKey: details.ApplicationKey,
                    url: details.Url);

                clientStore.RegisterClient(new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: client,
                    storageId: storage.StorageId,
                    externalId: storage.ExternalId,
                    storageType: storage.Type,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: storage.EncryptionType,
                    encryptionDetails: storage.EncryptionDetails));
            }

            if (storage.Type == StorageType.CloudflareR2)
            {
                var details = Json.Deserialize<CloudflareR2DetailsEntity>(
                    storage.DetailsJson);

                var client = S3Client.BuildCloudflareClientOrThrow(
                    accessKeyId: details!.AccessKeyId,
                    secretAccessKey: details.SecretAccessKey,
                    url: details.Url);
                
                clientStore.RegisterClient(new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: client,
                    storageId: storage.StorageId,
                    externalId: storage.ExternalId,
                    storageType: storage.Type,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: storage.EncryptionType,
                    encryptionDetails: storage.EncryptionDetails));
            }

            if (storage.Type == StorageType.AwsS3)
            {
                var details = Json.Deserialize<AwsS3DetailsEntity>(
                    storage.DetailsJson);

                var client = S3Client.BuildAwsClientOrThrow(
                    accessKey: details!.AccessKey,
                    secretAccessKey: details.SecretAccessKey,
                    region: details.Region);
                
                clientStore.RegisterClient(new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: client,
                    storageId: storage.StorageId,
                    externalId: storage.ExternalId,
                    storageType: storage.Type,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: storage.EncryptionType,
                    encryptionDetails: storage.EncryptionDetails));
            }
            
            if (storage.Type == StorageType.DigitalOceanSpaces)
            {
                var details = Json.Deserialize<DigitalOceanSpacesDetailsEntity>(
                    storage.DetailsJson);

                var client = S3Client.BuildDigitalOceanSpacesClientOrThrow(
                    accessKey: details!.AccessKey,
                    secretKey: details.SecretKey,
                    url: details.Url);
                
                clientStore.RegisterClient(new S3StorageClient(
                    appUrl: config.AppUrl,
                    clock: clock,
                    s3Client: client,
                    storageId: storage.StorageId,
                    externalId: storage.ExternalId,
                    storageType: storage.Type,
                    preSignedUrlsService: preSignedUrlsService,
                    encryptionType: storage.EncryptionType,
                    encryptionDetails: storage.EncryptionDetails));
            }

            if (storage.Type == StorageType.HardDrive)
            {
                clientStore.RegisterClient(new HardDriveStorageClient(
                    preSignedUrlsService: app
                        .Services
                        .GetRequiredService<PreSignedUrlsService>(),
                    details: Json.Deserialize<HardDriveDetailsEntity>(
                        storage.DetailsJson)!,
                    storageId: storage.StorageId,
                    externalId: storage.ExternalId,
                    clock: clock,
                    encryptionType: storage.EncryptionType,
                    encryptionDetails: storage.EncryptionDetails));
            }
        }
        
        Log.Information("[INITIALIZATION] S3 Client Store initialization finished.");
    }
}