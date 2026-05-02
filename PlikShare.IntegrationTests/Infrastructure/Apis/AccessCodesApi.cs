using Flurl.Http;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;

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

    public async Task<GetBoxFileDownloadLinkResponseDto> GetFileDownloadLink(
        string accessCode,
        FileExtId fileExternalId,
        string contentDisposition,
        BoxLinkToken? boxLinkToken)
    {
        return await flurlClient.ExecuteGet<GetBoxFileDownloadLinkResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/files/{fileExternalId}/download-link?contentDisposition={contentDisposition}",
            cookie: null,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<GetBulkDownloadLinkResponseDto> GetBulkDownloadLink(
        string accessCode,
        GetBulkDownloadLinkRequestDto request,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        return await flurlClient.ExecutePost<GetBulkDownloadLinkResponseDto, GetBulkDownloadLinkRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/files/bulk-download-link",
            request: request,
            cookie: null,
            antiforgery: antiforgery,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<GetZipFileDetailsResponseDto> GetZipFilePreviewDetails(
        string accessCode,
        FileExtId fileExternalId,
        BoxLinkToken? boxLinkToken)
    {
        return await flurlClient.ExecuteGet<GetZipFileDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/files/{fileExternalId}/preview/zip",
            cookie: null,
            isResponseInProtobuf: true,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<GetZipContentDownloadLinkResponseDto> GetZipContentDownloadLink(
        string accessCode,
        FileExtId fileExternalId,
        GetZipContentDownloadLinkRequestDto request,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        return await flurlClient.ExecutePost<GetZipContentDownloadLinkResponseDto, GetZipContentDownloadLinkRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/files/{fileExternalId}/preview/zip/download-link",
            request: request,
            cookie: null,
            antiforgery: antiforgery,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<BulkInitiateFileUploadResponseDto> BulkInitiateFileUpload(
        string accessCode,
        BulkInitiateFileUploadRequestDto request,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        return await flurlClient.ExecutePost<BulkInitiateFileUploadResponseDto, BulkInitiateFileUploadRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/uploads/initiate/bulk",
            request: request,
            cookie: null,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<InitiateBoxFilePartUploadResponseDto> InitiateFilePartUpload(
        string accessCode,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        return await flurlClient.ExecutePost<InitiateBoxFilePartUploadResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/uploads/{fileUploadExternalId}/parts/{partNumber}/initiate",
            request: new { },
            cookie: null,
            antiforgery: antiforgery,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task CompleteFilePartUpload(
        string accessCode,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        CompleteBoxFilePartUploadRequestDto request,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/uploads/{fileUploadExternalId}/parts/{partNumber}/complete",
            request: request,
            cookie: null,
            antiforgery: antiforgery,
            headers: boxLinkToken is null
                ? null
                : [boxLinkToken]);
    }

    public async Task<CompleteBoxFileUploadResponseDto> CompleteUpload(
        string accessCode,
        FileUploadExtId fileUploadExternalId,
        BoxLinkToken? boxLinkToken,
        AntiforgeryCookies? antiforgery = null)
    {
        return await flurlClient.ExecutePost<CompleteBoxFileUploadResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/access-codes/{accessCode}/uploads/{fileUploadExternalId}/complete",
            request: new { },
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