using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Create.Contracts;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Users.Cache;

namespace PlikShare.Integrations.Create;

public class CreateIntegrationOperation(
    CreateIntegrationWithWorkspaceQuery createIntegrationWithWorkspaceQuery)
{
    public async Task<CreateIntegrationWithWorkspaceQuery.Result> Execute(
        CreateIntegrationRequestDto request,
        UserContext owner,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return request switch
        {
            CreateAwsTextractIntegrationRequestDto awsTextract => await createIntegrationWithWorkspaceQuery.Execute(
                name: request.Name,
                type: IntegrationType.AwsTextract,
                details: new AwsTextractDetails
                {
                    StorageExternalId = awsTextract.StorageExternalId,
                    Region = awsTextract.Region,
                    AccessKey = awsTextract.AccessKey,
                    SecretAccessKey = awsTextract.SecretAccessKey
                },
                ownerId: owner.Id,
                correlationId: correlationId,
                cancellationToken: cancellationToken),

            CreateOpenAiChatGptIntegrationRequestDto chatGpt => await createIntegrationWithWorkspaceQuery.Execute(
                name: request.Name,
                type: IntegrationType.OpenaiChatgpt,
                details: new ChatGptDetails
                {
                    StorageExternalId = chatGpt.StorageExternalId,
                    ApiKey = chatGpt.ApiKey
                },
                ownerId: owner.Id,
                correlationId: correlationId,
                cancellationToken: cancellationToken),

            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                message: $"Unknown integration type: '{request.GetType()}'")
        };
    }
}