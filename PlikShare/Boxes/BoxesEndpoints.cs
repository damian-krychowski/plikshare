using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Create;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.CreateLink;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Delete;
using PlikShare.Boxes.Get;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.List;
using PlikShare.Boxes.List.Contracts;
using PlikShare.Boxes.Members.CreateInvitation;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.Boxes.Members.Revoke;
using PlikShare.Boxes.Members.UpdatePermissions;
using PlikShare.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Boxes.Permissions;
using PlikShare.Boxes.UpdateFolder;
using PlikShare.Boxes.UpdateFolder.Contracts;
using PlikShare.Boxes.UpdateFooter;
using PlikShare.Boxes.UpdateFooter.Contracts;
using PlikShare.Boxes.UpdateFooterIsEnabled;
using PlikShare.Boxes.UpdateFooterIsEnabled.Contracts;
using PlikShare.Boxes.UpdateHeader;
using PlikShare.Boxes.UpdateHeader.Contracts;
using PlikShare.Boxes.UpdateHeaderIsEnabled;
using PlikShare.Boxes.UpdateHeaderIsEnabled.Contracts;
using PlikShare.Boxes.UpdateIsEnabled;
using PlikShare.Boxes.UpdateIsEnabled.Contracts;
using PlikShare.Boxes.UpdateName;
using PlikShare.Boxes.UpdateName.Contracts;
using PlikShare.Boxes.Validation;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Utils;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Members.CountAll;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Boxes;

public static class BoxesEndpoints
{
    public static void MapBoxesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/boxes")
            .WithTags("Boxes")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        group.MapPost("/", CreateBox)
            .WithName("CreateBox");

        group.MapGet("/", GetBoxes)
            .WithName("GetBoxes");

        group.MapGet("/{boxExternalId}", GetBox)
            .WithName("GetBox")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/name", UpdateBoxName)
            .WithName("UpdateBoxName")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/header/is-enabled", UpdateBoxHeaderIsEnabled)
            .WithName("UpdateBoxHeaderIsEnabled")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/header", UpdateBoxHeader)
            .WithName("UpdateBoxHeader")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/footer/is-enabled", UpdateBoxFooterIsEnabled)
            .WithName("UpdateBoxFooterIsEnabled")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/footer", UpdateBoxFooter)
            .WithName("UpdateBoxFooter")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/folder", UpdateBoxFolder)
            .WithName("UpdateBoxFolder")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/is-enabled", UpdateBoxIsEnabled)
            .WithName("UpdateBoxIsEnabled")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapDelete("/{boxExternalId}", DeleteBox)
            .WithName("DeleteBox")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPost("/{boxExternalId}/members", InviteMember)
            .WithName("InviteBoxMember")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapDelete("/{boxExternalId}/members/{memberExternalId}", RevokeMember)
            .WithName("RevokeBoxMember")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPatch("/{boxExternalId}/members/{memberExternalId}/permissions", UpdateMemberPermissions)
            .WithName("UpdateBoxMemberPermissions")
            .AddEndpointFilter<ValidateBoxFilter>();

