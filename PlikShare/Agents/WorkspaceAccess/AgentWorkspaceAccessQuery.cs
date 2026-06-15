using Microsoft.Data.Sqlite;
using PlikShare.Agents.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Agents.WorkspaceAccess;

public class AgentWorkspaceAccessQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<Result> Grant(
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => GrantOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                workspaceExternalId: workspaceExternalId),
            cancellationToken: cancellationToken);
    }

    public Task<Result> Revoke(
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => RevokeOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                workspaceExternalId: workspaceExternalId),
            cancellationToken: cancellationToken);
    }

    private Result GrantOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (code, targets) = ResolveTargets(
                dbWriteContext,
                transaction,
                agentExternalId,
                workspaceExternalId);

            if (code != ResultCode.Ok)
            {
                transaction.Rollback();
                return new Result(Code: code);
            }

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         INSERT INTO wa_workspace_agents (
                             wa_workspace_id,
                             wa_agent_id,
                             wa_created_at
                         ) VALUES (
                             $workspaceId,
                             $agentId,
                             $now
                         )
                         ON CONFLICT (wa_workspace_id, wa_agent_id) DO NOTHING
                         """,
                    transaction: transaction)
                .WithParameter("$workspaceId", targets.WorkspaceId)
                .WithParameter("$agentId", targets.AgentId)
                .WithParameter("$now", clock.UtcNow)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId}' was granted access to Workspace '{WorkspaceExternalId}'.",
                agentExternalId,
                workspaceExternalId);

            return new Result(
                Code: ResultCode.Ok,
                AgentName: targets.AgentName,
                WorkspaceName: targets.WorkspaceName);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while granting Agent '{AgentExternalId}' access to Workspace '{WorkspaceExternalId}'.",
                agentExternalId,
                workspaceExternalId);

            throw;
        }
    }

    private Result RevokeOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (code, targets) = ResolveTargets(
                dbWriteContext,
                transaction,
                agentExternalId,
                workspaceExternalId);

            if (code != ResultCode.Ok)
            {
                transaction.Rollback();
                return new Result(Code: code);
            }

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         DELETE FROM wa_workspace_agents
                         WHERE wa_workspace_id = $workspaceId
                             AND wa_agent_id = $agentId
                         """,
                    transaction: transaction)
                .WithParameter("$workspaceId", targets.WorkspaceId)
                .WithParameter("$agentId", targets.AgentId)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId}' access to Workspace '{WorkspaceExternalId}' was revoked.",
                agentExternalId,
                workspaceExternalId);

            return new Result(
                Code: ResultCode.Ok,
                AgentName: targets.AgentName,
                WorkspaceName: targets.WorkspaceName);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while revoking Agent '{AgentExternalId}' access to Workspace '{WorkspaceExternalId}'.",
                agentExternalId,
                workspaceExternalId);

            throw;
        }
    }

    private static (ResultCode Code, Targets Targets) ResolveTargets(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        AgentExtId agentExternalId,
        WorkspaceExtId workspaceExternalId)
    {
        var agent = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT
                         a_id,
                         a_name
                     FROM a_agents
                     WHERE a_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => new AgentRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$externalId", agentExternalId.Value)
            .Execute();

        if (agent.IsEmpty)
            return (ResultCode.AgentNotFound, default);

        var workspace = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT
                         w_id,
                         w_name
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                         AND w_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => new WorkspaceRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$externalId", workspaceExternalId.Value)
            .Execute();

        if (workspace.IsEmpty)
            return (ResultCode.WorkspaceNotFound, default);

        return (ResultCode.Ok, new Targets(
            AgentId: agent.Value.Id,
            AgentName: agent.Value.Name,
            WorkspaceId: workspace.Value.Id,
            WorkspaceName: workspace.Value.Name));
    }

    private readonly record struct AgentRow(
        int Id,
        string Name);

    private readonly record struct WorkspaceRow(
        int Id,
        string Name);

    private readonly record struct Targets(
        int AgentId,
        string AgentName,
        int WorkspaceId,
        string WorkspaceName);

    public readonly record struct Result(
        ResultCode Code,
        string? AgentName = null,
        string? WorkspaceName = null);

    public enum ResultCode
    {
        Ok = 0,
        AgentNotFound,
        WorkspaceNotFound
    }
}
