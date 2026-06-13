using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog.Contracts;
using PlikShare.AuditLog.Decryption;
using PlikShare.AuditLog.Details;
using PlikShare.AuditLog.Id;
using PlikShare.AuditLog.Queries;
using PlikShare.Core.Authorization;
using PlikShare.GeneralSettings;
using PlikShare.Core.Protobuf;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog;

public static class AuditLogEndpoints
{
    public static void MapAuditLogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/audit-log")
            .WithTags("AuditLog")
            .RequireAuthorization(new AuthorizeAttribute
            {
                Policy = AuthPolicy.Internal,
                Roles = Roles.Admin
            })
            .AddEndpointFilter(new RequireAdminPermissionEndpointFilter(Permissions.ManageAuditLog));

        group.MapPost("/", GetAuditLog)
            .WithName("GetAuditLog")
            .WithProtobufResponse();

        group.MapGet("/stats", GetAuditLogStats)
            .WithName("GetAuditLogStats");

        group.MapPost("/max-size", UpdateMaxSize)
            .WithName("UpdateAuditLogMaxSize");

        group.MapPost("/compact", CompactDatabase)
            .WithName("CompactAuditLog");

        group.MapGet("/filter-options", GetFilterOptions)
            .WithName("GetAuditLogFilterOptions");

        group.MapGet("/{externalId}", GetAuditLogEntryDetails)
            .WithName("GetAuditLogEntryDetails");

        group.MapPost("/delete-old", DeleteOldAuditLogs)
            .WithName("DeleteOldAuditLogs");

        group.MapPost("/archive", ArchiveAuditLogs)
            .WithName("ArchiveAuditLogs");
    }

    private static GetAuditLogResponseDto GetAuditLog(
        [FromBody] GetAuditLogRequestDto request,
        GetAuditLogQuery getAuditLogQuery)
    {
        return getAuditLogQuery.Execute(request);
    }

    private static AuditLogStatsResponseDto GetAuditLogStats(
        GetAuditLogStatsQuery getAuditLogStatsQuery)
    {
        return getAuditLogStatsQuery.Execute();
    }

    private static AuditLogFilterOptionsDto GetFilterOptions(
        GetAuditLogFilterOptionsQuery getAuditLogFilterOptionsQuery)
    {
        return getAuditLogFilterOptionsQuery.Execute();
    }

    private static async Task<IResult> UpdateMaxSize(
        [FromBody] UpdateAuditLogMaxSizeRequestDto request,
        AppSettings appSettings,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        appSettings.SetAuditLogMaxSizeInBytes(
            request.MaxSizeInBytes);

        await auditLogService.Log(
            Audit.Settings.AuditLogMaxSizeChangedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                value: appSettings.AuditLogMaxSizeInBytes),
            cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<IResult> CompactDatabase(
        CompactAuditLogQuery compactAuditLogQuery,
        HttpContext httpContext,
        AuditLogService auditLogService,
        CancellationToken cancellationToken)
    {
        var result = compactAuditLogQuery.Execute();

        await auditLogService.Log(
            Audit.Settings.AuditLogCompactedEntry(
                actor: httpContext.GetAuditLogActorContext(),
                deletedCount: result.DeletedCount,
                dbSizeInBytes: result.DbSizeInBytes),
            cancellationToken);

        return TypedResults.Ok(result);
    }

    private static async Task<IResult> GetAuditLogEntryDetails(
        AuditLogExtId externalId,
        HttpContext httpContext,
        GetAuditLogEntryDetailsQuery getAuditLogEntryDetailsQuery,
        AuditLogDetailsDecryptor auditLogDetailsDecryptor,
        WorkspaceCache workspaceCache,
        CancellationToken cancellationToken)
    {
        var result = getAuditLogEntryDetailsQuery.Execute(
            externalId);

        if (result is null)
            return HttpErrors.AuditLog.NotFound(externalId);

        if (result.Details is null || result.WorkspaceExternalId is null)
            return TypedResults.Ok(result);

        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceExternalId: WorkspaceExtId.Parse(
                result.WorkspaceExternalId),
            cancellationToken: cancellationToken);

        if(workspace?.Storage.Encryption is not FullStorageEncryption)
            return TypedResults.Ok(result);

        var (code, session) = await httpContext.TryStartWorkspaceEncryptionSession(
            workspace);

        if (code == StartWorkspaceEncryptionSessionResultCode.UserEncryptionSessionRequired)
            return HttpErrors.Storage.UserEncryptionSessionRequired();

        using (session)
        {
            var decryptedDetails = auditLogDetailsDecryptor.Decrypt(
                detailsJson: result.Details,
                entryWorkspaceExternalId: WorkspaceExtId.Parse(result.WorkspaceExternalId),
                workspaceEncryptionSession: session);

            return TypedResults.Ok(result with
            {
                Details = decryptedDetails
            });
        }
    }

    private static DeleteOldAuditLogsResponseDto DeleteOldAuditLogs(
        [FromBody] DeleteOldAuditLogsRequestDto request,
        DeleteOldAuditLogsQuery deleteOldAuditLogsQuery)
    {
        var deletedCount = deleteOldAuditLogsQuery.Execute(
            olderThanDate: request.OlderThanDate);

        return new DeleteOldAuditLogsResponseDto
        {
            DeletedCount = deletedCount
        };
    }

    private static ArchiveAuditLogsResponseDto ArchiveAuditLogs(
        [FromBody] ArchiveAuditLogsRequestDto request,
        ArchiveAuditLogsQuery archiveAuditLogsQuery)
    {
        return archiveAuditLogsQuery.Execute(
            olderThanDate: request.OlderThanDate);
    }
}
