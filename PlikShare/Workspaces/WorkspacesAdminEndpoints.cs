using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Utils;
using PlikShare.Users.Cache;
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

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateWorkspaceOwner(
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

        if (newOwner.ExternalId == workspaceMembership.Workspace.Owner.ExternalId)
            return TypedResults.Ok();

        await changeWorkspaceOwnerQuery.Execute(
            workspace: workspaceMembership.Workspace,
            newOwner: newOwner,
            cancellationToken: cancellationToken);

        await workspaceCache.InvalidateEntry(
            workspaceMembership.Workspace.ExternalId,
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