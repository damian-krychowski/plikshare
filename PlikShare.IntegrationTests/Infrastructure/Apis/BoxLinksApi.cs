using Flurl.Http;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class BoxLinksApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task UpdatePermissions(
        WorkspaceExtId workspaceExternalId,
        BoxLinkExtId boxLinkExternalId,
        UpdateBoxLinkPermissionsRequestDto request,
        SessionAuthCookie? cookie)
    {
        await flurlClient.ExecutePatch(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId}/box-links/{boxLinkExternalId}/permissions",
            request: request,
            cookie: cookie);
    }
}