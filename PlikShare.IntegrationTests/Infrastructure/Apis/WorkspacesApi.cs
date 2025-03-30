using Flurl.Http;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Get.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class WorkspacesApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<CreateWorkspaceResponseDto> Create(
        CreateWorkspaceRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<CreateWorkspaceResponseDto, CreateWorkspaceRequestDto>(
            appUrl: appUrl,
            apiPath: "api/workspaces",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }

    public async Task<GetWorkspaceDetailsResponseDto> GetDetails(
        WorkspaceExtId externalId,
        SessionAuthCookie? cookie)
    {
        return await flurlClient.ExecuteGet<GetWorkspaceDetailsResponseDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{externalId.Value}",
            cookie: cookie);
    }
}