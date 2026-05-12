using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog.Policy.Queries;

/// <summary>
/// Lists all non-deleted workspaces alongside a summary of their audit-log policy: how many
/// events are disabled, how many have a severity override. Backs the "Per-workspace" tab in the
/// policy editor — admin sees the whole inventory in one shot and can drill into any workspace.
/// The policy JSON is parsed in-process (cheap, sparse) so this stays a single indexed scan over
/// <c>w_workspaces</c>.
/// </summary>
public class GetWorkspacesWithAuditLogPolicyQuery(PlikShareDb plikShareDb)
{
    public record Result(IReadOnlyList<Row> Workspaces);

    public record Row(
        WorkspaceExtId ExternalId,
        string Name,
        UserExtId OwnerExternalId,
        string OwnerEmail,
        int DisabledCount,
        int SeverityOverrideCount);

    public Result Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT
                         w.w_external_id,
                         w.w_name,
                         w.w_audit_log_disabled_events_json,
                         owner.u_external_id,
                         owner.u_email
                     FROM w_workspaces AS w
                     INNER JOIN u_users AS owner
                         ON owner.u_id = w.w_owner_id
                     WHERE w.w_is_being_deleted = FALSE
                     ORDER BY w.w_name COLLATE NOCASE ASC
                     """,
                readRowFunc: reader =>
                {
                    var policy = AuditLogPolicy.Parse(reader.GetStringOrNull(2));

                    return new Row(
                        ExternalId: reader.GetExtId<WorkspaceExtId>(0),
                        Name: reader.GetString(1),
                        OwnerExternalId: reader.GetExtId<UserExtId>(3),
                        OwnerEmail: reader.GetString(4),
                        DisabledCount: policy.DisabledEventTypes.Count,
                        SeverityOverrideCount: policy.SeverityOverrides.Count);
                })
            .Execute();

        return new Result(rows);
    }
}
