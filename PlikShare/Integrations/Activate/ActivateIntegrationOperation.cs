using PlikShare.Integrations.Aws.Textract.Register;
using PlikShare.Integrations.Id;
using PlikShare.Integrations.OpenAi.ChatGpt.Register;

namespace PlikShare.Integrations.Activate;

public class ActivateIntegrationOperation(
    ActivateIntegrationQuery activateIntegrationQuery,
    RegisterTextractClientOperation registerTextractClientOperation,
    RegisterChatGptClientOperation registerChatGptClientOperation)
{
    public async Task<ActivateIntegrationQuery.Result> Execute(
        IntegrationExtId externalId,
        CancellationToken cancellationToken)
    {
        var result = await activateIntegrationQuery.Execute(
            externalId: externalId,
            cancellationToken: cancellationToken);

        if (result is { Code: ActivateIntegrationQuery.ResultCode.Ok, Integration.Type: IntegrationType.AwsTextract })
        {
            registerTextractClientOperation.ExecuteOrThrow(
                integrationId: result.Integration.Id);
        }

        if (result is { Code: ActivateIntegrationQuery.ResultCode.Ok, Integration.Type: IntegrationType.OpenaiChatgpt })
        {
            registerChatGptClientOperation.ExecuteOrThrow(
                integrationId: result.Integration.Id);
        }

        return result;
    }
}