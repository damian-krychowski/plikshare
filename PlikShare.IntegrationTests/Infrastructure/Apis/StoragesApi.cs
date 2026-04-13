using System.Diagnostics;
using Flurl.Http;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.HardDrive.GetVolumes.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;
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

    public async Task<GenericCookie> UnlockFullEncryption(
        StorageExtId externalId,
        UnlockFullEncryptionRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, $"api/storages/{externalId}/unlock-full-encryption")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var cookieName = FullEncryptionSessionCookie.GetCookieName(externalId);

        var sessionCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == cookieName);

        Debug.Assert(sessionCookie != null,
            $"Set-Cookie '{cookieName}' was not present in unlock response.");

        return new GenericCookie(cookieName, sessionCookie!.Value);
    }

    public async Task<GenericCookie> ChangeMasterPassword(
        StorageExtId externalId,
        ChangeMasterPasswordRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        var response = await flurlClient
            .Request(appUrl, $"api/storages/{externalId}/change-master-password")
            .AllowAnyHttpStatus()
            .WithCookie(cookie)
            .WithAntiforgery(antiforgery)
            .PostJsonAsync(request);

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            throw new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);
        }

        var cookieName = FullEncryptionSessionCookie.GetCookieName(externalId);

        var sessionCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == cookieName);

        Debug.Assert(sessionCookie != null,
            $"Set-Cookie '{cookieName}' was not present in change-master-password response.");

        return new GenericCookie(cookieName, sessionCookie!.Value);
    }

    public async Task ResetMasterPassword(
        StorageExtId externalId,
        ResetMasterPasswordRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/storages/{externalId}/reset-master-password",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}