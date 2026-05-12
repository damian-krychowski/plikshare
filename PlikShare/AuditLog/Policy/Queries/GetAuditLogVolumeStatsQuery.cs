using PlikShare.AuditLog.Policy.Contracts;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Policy.Queries;

/// <summary>
/// Aggregate count of audit-log rows per event type over the last N days, optionally scoped to a
/// single workspace. Backs the "X events / 30d" badge in the policy editor — the badge is what
/// makes 119 toggles tractable (decide on data, not on cryptic names). Not on a hot path:
/// the policy editor opens it on page load and again on refresh.
/// </summary>
public class GetAuditLogVolumeStatsQuery(
    PlikShareAuditLogDb auditLogDb,
    IClock clock)
{
    public AuditLogVolumeStatsDto Execute(
        string? workspaceExternalId,
        int daysWindow)
    {
        if (daysWindow <= 0)
            daysWindow = 30;

        var cutoff = clock.UtcNow.AddDays(-daysWindow);

        using var connection = auditLogDb.OpenConnection();

        var rows = workspaceExternalId is null
            ? connection
                .Cmd(
                    sql: """
                         SELECT al_event_type, COUNT(*)
                         FROM al_audit_logs
                         WHERE al_created_at >= $cutoff
                         GROUP BY al_event_type
                         """,
                    readRowFunc: reader => new EventCountRow(
                        EventType: reader.GetString(0),
                        Count: reader.GetInt32(1)))
                .WithParameter("$cutoff", cutoff)
                .Execute()
            : connection
                .Cmd(
                    sql: """
                         SELECT al_event_type, COUNT(*)
                         FROM al_audit_logs
                         WHERE al_created_at >= $cutoff
                           AND al_workspace_external_id = $workspaceExternalId
                         GROUP BY al_event_type
                         """,
                    readRowFunc: reader => new EventCountRow(
                        EventType: reader.GetString(0),
                        Count: reader.GetInt32(1)))
                .WithParameter("$cutoff", cutoff)
                .WithParameter("$workspaceExternalId", workspaceExternalId)
                .Execute();

        return new AuditLogVolumeStatsDto
        {
            DaysWindow = daysWindow,
            WorkspaceExternalId = workspaceExternalId,
            CountsByEventType = rows.ToDictionary(r => r.EventType, r => r.Count)
        };
    }

    private sealed record EventCountRow(string EventType, int Count);
}
