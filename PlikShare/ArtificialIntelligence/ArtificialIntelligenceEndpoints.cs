using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.ArtificialIntelligence.CheckConversationStatus;
using PlikShare.ArtificialIntelligence.CheckConversationStatus.Contracts;
using PlikShare.ArtificialIntelligence.DeleteConversation;
using PlikShare.ArtificialIntelligence.GetMessages;
using PlikShare.ArtificialIntelligence.GetMessages.Contracts;
using PlikShare.ArtificialIntelligence.SendFileMessage;
using PlikShare.ArtificialIntelligence.SendFileMessage.Contracts;
using PlikShare.ArtificialIntelligence.UpdateConversationName;
using PlikShare.ArtificialIntelligence.UpdateConversationName.Contracts;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Validation;

namespace PlikShare.ArtificialIntelligence;

public static class ArtificialIntelligenceEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("ArtificialIntelligence")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapPost("/conversations/check-status", CheckConversationsStatus)
            .WithName("CheckConversationsStatus");

        var workspaceGroup = group.MapGroup("/workspaces/{workspaceExternalId}")
            .WithTags("ArtificialIntelligence_Workspace")
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        workspaceGroup.MapPost("/files/{fileExternalId}/messages", SendFileAiMessage)
            .WithName("SendFileAiMessage");

        workspaceGroup.MapPatch("/files/{fileExternalId}/conversations/{fileArtifactExternalId}/name", UpdateAiConversationName)
            .WithName("UpdateAiConversationName");

        workspaceGroup.MapDelete("/files/{fileExternalId}/conversations/{fileArtifactExternalId}", DeleteAiConversation)
            .WithName("DeleteAiConversation");

        workspaceGroup.MapGet("/files/{fileExternalId}/conversations/{fileArtifactExternalId}/messages", GetAiConversationMessages)
            .WithName("GetAiConversationMessages");
    }

    private static CheckAiConversationStatusResponseDto CheckConversationsStatus(
        [FromBody] CheckAiConversationStatusRequestDto request,
        CheckAiConversationsStatusQuery checkAiConversationsStatusQuery)
    {
        var response = checkAiConversationsStatusQuery.Execute(
            request: request);

        return response;
    }

    private static async ValueTask<Results<Ok<GetAiMessagesResponseDto>, NotFound<HttpError>>> GetAiConversationMessages(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId fileArtifactExternalId,
        [FromQuery] int? fromConversationCounter,
        HttpContext httpContext,
        GetAiMessagesOperation getAiMessagesOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await getAiMessagesOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            fileArtifactExternalId: fileArtifactExternalId,
            fromConversationCounter: fromConversationCounter ?? 0,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetAiMessagesOperation.ResultCode.Ok => TypedResults.Ok(
                result.Response),

            GetAiMessagesOperation.ResultCode.NotFound =>
                HttpErrors.ArtificialIntelligence.ConversationNotFound(fileArtifactExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeleteAiConversationOperation),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteAiConversation(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId fileArtifactExternalId,
        HttpContext httpContext,
        DeleteAiConversationOperation deleteAiConversationOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await deleteAiConversationOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            fileArtifactExternalId: fileArtifactExternalId,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            DeleteAiConversationOperation.ResultCode.Ok => TypedResults.Ok(),

            DeleteAiConversationOperation.ResultCode.AiConversationNotFound =>
                HttpErrors.ArtificialIntelligence.ConversationNotFound(fileArtifactExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(DeleteAiConversationOperation),
                resultValueStr: resultCode.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateAiConversationName(
        [FromRoute] FileExtId fileExternalId,
        [FromRoute] FileArtifactExtId fileArtifactExternalId,
        [FromBody] UpdateAiConversationNameRequestDto request,
        HttpContext httpContext,
        UpdateAiConversationNameOperation updateAiConversationNameOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateAiConversationNameOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            fileArtifactExternalId: fileArtifactExternalId,
            name: request.Name,
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            UpdateAiConversationNameOperation.ResultCode.Ok => TypedResults.Ok(),

            UpdateAiConversationNameOperation.ResultCode.AiConversationNotFound =>
                HttpErrors.ArtificialIntelligence.ConversationNotFound(fileArtifactExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateAiConversationNameOperation),
                resultValueStr: resultCode.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> SendFileAiMessage(
        [FromBody] SendAiFileMessageRequestDto request,
        [FromRoute] FileExtId fileExternalId,
        HttpContext httpContext,
        SendAiFileMessageOperation sendAiFileMessageOperation,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await sendAiFileMessageOperation.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalId: fileExternalId,
            request: request,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return result switch
        {
            SendAiFileMessageOperation.ResultCode.Ok => TypedResults.Ok(),

            SendAiFileMessageOperation.ResultCode.FileNotFound =>
                HttpErrors.File.NotFound(fileExternalId),

            SendAiFileMessageOperation.ResultCode.StaleCounter =>
                HttpErrors.ArtificialIntelligence.StaleConversationCounter(),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(SendAiFileMessageOperation),
                resultValueStr: result.ToString())
        };
    }
}