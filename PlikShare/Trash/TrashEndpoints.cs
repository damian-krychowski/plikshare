using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Queries;
using PlikShare.Core.Authorization;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Trash.DeleteForever;
using PlikShare.Trash.DeleteForever.Contracts;
using PlikShare.Trash.Empty;
using PlikShare.Trash.List;
using PlikShare.Trash.List.Contracts;
using PlikShare.Trash.Restore;
using PlikShare.Trash.Restore.Contracts;
using PlikShare.Workspaces.Validation;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Trash;

public static class TrashEndpoints
{
    public static void MapTrashEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/trash")
            .WithTags("Trash")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        group.MapGet("/", GetTrashItems)
            .WithName("GetTrashItems");

        group.MapPost("/restore", RestoreFromTrash)
            .WithName("RestoreFromTrash");

        group.MapPost("/items/delete-forever", DeleteForever)
            .WithName("DeleteForever");

        group.MapPost("/empty", EmptyTrash)
            .WithName("EmptyTrash");
    }

    private static GetTrashItemsResponseDto GetTrashItems(
        HttpContext httpContext,
        GetTrashItemsQuery getTrashItemsQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        return getTrashItemsQuery.Execute(
            workspace: workspaceMembership.Workspace,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession());
    }

    private static async Task<RestoreFromTrashResponseDto> RestoreFromTrash(
        [FromBody] RestoreFromTrashRequestDto request,
        HttpContext httpContext,
        RestoreFromTrashQuery restoreFromTrashQuery,
        GetFileAuditContextQuery getFileAuditContextQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await restoreFromTrashQuery.Execute(
            workspace: workspaceMembership.Workspace,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            request: request,
            workspaceEncryptionSession: httpContext.TryGetWorkspaceEncryptionSession(),
            cancellationToken: cancellationToken);

        var restoredFileExternalIds = result.Results
            .Where(r => r.Status == RestoreStatus.Restored)
            .Select(r => r.FileExternalId)
            .ToList();

        if (restoredFileExternalIds.Count > 0)
        {
            // Read the file details after the restore — the files are live again, so their
            // folder path reflects where they were restored to.
            var files = getFileAuditContextQuery
                .ExecuteMany(restoredFileExternalIds)
                .Values
                .ToList();

            await auditLogService.LogWithStorageContext(
                storageExternalId: workspaceMembership.Workspace.Storage.ExternalId,
                buildEntry: storageRef => Audit.Trash.ItemsRestoredEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    storage: storageRef,
                    workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                    files: files),
                cancellationToken);
        }

        return result;
    }

    private static async Task<Results<Ok<DeleteForeverResponseDto>, BadRequest<HttpError>>> DeleteForever(
        [FromBody] DeleteForeverRequestDto request,
        HttpContext httpContext,
        DeleteForeverQuery deleteForeverQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        // Delete-forever is owner/admin-only — same reasoning as workspace delete (it removes
        // user data irreversibly). Regular members can restore but not permanently destroy.
        if (workspaceMembership is { IsOwnedByUser: false, User.HasAdminRole: false })
            return HttpErrors.Workspace.NotOwner(workspaceMembership.Workspace.ExternalId);

        var result = await deleteForeverQuery.Execute(
            workspace: workspaceMembership.Workspace,
            fileExternalIds: request.FileExternalIds,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Files.Count > 0)
        {
            await auditLogService.LogWithStorageContext(
                storageExternalId: workspaceMembership.Workspace.Storage.ExternalId,
                buildEntry: storageRef => Audit.Trash.ItemsPermanentlyDeletedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    storage: storageRef,
                    workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                    files: result.Files),
                cancellationToken);
        }

        return TypedResults.Ok(new DeleteForeverResponseDto
        {
            DeletedCount = result.DeletedCount,
            NewWorkspaceSizeInBytes = result.NewWorkspaceSizeInBytes
        });
    }

    private static async Task<Results<Ok<DeleteForeverResponseDto>, BadRequest<HttpError>>> EmptyTrash(
        HttpContext httpContext,
        EmptyTrashQuery emptyTrashQuery,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        // Empty-trash is owner/admin-only — bulk irreversible action on workspace data.
        if (workspaceMembership is { IsOwnedByUser: false, User.HasAdminRole: false })
            return HttpErrors.Workspace.NotOwner(workspaceMembership.Workspace.ExternalId);

        var result = await emptyTrashQuery.Execute(
            workspace: workspaceMembership.Workspace,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        if (result.Files.Count > 0)
        {
            await auditLogService.LogWithStorageContext(
                storageExternalId: workspaceMembership.Workspace.Storage.ExternalId,
                buildEntry: storageRef => Audit.Trash.EmptiedEntry(
                    actor: httpContext.GetAuditLogActorContext(),
                    storage: storageRef,
                    workspace: workspaceMembership.Workspace.ToAuditLogWorkspaceRef(),
                    files: result.Files),
                cancellationToken);
        }

        return TypedResults.Ok(new DeleteForeverResponseDto
        {
            DeletedCount = result.DeletedCount,
            NewWorkspaceSizeInBytes = result.NewWorkspaceSizeInBytes
        });
    }
}
