using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlikShare.AuditLog.Contracts;
using PlikShare.AuditLog.Queries;
using PlikShare.Core.Authorization;
using PlikShare.Core.Protobuf;

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

        group.MapGet("/filter-options", GetFilterOptions)
            .WithName("GetAuditLogFilterOptions");

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
