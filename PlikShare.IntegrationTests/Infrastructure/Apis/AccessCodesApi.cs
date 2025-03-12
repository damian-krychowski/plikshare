using System.Diagnostics;
using Flurl.Http;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AccessCodesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<BoxLinkAuthCookie> StartSession()
    {
        var response = await flurlClient
            .Request(appUrl, "api/access-codes/start-session")
            .AllowAnyHttpStatus()
            .PostAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var boxLinkAuthCookie = response
            .Cookies
            .FirstOrDefault(c => c.Name == CookieName.BoxLinkAuth);

        Debug.Assert(boxLinkAuthCookie != null);

        return new BoxLinkAuthCookie(boxLinkAuthCookie.Value);
    }

    public async Task<GetBoxDetailsAndContentResponseDto> GetBoxDetailsAndContent(
        string accessCode,
        BoxLinkAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetBoxDetailsAndContentResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}",
            cookie: cookie,
            isResponseInProtobuf: true);
    }

    public async Task<CreateFolderResponseDto> CreateFolder(
        string accessCode,
        CreateFolderRequestDto request,
        BoxLinkAuthCookie? cookie)
    {
        return await flurlClient.ExecutePost<CreateFolderResponseDto, CreateFolderRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/folders",
            request: request,
            cookie: cookie);
    }
    
    public async Task UpdateFolderName(
        string accessCode,
        FolderExtId folderExternalId,
        UpdateBoxFolderNameRequestDto request,
        BoxLinkAuthCookie? cookie)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/folders/{folderExternalId}/name",
            request: request,
            cookie: cookie);
    }
}