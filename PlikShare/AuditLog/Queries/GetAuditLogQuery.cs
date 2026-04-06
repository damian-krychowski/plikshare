using System.Text.Json;
using PlikShare.AuditLog.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Queries;

public class GetAuditLogQuery(PlikShareAuditLogDb plikShareAuditLogDb)
{
    public GetAuditLogResponseDto Execute(GetAuditLogRequestDto request)
    {
        using var connection = plikShareAuditLogDb.OpenConnection();

        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var categoriesJson = ToJsonArrayOrNull(request.EventCategories);
        var eventTypesJson = ToJsonArrayOrNull(request.EventTypes);
        var severitiesJson = ToJsonArrayOrNull(request.Severities);
        var actorIdentitiesJson = ToJsonArrayOrNull(request.ActorIdentities);

        var items = connection
            .Cmd(
                sql: """
                    SELECT
                        al_id,
                        al_external_id,
                        al_created_at,
                        al_correlation_id,
                        al_actor_identity_type,
                        al_actor_identity,
                        al_actor_email,
                        al_actor_ip,
                        al_event_category,
                        al_event_type,
                        al_event_severity,
                        al_resource_type,
                        al_resource_external_id,
                        al_resource_name,
                        al_workspace_external_id,
                        al_details
                    FROM al_audit_logs
                    WHERE ($cursor IS NULL OR al_id < $cursor)
                      AND ($eventCategories IS NULL OR al_event_category IN (SELECT value FROM json_each($eventCategories)))
                      AND ($eventTypes IS NULL OR al_event_type IN (SELECT value FROM json_each($eventTypes)))
                      AND ($severities IS NULL OR al_event_severity IN (SELECT value FROM json_each($severities)))
                      AND ($fromDate IS NULL OR al_created_at >= $fromDate)
                      AND ($toDate IS NULL OR al_created_at <= $toDate)
                      AND ($actorIdentities IS NULL OR al_actor_email IN (SELECT value FROM json_each($actorIdentities)))
                      AND ($resourceType IS NULL OR al_resource_type = $resourceType)
                      AND ($workspaceExternalId IS NULL OR al_workspace_external_id = $workspaceExternalId)
                      AND ($search IS NULL OR al_actor_email LIKE '%' || $search || '%' OR al_resource_name LIKE '%' || $search || '%')
                    ORDER BY al_id DESC
                    LIMIT $pageSize + 1
                    """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    Item = new AuditLogItemDto
                    {
                        ExternalId = reader.GetString(1),
                        CreatedAt = reader.GetString(2),
                        CorrelationId = reader.GetStringOrNull(3),
                        ActorIdentityType = reader.GetString(4),
                        ActorIdentity = reader.GetString(5),
                        ActorEmail = reader.GetStringOrNull(6),
                        ActorIp = reader.GetStringOrNull(7),
                        EventCategory = reader.GetString(8),
                        EventType = reader.GetString(9),
                        EventSeverity = reader.GetString(10),
                        ResourceType = reader.GetStringOrNull(11),
                        ResourceExternalId = reader.GetStringOrNull(12),
                        ResourceName = reader.GetStringOrNull(13),
                        WorkspaceExternalId = reader.GetStringOrNull(14),
                        Details = reader.GetStringOrNull(15)
                    }
                })
            .WithParameter("$cursor", request.Cursor)
            .WithParameter("$eventCategories", categoriesJson)
            .WithParameter("$eventTypes", eventTypesJson)
            .WithParameter("$severities", severitiesJson)
            .WithParameter("$fromDate", request.FromDate)
            .WithParameter("$toDate", request.ToDate)
            .WithParameter("$actorIdentities", actorIdentitiesJson)
            .WithParameter("$resourceType", request.ResourceType)
            .WithParameter("$workspaceExternalId", request.WorkspaceExternalId)
            .WithParameter("$search", request.Search)
            .WithParameter("$pageSize", pageSize)
            .Execute();

        var hasMore = items.Count > pageSize;
        var resultItems = hasMore ? items.Take(pageSize).ToList() : items;

        return new GetAuditLogResponseDto
        {
            Items = resultItems.Select(x => x.Item).ToList(),
            NextCursor = hasMore ? resultItems.Last().Id : null,
            HasMore = hasMore
        };
    }

    private static string? ToJsonArrayOrNull(List<string>? values)
    {
        if (values is null || values.Count == 0)
            return null;

        return JsonSerializer.Serialize(values);
    }
}
