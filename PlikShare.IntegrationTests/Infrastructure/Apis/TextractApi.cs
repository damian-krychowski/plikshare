using Flurl.Http;
using PlikShare.Integrations.Aws.Textract.Jobs.StartJob.Contracts;
using PlikShare.Workspaces.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class TextractApi(IFlurlClient flurlClient, string appUrl)
{
    public async Task<StartTextractJobResponseDto> StartJob(
        WorkspaceExtId workspaceExternalId,
        StartTextractJobRequestDto request,
        SessionAuthCookie? cookie,
        AntiforgeryCookies antiforgery)
    {
        return await flurlClient.ExecutePost<StartTextractJobResponseDto, StartTextractJobRequestDto>(
            appUrl: appUrl,
            apiPath: $"api/workspaces/{workspaceExternalId.Value}/aws-textract/jobs",
            request: request,
            cookie: cookie,
            antiforgery: antiforgery);
    }
}
