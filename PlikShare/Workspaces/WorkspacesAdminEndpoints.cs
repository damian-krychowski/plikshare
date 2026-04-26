using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Users.Cache;
using PlikShare.Users.Middleware;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.ChangeOwner;
using PlikShare.Workspaces.ChangeOwner.Contracts;
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