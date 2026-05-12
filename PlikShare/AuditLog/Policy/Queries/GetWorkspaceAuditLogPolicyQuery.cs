using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog.Policy.Queries;

/// <summary>
/// Reads the per-workspace audit-log policy directly from the database, bundling the workspace's
/// display name so the UI can show "Audit log — Acme Marketing" instead of the cryptic external id.
/// Bypasses the in-memory policy cache so the GET endpoint always returns the canonical persisted
/// value (the cache is an optimisation for the hot logging path, not the source of truth for the UI).
/// </summary>
public class GetWorkspaceAuditLogPolicyQuery(PlikShareDb plikShareDb)
{
    public record Result(bool WorkspaceFound, string WorkspaceName, AuditLogPolicy Policy);

    public Result Execute(WorkspaceExtId workspaceExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var row = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         w_name,
                         w_audit_log_disabled_events_json
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new
                {
                    Name = reader.GetString(0),
                    PolicyJson = reader.GetStringOrNull(1)
                })
            .WithParameter("$externalId", workspaceExternalId.Value)
            .Execute();

        if (row.IsEmpty)
            return new Result(WorkspaceFound: false, WorkspaceName: string.Empty, Policy: AuditLogPolicy.Empty);

        return new Result(
            WorkspaceFound: true,
            WorkspaceName: row.Value.Name,
            Policy: AuditLogPolicy.Parse(row.Value.PolicyJson));
    }
}