        group.MapPost("/{boxExternalId}/box-links", CreateBoxLink)
            .WithName("CreateBoxLink")
            .AddEndpointFilter<ValidateBoxFilter>();
    }

    private static async Task<Results<Ok<CreateBoxResponseDto>, NotFound<HttpError>>> CreateBox(
        [FromBody] CreateBoxRequestDto request,
        HttpContext httpContext,
        CreateBoxQuery createBoxQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await createBoxQuery.Execute(
            workspace: workspaceMembership.Workspace,
            name: request.Name,
            folderExternalId: request.FolderExternalId,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateBoxQuery.ResultCode.Ok => TypedResults.Ok(new CreateBoxResponseDto(
                ExternalId: result.BoxExternalId)),

            CreateBoxQuery.ResultCode.FolderWasNotFound => HttpErrors.Folder.NotFound(request.FolderExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateBoxQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static GetBoxesResponseDto GetBoxes(
        HttpContext httpContext,
        GetBoxesListQuery getBoxesListQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getBoxesListQuery.Execute(
            workspace: workspaceMembership.Workspace);

        return response;
    }

    private static GetBoxResponseDto GetBox(
        [FromRoute] BoxExtId boxExternalId,
        HttpContext httpContext,
        GetBoxQuery getBoxQuery,
        CancellationToken cancellationToken)
    {
        var response = getBoxQuery.Execute(
            box: httpContext.GetBoxContext());

        return response;
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxName(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxNameRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxNameQuery updateBoxNameQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateBoxNameQuery.Execute(
            box: httpContext.GetBoxContext(),
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case UpdateBoxNameQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxNameQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxNameQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxHeaderIsEnabled(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxHeaderIsEnabledRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxHeaderIsEnabledQuery updateBoxHeaderIsEnabledQuery,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxHeaderIsEnabledQuery.Execute(
            box: httpContext.GetBoxContext(),
            isHeaderEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxHeaderIsEnabledQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxHeaderIsEnabledQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxHeaderIsEnabledQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxHeader(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxHeaderRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxHeaderQuery updateBoxHeaderQuery,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxHeaderQuery.Execute(
            box: httpContext.GetBoxContext(),
            json: request.Json,
            html: request.Html,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxHeaderQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxHeaderQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxHeaderQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxFooterIsEnabled(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxFooterIsEnabledRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxFooterIsEnabledQuery updateBoxFooterIsEnabledQuery,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxFooterIsEnabledQuery.Execute(
            box: httpContext.GetBoxContext(),
            isFooterEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxFooterIsEnabledQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxFooterIsEnabledQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxFooterIsEnabledQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxFooter(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxFooterRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxFooterQuery updateBoxFooterQuery,
        CancellationToken cancellationToken)
    {
        var resultCode = await updateBoxFooterQuery.Execute(
            box: httpContext.GetBoxContext(),
            json: request.Json,
            html: request.Html,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxFooterQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxFooterQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxFooterQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxFolder(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxFolderRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxFolderQuery updateBoxFolderQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateBoxFolderQuery.Execute(
            box: httpContext.GetBoxContext(),
            folderExternalId: request.FolderExternalId,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case UpdateBoxFolderQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxFolderQuery.ResultCode.FolderNotFound:
                return HttpErrors.Folder.NotFound(request.FolderExternalId);

            case UpdateBoxFolderQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxFolderQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxIsEnabled(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxIsEnabledRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxIsEnabledQuery updateBoxIsEnabledQuery,
        CancellationToken cancellationToken)
    {
        var result = await updateBoxIsEnabledQuery.Execute(
            box: httpContext.GetBoxContext(),
            isEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case UpdateBoxIsEnabledQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case UpdateBoxIsEnabledQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(UpdateBoxIsEnabledQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> DeleteBox(
        [FromRoute] BoxExtId boxExternalId,
        HttpContext httpContext,
        BoxCache boxCache,
        ScheduleBoxesDeleteQuery scheduleBoxesDeleteQuery,
        CancellationToken cancellationToken)
    {
        var resultCode = await scheduleBoxesDeleteQuery.Execute(
            box: httpContext.GetBoxContext(),
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case ScheduleBoxesDeleteQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);
                return TypedResults.Ok();

            case ScheduleBoxesDeleteQuery.ResultCode.BoxesNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ScheduleBoxesDeleteQuery),
                    resultValueStr: resultCode.ToString());
        }
    }

    private static async ValueTask<Results<Ok<CreateBoxInvitationResponseDto>, NotFound<HttpError>, BadRequest<HttpError>>> InviteMember(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] CreateBoxInvitationRequestDto request,
        HttpContext httpContext,
        CreateBoxMemberInvitationOperation createBoxMemberInvitationOperation,
        CountWorkspaceTotalTeamMembersQuery countWorkspaceTotalTeamMembersQuery,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var workspaceMaxTeamMembers = boxContext.Workspace.MaxTeamMembers;

        if (workspaceMaxTeamMembers is not null)
        {
            var currentTeamMembers = countWorkspaceTotalTeamMembersQuery.Execute(
                workspaceId: boxContext.Workspace.Id);

            if (currentTeamMembers.TotalCount + request.MemberEmails.Count > workspaceMaxTeamMembers)
            {
                return HttpErrors.Workspace.MaxTeamMembersExceeded(
                    boxContext.Workspace.ExternalId);
            }
        }

        var result = await createBoxMemberInvitationOperation.Execute(
            inviter: httpContext.GetUserContext(),
            memberEmails: request
                .MemberEmails
                .Select(email => new Email(email)),
            box: boxContext,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        return TypedResults.Ok(new CreateBoxInvitationResponseDto
        {
            Members = result
                .Members!
                .Select(m => new CreateBoxInvitationResponseDto.BoxInvitationMember(
                    m.Email.Value,
                    ExternalId: m.ExternalId))
                .ToList()
        });
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> RevokeMember(
        [FromRoute] BoxExtId boxExternalId,
        [FromRoute] UserExtId memberExternalId,
        HttpContext httpContext,
        BoxMembershipCache boxMembershipCache,
        RevokeBoxMemberQuery revokeBoxMemberQuery,
        CancellationToken cancellationToken)
    {
        var boxMembership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId: boxExternalId,
            memberExternalId: memberExternalId,
            cancellationToken: cancellationToken);

        if (boxMembership is null)
            return HttpErrors.Box.MemberNotFound(boxExternalId, memberExternalId);

        var result = await revokeBoxMemberQuery.Execute(
            boxMembership: boxMembership,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (result)
        {
            case RevokeBoxMemberQuery.ResultCode.Ok:
                await boxMembershipCache.InvalidateEntry(boxMembership, cancellationToken);
                return TypedResults.Ok();

            case RevokeBoxMemberQuery.ResultCode.MembershipNotFound:
                return HttpErrors.Box.MemberNotFound(boxExternalId, memberExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(RevokeBoxMemberQuery),
                    resultValueStr: result.ToString());
        }
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateMemberPermissions(
        [FromRoute] BoxExtId boxExternalId,
        [FromRoute] UserExtId memberExternalId,
        [FromBody] UpdateBoxMemberPermissionsRequestDto request,
        HttpContext httpContext,
        BoxMembershipCache boxMembershipCache,
        UpdateBoxMemberPermissionsQuery updateBoxMemberPermissionsQuery,
        CancellationToken cancellationToken)
    {
        var boxMembership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId: boxExternalId,
            memberExternalId: memberExternalId,
            cancellationToken: cancellationToken);

        if (boxMembership is null)
            return HttpErrors.Box.MemberNotFound(boxExternalId, memberExternalId);

        await updateBoxMemberPermissionsQuery.Execute(
            boxMembership: boxMembership,
            permissions: new BoxPermissions(
                AllowList: request.AllowList,
                AllowUpload: request.AllowUpload,
                AllowDownload: request.AllowDownload,
                AllowDeleteFile: request.AllowDeleteFile,
                AllowRenameFile: request.AllowRenameFile,
                AllowMoveItems: request.AllowMoveItems,
                AllowCreateFolder: request.AllowCreateFolder,
                AllowRenameFolder: request.AllowRenameFolder,
                AllowDeleteFolder: request.AllowDeleteFolder),
            cancellationToken: cancellationToken);

        await boxMembershipCache.InvalidateEntry(
            boxMembership,
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<CreateBoxLinkResponseDto>, NotFound<HttpError>>> CreateBoxLink(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] CreateBoxLinkRequestDto request,
        HttpContext httpContext,
        CreateBoxLinkQuery createBoxLinkQuery,
        CancellationToken cancellationToken)
    {
        var result = await createBoxLinkQuery.Execute(
            box: httpContext.GetBoxContext(),
            name: request.Name,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            CreateBoxLinkQuery.ResultCode.Ok => TypedResults.Ok(new CreateBoxLinkResponseDto(
                ExternalId: result.BoxLink.ExternalId,
                AccessCode: result.BoxLink.AccessCode)),

            CreateBoxLinkQuery.ResultCode.BoxNotFound => HttpErrors.Box.NotFound(boxExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateBoxLinkQuery),
                resultValueStr: result.Code.ToString())
        };
    }
}