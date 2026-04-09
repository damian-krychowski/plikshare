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

        var items = connection
            .Cmd(
                sql: """
                    SELECT
                        al_id,
                        al_external_id,
                        al_created_at,
                        al_actor_email,
                        al_actor_identity,
                        al_event_type,
                        al_event_severity
                    FROM al_audit_logs
                    WHERE ($cursor IS NULL OR al_id < $cursor)
                      AND ($eventCategories IS NULL OR al_event_category IN (SELECT value FROM json_each($eventCategories)))
                      AND ($eventTypes IS NULL OR al_event_type IN (SELECT value FROM json_each($eventTypes)))
                      AND ($excludeEventTypes IS NULL OR al_event_type NOT IN (SELECT value FROM json_each($excludeEventTypes)))
                      AND ($severities IS NULL OR al_event_severity IN (SELECT value FROM json_each($severities)))
                      AND ($fromDate IS NULL OR al_created_at >= $fromDate)
                      AND ($toDate IS NULL OR al_created_at <= $toDate)
                      AND ($actorIdentities IS NULL OR al_actor_email IN (SELECT value FROM json_each($actorIdentities)))
                      AND ($correlationId IS NULL OR al_correlation_id = $correlationId)
                      AND ($workspaceExternalId IS NULL OR al_workspace_external_id = $workspaceExternalId)
                      AND ($search IS NULL OR al_actor_email LIKE '%' || $search || '%')
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
                        ActorEmail = reader.GetStringOrNull(3),
                        ActorIdentity = reader.GetString(4),
                        EventType = reader.GetString(5),
                        EventSeverity = reader.GetString(6)
                    }
                })
            .WithParameter("$cursor", request.Cursor)
            .WithJsonParameter("$eventCategories", request.EventCategories)
            .WithJsonParameter("$eventTypes", request.EventTypes)
            .WithJsonParameter("$excludeEventTypes", request.ExcludeEventTypes)
            .WithJsonParameter("$severities", request.Severities)
            .WithParameter("$fromDate", request.FromDate)
            .WithParameter("$toDate", request.ToDate)
            .WithJsonParameter("$actorIdentities", request.ActorIdentities)
            .WithParameter("$correlationId", request.CorrelationId)
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
}
