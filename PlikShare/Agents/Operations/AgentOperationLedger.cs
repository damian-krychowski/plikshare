using PlikShare.Agents.Operations.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Operations;

/// <summary>
/// The ledger of agent operations that needed human approval. An operation is created as
/// <c>pending</c> when an approval-gated tool is invoked, moves to <c>approved</c>/<c>denied</c>
/// when a human resolves it, and to <c>executed</c>/<c>failed</c> when the agent commits it.
/// </summary>
public class AgentOperationLedger(
    DbWriteQueue dbWriteQueue,
    PlikShareDb plikShareDb,
    IClock clock)
{
    public Task<AgentOperationExtId> CreatePending(
        int agentId,
        int? workspaceId,
        string toolName,
        string paramsJson,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        var externalId = AgentOperationExtId.NewId();

        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                context.Connection
                    .NonQueryCmd(
                        sql: """
                             INSERT INTO aop_agent_operations (
                                 aop_external_id,
                                 aop_agent_id,
                                 aop_workspace_id,
                                 aop_tool_name,
                                 aop_params_json,
                                 aop_status,
                                 aop_created_at,
                                 aop_expires_at
                             ) VALUES (
                                 $externalId,
                                 $agentId,
                                 $workspaceId,
                                 $toolName,
                                 $paramsJson,
                                 $status,
                                 $createdAt,
                                 $expiresAt
                             )
                             """)
                    .WithParameter("$externalId", externalId.Value)
                    .WithParameter("$agentId", agentId)
                    .WithParameter("$workspaceId", workspaceId)
                    .WithParameter("$toolName", toolName)
                    .WithParameter("$paramsJson", paramsJson)
                    .WithParameter("$status", AgentOperationStatuses.Pending)
                    .WithParameter("$createdAt", clock.UtcNow)
                    .WithParameter("$expiresAt", expiresAt)
                    .Execute();

                return externalId;
            },
            cancellationToken: cancellationToken);
    }

    public AgentOperation? GetByExternalId(AgentOperationExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         aop_id,
                         aop_agent_id,
                         aop_tool_name,
                         aop_params_json,
                         aop_status,
                         aop_expires_at,
                         aop_result_json
                     FROM aop_agent_operations
                     WHERE aop_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentOperation(
                    Id: reader.GetInt32(0),
                    ExternalId: externalId,
                    AgentId: reader.GetInt32(1),
                    ToolName: reader.GetString(2),
                    ParamsJson: reader.GetString(3),
                    Status: reader.GetString(4),
                    ExpiresAt: reader.GetFieldValue<DateTimeOffset>(5),
                    ResultJson: reader.GetStringOrNull(6)))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    /// <summary>
    /// The agent's operations that still warrant its attention — pending (awaiting approval),
    /// approved (ready to commit), or recently denied/expired. Executed/failed ones drop off, since
    /// the agent already received their outcome from execute_operation. Newest first.
    /// </summary>
    public List<AgentOperationSummary> ListOutstandingByAgent(int agentId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         aop_external_id,
                         aop_tool_name,
                         aop_status,
                         aop_created_at,
                         aop_expires_at
                     FROM aop_agent_operations
                     WHERE aop_agent_id = $agentId
                       AND aop_status NOT IN ($executed, $failed)
                     ORDER BY aop_id DESC
                     """,
                readRowFunc: reader => new AgentOperationSummary(
                    ExternalId: AgentOperationExtId.Parse(reader.GetString(0)),
                    ToolName: reader.GetString(1),
                    Status: reader.GetString(2),
                    CreatedAt: reader.GetFieldValue<DateTimeOffset>(3),
                    ExpiresAt: reader.GetFieldValue<DateTimeOffset>(4)))
            .WithParameter("$agentId", agentId)
            .WithParameter("$executed", AgentOperationStatuses.Executed)
            .WithParameter("$failed", AgentOperationStatuses.Failed)
            .Execute();
    }

    public Task<ResolveResultCode> Resolve(
        AgentOperationExtId externalId,
        string targetStatus,
        int resolvedByUserId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                var result = context.Connection
                    .OneRowCmd(
                        sql: """
                             UPDATE aop_agent_operations
                             SET aop_status = $targetStatus,
                                 aop_resolved_by_user_id = $userId,
                                 aop_resolved_at = $now
                             WHERE aop_external_id = $externalId
                                 AND aop_status = $pending
                             RETURNING aop_id
                             """,
                        readRowFunc: reader => reader.GetInt32(0))
                    .WithParameter("$targetStatus", targetStatus)
                    .WithParameter("$userId", resolvedByUserId)
                    .WithParameter("$now", clock.UtcNow)
                    .WithParameter("$externalId", externalId.Value)
                    .WithParameter("$pending", AgentOperationStatuses.Pending)
                    .Execute();

                return result.IsEmpty ? ResolveResultCode.NotPending : ResolveResultCode.Ok;
            },
            cancellationToken: cancellationToken);
    }

    public Task Complete(
        AgentOperationExtId externalId,
        string resultJson,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                context.Connection
                    .NonQueryCmd(
                        sql: """
                             UPDATE aop_agent_operations
                             SET aop_status = $executed,
                                 aop_executed_at = $now,
                                 aop_result_json = $result
                             WHERE aop_external_id = $externalId
                             """)
                    .WithParameter("$executed", AgentOperationStatuses.Executed)
                    .WithParameter("$now", clock.UtcNow)
                    .WithParameter("$result", resultJson)
                    .WithParameter("$externalId", externalId.Value)
                    .Execute();

                return true;
            },
            cancellationToken: cancellationToken);
    }

    public Task Fail(
        AgentOperationExtId externalId,
        string errorJson,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                context.Connection
                    .NonQueryCmd(
                        sql: """
                             UPDATE aop_agent_operations
                             SET aop_status = $failed,
                                 aop_executed_at = $now,
                                 aop_result_json = $error
                             WHERE aop_external_id = $externalId
                             """)
                    .WithParameter("$failed", AgentOperationStatuses.Failed)
                    .WithParameter("$now", clock.UtcNow)
                    .WithParameter("$error", errorJson)
                    .WithParameter("$externalId", externalId.Value)
                    .Execute();

                return true;
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Moves every still-pending operation past its expiry into the terminal <c>expired</c> state.
    /// Keeps the stored state consistent with what the commit/status tools compute on the fly,
    /// and gives the purge step a terminal row to eventually delete. Returns the number expired.
    /// </summary>
    public Task<int> ExpirePending(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        // Cheap read first — never occupy the single-writer queue with a no-op UPDATE.
        using (var connection = plikShareDb.OpenConnection())
        {
            var hasExpired = connection
                .OneRowCmd(
                    sql: """
                         SELECT 1 FROM aop_agent_operations
                         WHERE aop_status = $pending
                           AND aop_expires_at < $now
                         LIMIT 1
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$pending", AgentOperationStatuses.Pending)
                .WithParameter("$now", now)
                .Execute();

            if (hasExpired.IsEmpty)
                return Task.FromResult(0);
        }

        return dbWriteQueue.Execute(
            operationToEnqueue: context => context.Connection
                .NonQueryCmd(
                    sql: """
                         UPDATE aop_agent_operations
                         SET aop_status = $expired
                         WHERE aop_status = $pending
                           AND aop_expires_at < $now
                         """)
                .WithParameter("$expired", AgentOperationStatuses.Expired)
                .WithParameter("$pending", AgentOperationStatuses.Pending)
                .WithParameter("$now", now)
                .Execute()
                .AffectedRows,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Permanently deletes resolved operations (<c>expired</c>/<c>denied</c>/<c>executed</c>/<c>failed</c>)
    /// created before the cutoff, bounding how long the ledger — and any stored result payload —
    /// is retained. Pending operations are never touched here. Returns the number deleted.
    /// </summary>
    public Task<int> PurgeResolvedOlderThan(
        DateTimeOffset createdBefore,
        CancellationToken cancellationToken)
    {
        // Cheap read first — never occupy the single-writer queue with a no-op DELETE.
        using (var connection = plikShareDb.OpenConnection())
        {
            var result = connection
                .OneRowCmd(
                    sql: """
                         SELECT 1 FROM aop_agent_operations
                         WHERE aop_status != $pending
                           AND aop_created_at < $createdBefore
                         LIMIT 1
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$pending", AgentOperationStatuses.Pending)
                .WithParameter("$createdBefore", createdBefore)
                .Execute();

            if (result.IsEmpty)
                return Task.FromResult(0);
        }

        return dbWriteQueue.Execute(
            operationToEnqueue: context => context.Connection
                .NonQueryCmd(
                    sql: """
                         DELETE FROM aop_agent_operations
                         WHERE aop_status != $pending
                           AND aop_created_at < $createdBefore
                         """)
                .WithParameter("$pending", AgentOperationStatuses.Pending)
                .WithParameter("$createdBefore", createdBefore)
                .Execute()
                .AffectedRows,
            cancellationToken: cancellationToken);
    }

    public enum ResolveResultCode
    {
        Ok = 0,
        NotPending
    }
}

public sealed record AgentOperation(
    int Id,
    AgentOperationExtId ExternalId,
    int AgentId,
    string ToolName,
    string ParamsJson,
    string Status,
    DateTimeOffset ExpiresAt,
    string? ResultJson);

public sealed record AgentOperationSummary(
    AgentOperationExtId ExternalId,
    string ToolName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
