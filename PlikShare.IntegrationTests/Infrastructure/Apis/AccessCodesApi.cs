using System.Diagnostics;
using Flurl.Http;
using Microsoft.Extensions.Primitives;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class AccessCodesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<BoxLinkToken> StartSession(
        AntiforgeryCookies antiforgeryCookies)
    {
        var response = await flurlClient
            .Request(appUrl, "api/access-codes/start-session")
            .AllowAnyHttpStatus()
            .WithAntiforgery(antiforgeryCookies)
            .PostAsync();

        if (!response.ResponseMessage.IsSuccessStatusCode)
        {
            var exception = new TestApiCallException(
                responseBody: await response.GetStringAsync(),
                statusCode: response.StatusCode);

            throw exception;
        }

        var boxLinkTokenHeader = response
            .Headers
            .FirstOrDefault(c => c.Name == HeaderName.BoxLinkToken);
        
        return new BoxLinkToken(
            boxLinkTokenHeader.Value);
    }

    public async Task<GetBoxDetailsAndContentResponseDto> GetBoxDetailsAndContent(
        string accessCode,
        BoxLinkToken? boxLinkToken)
    {
        return await flurlClient.ExecuteGet<GetBoxDetailsAndContentResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}",
            cookie: null,
            isResponseInProtobuf: true,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<CreateFolderResponseDto> CreateFolder(
        string accessCode,
        CreateFolderRequestDto request,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        return await flurlClient.ExecutePost<CreateFolderResponseDto, CreateFolderRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/folders",
            request: request,
            cookie: null,
            antiforgery: antiforgery,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }
    
    public async Task UpdateFolderName(
        string accessCode,
        FolderExtId folderExternalId,
        UpdateBoxFolderNameRequestDto request,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/folders/{folderExternalId}/name",
            request: request,
            cookie: null,
            antiforgery: antiforgery,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }
}

public class BoxLinkToken(string value) : Header
{
    public override string Name => HeaderName.BoxLinkToken;
    public override string Value { get; } = value;
}