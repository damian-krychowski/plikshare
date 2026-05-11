using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.ChangeOwner;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using PlikShare.Workspaces.ListAll;
using PlikShare.Workspaces.ListAll.Contracts;
using PlikShare.Workspaces.Members.AdminAdd;
using PlikShare.Workspaces.Members.AdminAdd.Contracts;
using PlikShare.Workspaces.Members.CountAll;
using PlikShare.Workspaces.UpdateMaxSize;
using PlikShare.Workspaces.UpdateMaxSize.Contracts;
using PlikShare.Workspaces.UpdateMaxTeamMembers;
using PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;
using PlikShare.Workspaces.Validation;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;

namespace PlikShare.Workspaces;

public static class WorkspacesAdminEndpoints
{
    public static void MapWorkspacesAdminEndpoints(this WebApplication app)
    {
        app.MapPatch("/api/workspaces/{workspaceExternalId}/owner", UpdateWorkspaceOwner)
            .WithTags("Workspaces Admin")
            .WithName("UpdateWorkspaceOwner")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        app.MapPatch("/api/workspaces/{workspaceExternalId}/max-size", UpdateWorkspaceMaxSize)
            .WithTags("Workspaces Admin")
            .WithName("UpdateWorkspaceMaxSize")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(
                Core.Authorization.Permissions.ManageUsers))
            .AddEndpointFilter<ValidateWorkspaceFilter>();


