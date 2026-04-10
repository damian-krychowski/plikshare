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
using PlikShare.AuditLog;

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
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageIntegrations));

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await createIntegrationOperation.Execute(
            request: request,
            owner: httpContext.GetUserContext(),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        var integrationType = request switch
        {
            CreateAwsTextractIntegrationRequestDto => IntegrationType.AwsTextract,
            CreateOpenAiChatGptIntegrationRequestDto => IntegrationType.OpenaiChatgpt,
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };

        switch (result.Code)
        {
            case CreateIntegrationWithWorkspaceQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Integration.Created(
                        actor: httpContext.GetAuditLogActorContext(),
                        externalId: result.Integration.ExternalId,
                        name: request.Name,
                        type: integrationType.ToString()),
                    cancellationToken);

                return TypedResults.Ok(new CreateIntegrationResponseDto
                {
                    ExternalId = result.Integration.ExternalId,
                    Workspace = new CreateIntegrationWorkspaceDto
                    {
                        ExternalId = result.Integration.WorkspaceExternalId,
                        Name = result.Integration.WorkspaceName
                    }
                });

            case CreateIntegrationWithWorkspaceQuery.ResultCode.NameNotUnique:
                return HttpErrors.Integration.NameNotUnique(
                    request.Name);

            case CreateIntegrationWithWorkspaceQuery.ResultCode.StorageNotFound:
                return HttpErrors.Storage.NotFound(
                    result.MissingStorageExternalId!.Value);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateIntegrationWithWorkspaceQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> DeleteIntegration(
        [FromRoute] IntegrationExtId integrationExternalId,
        DeleteIntegrationQuery deleteIntegrationQuery,
        TextractClientStore textractClientStore,
        ChatGptClientStore chatGptClientStore,
        HttpContext httpContext,
        AuditLogService auditLogService,
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

            await auditLogService.Log(
                Audit.Integration.Deleted(
                    actor: httpContext.GetAuditLogActorContext(),
                    externalId: integrationExternalId,
                    name: result.Integration.Name),
                cancellationToken);
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
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = await updateIntegrationNameQuery.Execute(
            externalId: integrationExternalId,
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case UpdateIntegrationNameQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Integration.NameUpdated(
                        actor: httpContext.GetAuditLogActorContext(),
                        externalId: integrationExternalId,
                        name: request.Name),
                    cancellationToken);

                return TypedResults.Ok();

            case UpdateIntegrationNameQuery.ResultCode.NotFound:
                return HttpErrors.Integration.NotFound(
                    integrationExternalId);

            case UpdateIntegrationNameQuery.ResultCode.NameNotUnique:
                return HttpErrors.Integration.NameNotUnique(
                    request.Name);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateIntegrationNameQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> Activate(
       [FromRoute] IntegrationExtId integrationExternalId,
       ActivateIntegrationOperation activateIntegrationOperation,
       HttpContext httpContext,
       AuditLogService auditLogService,
       CancellationToken cancellationToken)
    {
        var result = await activateIntegrationOperation.Execute(
            externalId: integrationExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case ActivateIntegrationQuery.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Integration.Activated(
                        actor: httpContext.GetAuditLogActorContext(),
                        externalId: integrationExternalId,
                        name: result.Integration.Name),
                    cancellationToken);

                return TypedResults.Ok();

            case ActivateIntegrationQuery.ResultCode.NotFound:
                return HttpErrors.Integration.NotFound(
                    integrationExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ActivateIntegrationQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> Deactivate(
        [FromRoute] IntegrationExtId integrationExternalId,
        DeactivateIntegrationQuery deactivateIntegrationQuery,
        TextractClientStore textractClientStore,
        ChatGptClientStore chatGptClientStore,
        HttpContext httpContext,
        AuditLogService auditLogService,
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

            await auditLogService.Log(
                Audit.Integration.Deactivated(
                    actor: httpContext.GetAuditLogActorContext(),
                    externalId: integrationExternalId,
                    name: result.Integration.Name),
                cancellationToken);
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