using PlikShare.Agents.Id;
using PlikShare.Boxes.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Agents.BoxAccess;

/// <summary>
/// Invites an agent to a box (or removes it). Membership only — what the agent may do inside the box is
/// governed by the tool layer (global config + per-box overrides), so there are no per-box permission
/// flags to set here.
/// </summary>
public class AgentBoxAccessQuery(
    DbWriteQueue dbWriteQueue,
    IClock clock)
{
    public Task<Result> Grant(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => GrantOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                boxExternalId: boxExternalId),
            cancellationToken: cancellationToken);
    }

    public Task<Result> Revoke(
        AgentExtId agentExternalId,
        BoxExtId boxExternalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => RevokeOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                boxExternalId: boxExternalId),
            cancellationToken: cancellationToken);
    }

    private Result GrantOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (code, targets) = ResolveTargets(
                dbWriteContext,
                transaction,
                agentExternalId,
                boxExternalId);

            if (code != ResultCode.Ok)
            {
                transaction.Rollback();
                return new Result(Code: code);
            }

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         INSERT INTO ba_box_agents (
                             ba_box_id,
                             ba_agent_id,
                             ba_created_at
                         ) VALUES (
                             $boxId,
                             $agentId,
                             $now
                         )
                         ON CONFLICT (ba_box_id, ba_agent_id) DO NOTHING
                         """,
                    transaction: transaction)
                .WithParameter("$boxId", targets.BoxId)
                .WithParameter("$agentId", targets.AgentId)
                .WithParameter("$now", clock.UtcNow)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId}' was invited to Box '{BoxExternalId}'.",
                agentExternalId,
                boxExternalId);

            return new Result(
                Code: ResultCode.Ok,
                AgentName: targets.AgentName,
                BoxName: targets.BoxName);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while inviting Agent '{AgentExternalId}' to Box '{BoxExternalId}'.",
                agentExternalId,
                boxExternalId);

            throw;
        }
    }

    private Result RevokeOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (code, targets) = ResolveTargets(
                dbWriteContext,
                transaction,
                agentExternalId,
                boxExternalId);

            if (code != ResultCode.Ok)
            {
                transaction.Rollback();
                return new Result(Code: code);
            }

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         DELETE FROM ba_box_agents
                         WHERE ba_box_id = $boxId
                             AND ba_agent_id = $agentId
                         """,
                    transaction: transaction)
                .WithParameter("$boxId", targets.BoxId)
                .WithParameter("$agentId", targets.AgentId)
                .Execute();

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         DELETE FROM atbo_agent_tool_box_overrides
                         WHERE atbo_box_id = $boxId
                             AND atbo_agent_id = $agentId
                         """,
                    transaction: transaction)
                .WithParameter("$boxId", targets.BoxId)
                .WithParameter("$agentId", targets.AgentId)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId}' access to Box '{BoxExternalId}' was revoked.",
                agentExternalId,
                boxExternalId);

            return new Result(
                Code: ResultCode.Ok,
                AgentName: targets.AgentName,
                BoxName: targets.BoxName);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while revoking Agent '{AgentExternalId}' access to Box '{BoxExternalId}'.",
                agentExternalId,
                boxExternalId);

            throw;
        }
    }

    private static (ResultCode Code, Targets Targets) ResolveTargets(
        SqliteWriteContext dbWriteContext,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        AgentExtId agentExternalId,
        BoxExtId boxExternalId)
    {
        var agent = dbWriteContext.Connection
            .OneRowCmd(
                sql: "SELECT a_id, a_name FROM a_agents WHERE a_external_id = $externalId LIMIT 1",
                readRowFunc: reader => new AgentRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$externalId", agentExternalId.Value)
            .Execute();

        if (agent.IsEmpty)
            return (ResultCode.AgentNotFound, default);

        var box = dbWriteContext.Connection
            .OneRowCmd(
                sql: """
                     SELECT
                         bo_id,
                         bo_name
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                         AND bo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => new BoxRow(
                    Id: reader.GetInt32(0),
                    Name: reader.GetString(1)),
                transaction: transaction)
            .WithParameter("$externalId", boxExternalId.Value)
            .Execute();

        if (box.IsEmpty)
            return (ResultCode.BoxNotFound, default);

        return (ResultCode.Ok, new Targets(
            AgentId: agent.Value.Id,
            AgentName: agent.Value.Name,
            BoxId: box.Value.Id,
            BoxName: box.Value.Name));
    }

    private readonly record struct AgentRow(
        int Id,
        string Name);

    private readonly record struct BoxRow(
        int Id,
        string Name);

    private readonly record struct Targets(
        int AgentId,
        string AgentName,
        int BoxId,
        string BoxName);

    public readonly record struct Result(
        ResultCode Code,
        string? AgentName = null,
        string? BoxName = null);

    public enum ResultCode
    {
        Ok = 0,
        AgentNotFound,
        BoxNotFound
    }
}
