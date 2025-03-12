using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Utils;
using PlikShare.Integrations.Activate;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Create;
using PlikShare.Integrations.Create.Contracts;
using PlikShare.Integrations.Deactivate;
using PlikShare.Integrations.Delete;
using PlikShare.Integrations.Id;
using PlikShare.Integrations.List;
using PlikShare.Integrations.List.Contracts;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Integrations.UpdateName;
using PlikShare.Integrations.UpdateName.Contracts;
using PlikShare.Users.Middleware;

namespace PlikShare.Integrations;

public static class IntegrationsEndpoints
{
    public static void MapIntegrationsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/integrations")
            .WithTags("Integrations")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter<RequireAppOwnerEndpointFilter>();

        group.MapGet("/", GetIntegrations)
            .WithName("GetIntegrations");

        group.MapPost("/", CreateIntegration)
            .WithName("CreateIntegration");

        group.MapDelete("/{integrationExternalId}", DeleteIntegration)
            .WithName("DeleteIntegration");

        group.MapPatch("/{integrationExternalId}/name", UpdateName)
            .WithName("UpdateIntegrationName");

        group.MapPost("/{integrationExternalId}/activate", Activate)
            .WithName("ActivateIntegration");

        group.MapPost("/{integrationExternalId}/deactivate", Deactivate)
            .WithName("DeactivateIntegration");
    }

    private static GetIntegrationsResponseDto GetIntegrations(
        GetIntegrationsQuery getIntegrationsQuery)
    {
        return getIntegrationsQuery.Execute();
    }

    private static async Task<Results<Ok<CreateIntegrationResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> CreateIntegration(
        [FromBody] CreateIntegrationRequestDto request,
        HttpContext httpContext,
        CreateIntegrationOperation createIntegrationOperation,
        CancellationToken cancellationToken)
    {
        var result = await createIntegrationOperation.Execute(
            request: request,
            owner: httpContext.GetUserContext(),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateIntegrationWithWorkspaceQuery.ResultCode.Ok => TypedResults.Ok(new CreateIntegrationResponseDto
            {
                ExternalId = result.Integration.ExternalId,
                Workspace = new CreateIntegrationWorkspaceDto
                {
                    ExternalId = result.Integration.WorkspaceExternalId,
                    Name = result.Integration.WorkspaceName
                }
            }),

            CreateIntegrationWithWorkspaceQuery.ResultCode.NameNotUnique => 
                HttpErrors.Integration.NameNotUnique(
                    request.Name),

            CreateIntegrationWithWorkspaceQuery.ResultCode.StorageNotFound => 
                HttpErrors.Storage.NotFound(
                    result.MissingStorageExternalId!.Value),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateIntegrationWithWorkspaceQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteIntegration(
        [FromRoute] IntegrationExtId integrationExternalId,
        DeleteIntegrationQuery deleteIntegrationQuery,
        TextractClientStore textractClientStore,
        ChatGptClientStore chatGptClientStore,
        CancellationToken cancellationToken)
    {
        var result = await deleteIntegrationQuery.Execute(
            externalId: integrationExternalId,
            cancellationToken: cancellationToken);

        if (result.Code == DeleteIntegrationQuery.ResultCode.Ok)
        {
            if (result.Integration.Type == IntegrationType.AwsTextract)
            {
                textractClientStore.RemoveClient(
                    integrationId: result.Integration.Id);
            }

            if (result.Integration.Type == IntegrationType.OpenaiChatgpt)
            {
                chatGptClientStore.RemoveClient(
                    integrationId: result.Integration.Id);
            }
        }

        return result.Code switch
        {
            DeleteIntegrationQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            DeleteIntegrationQuery.ResultCode.NotFound => 
                HttpErrors.Integration.NotFound(
                    integrationExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeleteIntegrationQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> UpdateName(
        [FromRoute] IntegrationExtId integrationExternalId,
        [FromBody] UpdateIntegrationNameRequestDto request,
        UpdateIntegrationNameQuery updateIntegrationNameQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateIntegrationNameQuery.Execute(
            externalId: integrationExternalId, 
            name: request.Name,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            UpdateIntegrationNameQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            UpdateIntegrationNameQuery.ResultCode.NotFound =>
                HttpErrors.Integration.NotFound(
                    integrationExternalId),

            UpdateIntegrationNameQuery.ResultCode.NameNotUnique => 
                HttpErrors.Integration.NameNotUnique(
                    request.Name),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateIntegrationNameQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> Activate(
       [FromRoute] IntegrationExtId integrationExternalId,
       ActivateIntegrationOperation activateIntegrationOperation,
       CancellationToken cancellationToken)
    {
        var result = await activateIntegrationOperation.Execute(
            externalId: integrationExternalId,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            ActivateIntegrationQuery.ResultCode.Ok => 
                TypedResults.Ok(),
            
            ActivateIntegrationQuery.ResultCode.NotFound => 
                HttpErrors.Integration.NotFound(
                    integrationExternalId),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(ActivateIntegrationQuery),
                resultValueStr: result.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Deactivate(
        [FromRoute] IntegrationExtId integrationExternalId,
        DeactivateIntegrationQuery deactivateIntegrationQuery,
        TextractClientStore textractClientStore,
        ChatGptClientStore chatGptClientStore,
        CancellationToken cancellationToken)
    {
        var result = await deactivateIntegrationQuery.Execute(
            externalId: integrationExternalId,
            cancellationToken: cancellationToken);

        if (result.Code == DeactivateIntegrationQuery.ResultCode.Ok)
        {
            if (result.Integration.Type == IntegrationType.AwsTextract)
            {
                textractClientStore.RemoveClient(
                    integrationId: result.Integration.Id);
            }

            if (result.Integration.Type == IntegrationType.OpenaiChatgpt)
            {
                chatGptClientStore.RemoveClient(
                    integrationId: result.Integration.Id);
            }
        }

        return result.Code switch
        {
            DeactivateIntegrationQuery.ResultCode.Ok => 
                TypedResults.Ok(),
            
            DeactivateIntegrationQuery.ResultCode.NotFound => 
                HttpErrors.Integration.NotFound(
                    integrationExternalId),
            
            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeactivateIntegrationQuery),
                resultValueStr: result.ToString())
        };
    }
}