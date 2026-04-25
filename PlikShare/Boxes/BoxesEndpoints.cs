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
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Boxes.Validation;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Members.CountAll;
using PlikShare.Workspaces.Validation;
using Audit = PlikShare.AuditLog.Details.Audit;

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await createBoxQuery.Execute(
            workspace: workspaceMembership.Workspace,
            name: request.Name,
            folderExternalId: request.FolderExternalId,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateBoxQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: request.FolderExternalId,
                    buildEntry: folderRef => Audit.Box.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = result.BoxExternalId,
                            Name = request.Name,
                            Folder = folderRef
                        }),
                    cancellationToken);

                return TypedResults.Ok(new CreateBoxResponseDto(
                    ExternalId: result.BoxExternalId));

            case CreateBoxQuery.ResultCode.FolderWasNotFound:
                return HttpErrors.Folder.NotFound(request.FolderExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateBoxQuery),
                    resultValueStr: result.Code.ToString());
        }
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
            box: httpContext.GetBoxContext(),
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());

        return response;
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateBoxName(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] UpdateBoxNameRequestDto request,
        HttpContext httpContext,
        BoxCache boxCache,
        UpdateBoxNameQuery updateBoxNameQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var result = await updateBoxNameQuery.Execute(
            box: boxContext,
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case UpdateBoxNameQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.NameUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = request.Name,
                            Folder = folderRef
                        }),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var resultCode = await updateBoxHeaderIsEnabledQuery.Execute(
            box: boxContext,
            isHeaderEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxHeaderIsEnabledQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.HeaderIsEnabledUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        },
                        isEnabled: request.IsEnabled),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var resultCode = await updateBoxHeaderQuery.Execute(
            box: boxContext,
            json: request.Json,
            html: request.Html,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxHeaderQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.HeaderUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        },
                        contentJson: request.Json),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var resultCode = await updateBoxFooterIsEnabledQuery.Execute(
            box: boxContext,
            isFooterEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxFooterIsEnabledQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.FooterIsEnabledUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        },
                        isEnabled: request.IsEnabled),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var resultCode = await updateBoxFooterQuery.Execute(
            box: boxContext,
            json: request.Json,
            html: request.Html,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case UpdateBoxFooterQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.FooterUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        },
                        contentJson: request.Json),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var result = await updateBoxFolderQuery.Execute(
            box: boxContext,
            folderExternalId: request.FolderExternalId,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case UpdateBoxFolderQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: request.FolderExternalId,
                    buildEntry: newFolderRef => Audit.Box.FolderUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = newFolderRef
                        },
                        newFolder: newFolderRef),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var result = await updateBoxIsEnabledQuery.Execute(
            box: boxContext,
            isEnabled: request.IsEnabled,
            cancellationToken: cancellationToken);

        switch (result)
        {
            case UpdateBoxIsEnabledQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.IsEnabledUpdatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        },
                        isEnabled: request.IsEnabled),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var resultCode = await scheduleBoxesDeleteQuery.Execute(
            box: boxContext,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case ScheduleBoxesDeleteQuery.ResultCode.Ok:
                await boxCache.InvalidateEntry(boxExternalId, cancellationToken);

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.DeletedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        }),
                    cancellationToken);

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
        AuditLogService auditLogService,
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
            inviter: await httpContext.GetUserContext(),
            memberEmails: request
                .MemberEmails
                .Select(email => new Email(email)),
            box: boxContext,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        await auditLogService.LogWithFolderContext(
            folderExternalId: boxContext.Folder?.ExternalId,
            buildEntry: folderRef => Audit.Box.MemberInvitedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                box: new Audit.BoxRef
                {
                    ExternalId = boxContext.ExternalId,
                    Name = boxContext.Name,
                    Folder = folderRef
                },
                members: result
                    .Members
                    ?.Select(m => m.ToAuditLogUserRef())
                    .ToList() ?? []),
            cancellationToken);

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
        AuditLogService auditLogService,
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

                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxMembership.Box.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.MemberRevokedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxMembership.Box.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxMembership.Box.ExternalId,
                            Name = boxMembership.Box.Name,
                            Folder = folderRef
                        },
                        member: boxMembership.Member.ToAuditLogUserRef()),
                    cancellationToken);

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
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxMembership = await boxMembershipCache.TryGetBoxMembership(
            boxExternalId: boxExternalId,
            memberExternalId: memberExternalId,
            cancellationToken: cancellationToken);

        if (boxMembership is null)
            return HttpErrors.Box.MemberNotFound(boxExternalId, memberExternalId);

        var permissions = new BoxPermissions(
            AllowList: request.AllowList,
            AllowUpload: request.AllowUpload,
            AllowDownload: request.AllowDownload,
            AllowDeleteFile: request.AllowDeleteFile,
            AllowRenameFile: request.AllowRenameFile,
            AllowMoveItems: request.AllowMoveItems,
            AllowCreateFolder: request.AllowCreateFolder,
            AllowRenameFolder: request.AllowRenameFolder,
            AllowDeleteFolder: request.AllowDeleteFolder);

        await updateBoxMemberPermissionsQuery.Execute(
            boxMembership: boxMembership,
            permissions: permissions,
            cancellationToken: cancellationToken);

        await boxMembershipCache.InvalidateEntry(
            boxMembership,
            cancellationToken);

        await auditLogService.LogWithFolderContext(
            folderExternalId: boxMembership.Box.Folder?.ExternalId,
            buildEntry: folderRef => Audit.Box.MemberPermissionsUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspace: boxMembership.Box.Workspace.ToAuditLogWorkspaceRef(),
                box: new Audit.BoxRef
                {
                    ExternalId = boxMembership.Box.ExternalId,
                    Name = boxMembership.Box.Name,
                    Folder = folderRef
                },
                member: boxMembership.Member.ToAuditLogUserRef(),
                permissions: permissions),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok<CreateBoxLinkResponseDto>, NotFound<HttpError>>> CreateBoxLink(
        [FromRoute] BoxExtId boxExternalId,
        [FromBody] CreateBoxLinkRequestDto request,
        HttpContext httpContext,
        CreateBoxLinkQuery createBoxLinkQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var boxContext = httpContext.GetBoxContext();

        var result = await createBoxLinkQuery.Execute(
            box: boxContext,
            name: request.Name,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateBoxLinkQuery.ResultCode.Ok:
                await auditLogService.LogWithFolderContext(
                    folderExternalId: boxContext.Folder?.ExternalId,
                    buildEntry: folderRef => Audit.Box.LinkCreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspace: boxContext.Workspace.ToAuditLogWorkspaceRef(),
                        box: new Audit.BoxRef
                        {
                            ExternalId = boxContext.ExternalId,
                            Name = boxContext.Name,
                            Folder = folderRef
                        },
                        linkExternalId: result.BoxLink.ExternalId,
                        linkName: request.Name),
                    cancellationToken);

                return TypedResults.Ok(new CreateBoxLinkResponseDto(
                    ExternalId: result.BoxLink.ExternalId,
                    AccessCode: result.BoxLink.AccessCode));

            case CreateBoxLinkQuery.ResultCode.BoxNotFound:
                return HttpErrors.Box.NotFound(boxExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(CreateBoxLinkQuery),
                    resultValueStr: result.Code.ToString());
        }
    }
}