using Flurl.Http;
using PlikShare.Uploads.CompleteFileUpload.Contracts;
using PlikShare.Uploads.Count.Contracts;
using PlikShare.Uploads.FilePartUpload.Complete.Contracts;
using PlikShare.Uploads.FilePartUpload.Initiate.Contracts;
using PlikShare.Uploads.GetDetails.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Uploads.List.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class UploadsApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<BulkInitiateFileUploadResponseDto> BulkInitiate(
        WorkspaceExtId workspaceExternalId,
        BulkInitiateFileUploadRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<BulkInitiateFileUploadResponseDto, BulkInitiateFileUploadRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads/initiate/bulk",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            isRequestInProtobuf: true,
            isResponseInProtobuf: true);
    }

    public async Task<GetFileUploadDetailsResponseDto> GetDetails(
        WorkspaceExtId workspaceExternalId,
        FileUploadExtId fileUploadExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetFileUploadDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads/{fileUploadExternalId}",
            cookie: cookie);
    }

    public async Task<InitiateFilePartUploadResponseDto> InitiatePartUpload(
        WorkspaceExtId workspaceExternalId,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<InitiateFilePartUploadResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads/{fileUploadExternalId}/parts/{partNumber}/initiate",
            request: new { },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task CompletePartUpload(
        WorkspaceExtId workspaceExternalId,
        FileUploadExtId fileUploadExternalId,
        int partNumber,
        CompleteFilePartUploadRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads/{fileUploadExternalId}/parts/{partNumber}/complete",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<CompleteFileUploadResponseDto> CompleteUpload(
        WorkspaceExtId workspaceExternalId,
        FileUploadExtId fileUploadExternalId,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CompleteFileUploadResponseDto, object>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads/{fileUploadExternalId}/complete",
            request: new { },
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetUploadsListResponseDto> GetList(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetUploadsListResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads",
            cookie: cookie);
    }

    public async Task<GetUploadsCountResponse> GetCount(
        WorkspaceExtId workspaceExternalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetUploadsCountResponse>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/uploads/count",
            cookie: cookie);
    }
}
