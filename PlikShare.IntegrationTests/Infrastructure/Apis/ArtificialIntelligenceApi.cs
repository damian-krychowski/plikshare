using Flurl.Http;
using PlikShare.ArtificialIntelligence.SendFileMessage.Contracts;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class ArtificialIntelligenceApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task SendFileMessage(
        WorkspaceExtId workspaceExternalId,
        FileExtId fileExternalId,
        SendAiFileMessageRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery,
        Cookie? workspaceEncryptionSession = null)
    {
        await flurlClient.ExecutePost(
            appUrl: appUrl,
            apiPath: $"api/ai/workspaces/{workspaceExternalId.Value}/files/{fileExternalId.Value}/messages",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery,
            extraCookie: workspaceEncryptionSession);
    }
}
