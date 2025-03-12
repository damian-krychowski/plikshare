using Flurl.Http;
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
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecutePost<CreateHardDriveStorageResponseDto,CreateHardDriveStorageRequestDto>(
            appUrl: appUrl,
            apiPath: "api/storages/hard-drive",
            request: request,
            cookie: cookie);
    }

    public async Task DeleteStorage(
        StorageExtId externalId,
        SessionAuthCookie? cookie)
    {
        await flurlClient.ExecuteDelete(
            appUrl: appUrl,
            apiPath: $"api/storages/{externalId}",
            cookie: cookie);
    }
}