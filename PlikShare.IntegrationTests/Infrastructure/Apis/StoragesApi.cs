using Flurl.Http;
using PlikShare.Storages.AzureBlob.Create.Contracts;
using PlikShare.Storages.AzureBlob.UpdateDetails.Contracts;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.HardDrive.GetVolumes.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;

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

    public async Task UpdateAzureBlobStorageDetails(
        StorageExtId externalId,
        UpdateAzureBlobStorageDetailsRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/storages/azure-blob/{externalId}/details",
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
}