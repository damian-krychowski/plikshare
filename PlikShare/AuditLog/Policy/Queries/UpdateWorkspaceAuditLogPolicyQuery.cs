using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog.Policy.Queries;

/// <summary>
/// Persists a per-workspace audit-log policy to <c>w_workspaces.w_audit_log_disabled_events_json</c>
/// and invalidates the in-memory cache so the next audit-log call picks up the new value.
/// </summary>
public class UpdateWorkspaceAuditLogPolicyQuery(
    DbWriteQueue dbWriteQueue,
    WorkspaceAuditLogPolicyCache policyCache)
{
    public async Task<ResultCode> Execute(
        WorkspaceExtId workspaceExternalId,
        AuditLogPolicy policy,
        CancellationToken cancellationToken)
    {
        var result = await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                context,
                workspaceExternalId,
                policy),
            cancellationToken: cancellationToken);

        if (result == ResultCode.Ok)
        {
            await policyCache.Set(
                workspaceExternalId.Value,
                policy,
                cancellationToken);
        }

        return result;
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext context,
        WorkspaceExtId workspaceExternalId,
        AuditLogPolicy policy)
    {
        var result = context
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_audit_log_disabled_events_json = $json
                     WHERE w_external_id = $externalId
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$json", policy.Serialize())
            .WithParameter("$externalId", workspaceExternalId.Value)
            .Execute();

        return result.IsEmpty
            ? ResultCode.WorkspaceNotFound
            : ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok,
        WorkspaceNotFound
    }
}