        app.MapPatch("/api/workspaces/{workspaceExternalId}/max-team-members", UpdateWorkspaceMaxTeamMembers)
            .WithTags("Workspaces Admin")
            .WithName("UpdateWorkspaceMaxTeamMembers")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(
                Core.Authorization.Permissions.ManageUsers))
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        app.MapGet("/api/workspaces/admin-list-all", ListAllWorkspaces)
            .WithTags("Workspaces Admin")
            .WithName("ListAllWorkspaces")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(
                Core.Authorization.Permissions.ManageUsers));

        app.MapPost("/api/workspaces/{workspaceExternalId}/members/assign", AdminAddMember)
            .WithTags("Workspaces Admin")
            .WithName("AdminAssignWorkspaceMember")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = $"{Roles.Admin}"
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(
                Core.Authorization.Permissions.ManageUsers))
            .AddEndpointFilter<ValidateWorkspaceFilter>();
    }

    private static async Task<Results<Ok<GetAllWorkspacesResponseDto>, NotFound<HttpError>>> ListAllWorkspaces(
        [FromQuery] string? excludeMemberOrOwnerExternalId,
        UserCache userCache,
        ListAllWorkspacesQuery listAllWorkspacesQuery,
        CancellationToken cancellationToken)
    {
        int? excludeUserId = null;

        if (!string.IsNullOrWhiteSpace(excludeMemberOrOwnerExternalId))
        {
            if (!UserExtId.TryParse(excludeMemberOrOwnerExternalId, null, out var userExtId))
                return HttpErrors.User.NotFound(new UserExtId(excludeMemberOrOwnerExternalId));

            var user = await userCache.TryGetUser(
                userExternalId: userExtId,
                cancellationToken: cancellationToken);

            if (user is null)
                return HttpErrors.User.NotFound(userExtId);

            excludeUserId = user.Id;
        }

        var response = listAllWorkspacesQuery.Execute(
            excludeMemberOrOwnerUserId: excludeUserId);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> AdminAddMember(
        [FromBody] AdminAddWorkspaceMemberRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        WorkspaceMembershipCache workspaceMembershipCache,
        AdminAddWorkspaceMemberOperation adminAddWorkspaceMemberOperation,
        CountWorkspaceTotalTeamMembersQuery countWorkspaceTotalTeamMembersQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspace = workspaceMembership.Workspace;

        var target = await userCache.TryGetUser(
            userExternalId: request.MemberExternalId,
            cancellationToken: cancellationToken);

        if (target is null)
            return HttpErrors.User.NotFound(request.MemberExternalId);

        if (target.Id == workspace.Owner.Id)
            return HttpErrors.Workspace.MemberAlreadyAssigned(request.MemberExternalId, workspace.ExternalId);

        var teamMembersLimitError = ValidateTeamMembersLimit(
            workspace: workspace,
            countQuery: countWorkspaceTotalTeamMembersQuery);

        if (teamMembersLimitError is not null)
            return teamMembersLimitError;

        var actor = await httpContext.GetUserContext();

        // Mirrors the ChangeWorkspaceOwnerQuery contract: the admin only needs an unlocked
        // session for full-encryption workspaces; for None/Managed storages the cookie is
        // never touched.
        using var actorPrivateKey = workspace.Storage.Encryption is FullStorageEncryption
            ? UserEncryptionSessionCookie.TryReadPrivateKey(httpContext, actor.ExternalId)
            : null;

        var resultCode = await adminAddWorkspaceMemberOperation.Execute(
            workspace: workspace,
            actor: actor,
            target: target,
            allowShare: request.AllowShare,
            actorPrivateKey: actorPrivateKey,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case AdminAddWorkspaceMemberOperation.ResultCode.Ok:
                break;

            case AdminAddWorkspaceMemberOperation.ResultCode.AlreadyMember:
                return HttpErrors.Workspace.MemberAlreadyAssigned(request.MemberExternalId, workspace.ExternalId);

            case AdminAddWorkspaceMemberOperation.ResultCode.TargetNotRegistered:
                return HttpErrors.User.TargetNotRegistered(request.MemberExternalId);

            case AdminAddWorkspaceMemberOperation.ResultCode.ActorEncryptionSessionRequired:
                return HttpErrors.Storage.UserEncryptionSessionRequired();

            case AdminAddWorkspaceMemberOperation.ResultCode.ActorCannotDecryptWorkspace:
                return HttpErrors.Storage.NotAStorageAdmin(workspace.Storage.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(AdminAddWorkspaceMemberOperation),
                    resultValueStr: resultCode.ToString());
        }

        await workspaceMembershipCache.InvalidateEntry(
            workspaceId: workspace.Id,
            memberId: target.Id,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            userId: target.Id,
            cancellationToken: cancellationToken);

        await auditLogService.LogWithStorageContext(
            storageExternalId: workspace.Storage.ExternalId,
            buildEntry: storageRef => Audit.Workspace.AdminAssignedMemberEntry(
                actor: httpContext.GetAuditLogActorContext(),
                storage: storageRef,
                workspace: workspace.ToAuditLogWorkspaceRef(),
                member: target.ToAuditLogUserRef(),
                allowShare: request.AllowShare),
            cancellationToken);

        return TypedResults.Ok(new AdminAddWorkspaceMemberResponseDto
        {
            Email = target.Email.Value,
            ExternalId = target.ExternalId
        });
    }

    private static BadRequest<HttpError>? ValidateTeamMembersLimit(
        WorkspaceContext workspace,
        CountWorkspaceTotalTeamMembersQuery countQuery)
    {
        var maxTeamMembers = workspace.MaxTeamMembers;

        if (maxTeamMembers is null)
            return null;

        var currentTeamMembers = countQuery.Execute(workspaceId: workspace.Id);

        if (currentTeamMembers.TotalCount + 1 > maxTeamMembers)
            return HttpErrors.Workspace.MaxTeamMembersExceeded(workspace.ExternalId);

        return null;
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateWorkspaceMaxTeamMembers(
        [FromBody] UpdateWorkspaceMaxTeamMembersRequestDto request,
        HttpContext httpContext,
        WorkspaceCache workspaceCache,
        UpdateWorkspaceMaxTeamMembersQuery updateWorkspaceMaxTeamMembersQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await updateWorkspaceMaxTeamMembersQuery.Execute(
            workspace: workspaceMembership.Workspace,
            request: request,
            cancellationToken: cancellationToken);

        if (result == UpdateWorkspaceMaxTeamMembersQuery.ResultCode.NotFound)
            return HttpErrors.Workspace.NotFound(
                workspaceMembership.Workspace.ExternalId);

        await workspaceCache.InvalidateEntry(
            workspaceMembership.Workspace.ExternalId,
            cancellationToken: cancellationToken);

        await auditLogService.LogWithStorageContext(
            storageExternalId: workspaceMembership.Workspace.Storage.ExternalId,
            buildEntry: storageRef => Audit.Workspace.MaxTeamMembersUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                storage: storageRef,
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                value: request.MaxTeamMembers),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateWorkspaceMaxSize(
        [FromBody] UpdateWorkspaceMaxSizeDto request,
        HttpContext httpContext,
        WorkspaceCache workspaceCache,
        UpdateWorkspaceMaxSizeQuery updateWorkspaceMaxSizeQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await updateWorkspaceMaxSizeQuery.Execute(
            workspace: workspaceMembership.Workspace,
            request: request,
            cancellationToken: cancellationToken);

        if (result == UpdateWorkspaceMaxSizeQuery.ResultCode.NotFound)
            return HttpErrors.Workspace.NotFound(
                workspaceMembership.Workspace.ExternalId);

        await workspaceCache.InvalidateEntry(
            workspaceMembership.Workspace.ExternalId,
            cancellationToken: cancellationToken);

        await auditLogService.LogWithStorageContext(
            storageExternalId: workspaceMembership.Workspace.Storage.ExternalId,
            buildEntry: storageRef => Audit.Workspace.MaxSizeUpdatedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                storage: storageRef,
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                value: request.MaxSizeInBytes),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<IResult> UpdateWorkspaceOwner(
        [FromBody] ChangeWorkspaceOwnerRequestDto request,
        HttpContext httpContext,
        UserCache userCache,
        WorkspaceCache workspaceCache,
        ChangeWorkspaceOwnerQuery changeWorkspaceOwnerQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var newOwner = await userCache.TryGetUser(
            request.NewOwnerExternalId,
            cancellationToken: cancellationToken);

        if (newOwner is null)
            return HttpErrors.User.NotFound(request.NewOwnerExternalId);

        var previousOwnerId = workspaceMembership.Workspace.Owner.Id;

        if (newOwner.ExternalId == workspaceMembership.Workspace.Owner.ExternalId)
            return TypedResults.Ok();

        var actor = await httpContext.GetUserContext();

        // Full-encryption transfers re-wrap the workspace DEK to the new owner using the
        // actor's own keys (sek-derivation or wek), so the actor must arrive with an
        // unlocked encryption session. For None/Managed storages the cookie is never read.
        using var actorPrivateKey = workspaceMembership.Workspace.Storage.Encryption is FullStorageEncryption
            ? UserEncryptionSessionCookie.TryReadPrivateKey(httpContext, actor.ExternalId)
            : null;

        var resultCode = await changeWorkspaceOwnerQuery.Execute(
            workspace: workspaceMembership.Workspace,
            newOwner: newOwner,
            actor: actor,
            actorPrivateKey: actorPrivateKey,
            cancellationToken: cancellationToken);

        switch (resultCode)
        {
            case ChangeWorkspaceOwnerQuery.ResultCode.Ok:
                break;

            case ChangeWorkspaceOwnerQuery.ResultCode.TargetEncryptionNotSetUp:
                return HttpErrors.Workspace.MemberEncryptionNotSetUp(newOwner.ExternalId);

            case ChangeWorkspaceOwnerQuery.ResultCode.ActorEncryptionSessionRequired:
                return HttpErrors.Storage.UserEncryptionSessionRequired();

            case ChangeWorkspaceOwnerQuery.ResultCode.ActorCannotDecryptWorkspace:
                return HttpErrors.Storage.NotAStorageAdmin(workspaceMembership.Workspace.Storage.ExternalId);

            default:
                throw new UnexpectedOperationResultException(
                    operationName: nameof(ChangeWorkspaceOwnerQuery),
                    resultValueStr: resultCode.ToString());
        }

        await workspaceCache.InvalidateEntry(
            workspaceMembership.Workspace.ExternalId,
            cancellationToken: cancellationToken);

        // Old owner lost their wek wraps; new owner gained wek wraps and (if applicable)
        // dropped a workspace-membership row. Both UserContext caches must reload before
        // the next encryption-aware request.
        await userCache.InvalidateEntry(
            userId: previousOwnerId,
            cancellationToken: cancellationToken);

        await userCache.InvalidateEntry(
            userId: newOwner.Id,
            cancellationToken: cancellationToken);

        await auditLogService.LogWithStorageContext(
            storageExternalId: workspaceMembership.Workspace.Storage.ExternalId,
            buildEntry: storageRef => Audit.Workspace.OwnerChangedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                storage: storageRef,
                workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                newOwner: newOwner.ToAuditLogUserRef()),
            cancellationToken);

        return TypedResults.Ok();
    }
}