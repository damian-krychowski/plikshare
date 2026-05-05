using Flurl.Http;
using PlikShare.Storages.AzureBlob.Create.Contracts;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.HardDrive.GetVolumes.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;
using PlikShare.Storages.S3.AwsS3.Create.Contracts;
using PlikShare.Storages.S3.BackblazeB2.Create.Contracts;
using PlikShare.Storages.S3.CloudflareR2.Create.Contracts;
using PlikShare.Storages.S3.DigitalOcean.Create.Contracts;
using PlikShare.Storages.S3.GoogleCloudStorage.Create.Contracts;
using PlikShare.Storages.UpdateName.Contracts;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class StoragesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<GetStoragesResponseDto> Get(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetStoragesResponseDto>(
            appUrl: appUrl,
            apiPath: "api/storages",
            cookie: cookie);
    }

    public async Task<GetHardDriveVolumesResponseDto> GetHardDriveVolumes(SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetHardDriveVolumesResponseDto>(
            appUrl: appUrl,
            apiPath: "api/storages/hard-drive/volumes",
            cookie: cookie);
    }

    public async Task<CreateHardDriveStorageResponseDto> CreateHardDriveStorage(
        CreateHardDriveStorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateHardDriveStorageResponseDto,CreateHardDriveStorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/hard-drive",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateAwsS3StorageResponseDto> CreateAwsS3Storage(
        CreateAwsS3StorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateAwsS3StorageResponseDto, CreateAwsS3StorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/aws-s3",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateCloudflareR2StorageResponseDto> CreateCloudflareR2Storage(
        CreateCloudflareR2StorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateCloudflareR2StorageResponseDto, CreateCloudflareR2StorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/cloudflare-r2",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateBackblazeB2StorageResponseDto> CreateBackblazeB2Storage(
        CreateBackblazeB2StorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateBackblazeB2StorageResponseDto, CreateBackblazeB2StorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/backblaze-b2",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateDigitalOceanSpacesStorageResponseDto> CreateDigitalOceanSpacesStorage(
        CreateDigitalOceanSpacesStorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateDigitalOceanSpacesStorageResponseDto, CreateDigitalOceanSpacesStorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/digital-ocean-spaces",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateAzureBlobStorageResponseDto> CreateAzureBlobStorage(
        CreateAzureBlobStorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateAzureBlobStorageResponseDto, CreateAzureBlobStorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/azure-blob",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CreateGoogleCloudStorageResponseDto> CreateGoogleCloudStorage(
        CreateGoogleCloudStorageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateGoogleCloudStorageResponseDto, CreateGoogleCloudStorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/google-cloud-storage",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task DeleteStorage(
        StorageExtId externalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/storages/{externalId}",
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task UpdateName(
        StorageExtId externalId,
        UpdateStorageNameRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/storages/{externalId}/name",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

}