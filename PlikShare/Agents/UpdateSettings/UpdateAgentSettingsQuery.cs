using Microsoft.Data.Sqlite;
using PlikShare.Agents.Id;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.StorageAccess;
using Serilog;

namespace PlikShare.Agents.UpdateSettings;

public class UpdateAgentSettingsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> UpdateMaxWorkspaceNumber(
        AgentExtId agentExternalId,
        int? maxWorkspaceNumber,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteSimpleUpdate(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                applyUpdate: (agentId, transaction) => context
                    .OneRowCmd(
                        sql: """
                             UPDATE a_agents
                             SET a_max_workspace_number = $value
                             WHERE a_id = $agentId
                             RETURNING a_id
                             """,
                        readRowFunc: reader => reader.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$value", maxWorkspaceNumber)
                    .WithParameter("$agentId", agentId)
                    .Execute()),
            cancellationToken: cancellationToken);
    }

    public Task<Result> UpdateDefaultMaxWorkspaceSize(
        AgentExtId agentExternalId,
        long? maxSizeInBytes,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteSimpleUpdate(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                applyUpdate: (agentId, transaction) => context
                    .OneRowCmd(
                        sql: """
                             UPDATE a_agents
                             SET a_default_max_workspace_size_in_bytes = $value
                             WHERE a_id = $agentId
                             RETURNING a_id
                             """,
                        readRowFunc: reader => reader.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$value", maxSizeInBytes)
                    .WithParameter("$agentId", agentId)
                    .Execute()),
            cancellationToken: cancellationToken);
    }

    public Task<Result> UpdateDefaultMaxWorkspaceTeamMembers(
        AgentExtId agentExternalId,
        int? maxTeamMembers,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteSimpleUpdate(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                applyUpdate: (agentId, transaction) => context
                    .OneRowCmd(
                        sql: """
                             UPDATE a_agents
                             SET a_default_max_workspace_team_members = $value
                             WHERE a_id = $agentId
                             RETURNING a_id
                             """,
                        readRowFunc: reader => reader.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$value", maxTeamMembers)
                    .WithParameter("$agentId", agentId)
                    .Execute()),
            cancellationToken: cancellationToken);
    }

    public Task<Result> UpdateStorageAccess(
        AgentExtId agentExternalId,
        UserStorageAccessMode mode,
        List<string> storageExternalIds,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => UpdateStorageAccessOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                mode: mode,
                storageExternalIds: storageExternalIds),
            cancellationToken: cancellationToken);
    }

    private static Result ExecuteSimpleUpdate(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        Func<int, SqliteTransaction, SQLiteOneRowCommandResult<int>> applyUpdate)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var agent = ResolveAgent(dbWriteContext, transaction, agentExternalId);

            if (agent is null)
            {
                transaction.Rollback();
                return new Result(ResultCode.NotFound);
            }

            applyUpdate(agent.Value.Id, transaction);

            transaction.Commit();

            return new Result(
                ResultCode.Ok,
                AgentName: agent.Value.Name);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating Agent '{AgentExternalId}' settings.",
                agentExternalId);

            throw;
        }
    }

    private Result UpdateStorageAccessOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        UserStorageAccessMode mode,
        List<string> storageExternalIds)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var agent = ResolveAgent(dbWriteContext, transaction, agentExternalId);

            if (agent is null)
            {
                transaction.Rollback();
                return new Result(ResultCode.NotFound);
            }

            var distinctExternalIds = mode == UserStorageAccessMode.All
                ? new List<string>()
                : storageExternalIds.Distinct().ToList();

            var resolvedIds = new List<int>(distinctExternalIds.Count);
            var unknown = new List<string>();

            foreach (var externalId in distinctExternalIds)
            {
                var storageId = TryResolveStorageId(externalId, dbWriteContext, transaction);

                if (storageId is null)
                    unknown.Add(externalId);
                else
                    resolvedIds.Add(storageId.Value);
            }

            if (unknown.Count > 0)
            {
                transaction.Rollback();
                return new Result(ResultCode.UnknownStorageExternalIds, UnknownExternalIds: unknown);
            }

            dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE a_agents
                         SET a_storage_access_mode = $mode
                         WHERE a_id = $agentId
                         RETURNING a_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithEnumParameter("$mode", mode)
                .WithParameter("$agentId", agent.Value.Id)
                .Execute();

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: "DELETE FROM asa_agent_storage_access WHERE asa_agent_id = $agentId",
                    transaction: transaction)
                .WithParameter("$agentId", agent.Value.Id)
                .Execute();

            foreach (var storageId in resolvedIds)
            {
                dbWriteContext.Connection
                    .NonQueryCmd(
                        sql: """
                             INSERT INTO asa_agent_storage_access (
                                 asa_agent_id,
                                 asa_storage_id
                             ) VALUES (
                                 $agentId,
                                 $storageId
                             )
                             ON CONFLICT (asa_agent_id, asa_storage_id) DO NOTHING
                             """,
                        transaction: transaction)
                    .WithParameter("$agentId", agent.Value.Id)
                    .WithParameter("$storageId", storageId)
                    .Execute();
            }

            transaction.Commit();

            return new Result(
                ResultCode.Ok,
                AgentName: agent.Value.Name);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating storage access for Agent '{AgentExternalId}'.",
                agentExternalId);

            throw;
        }
    }

    private static AgentRow? ResolveAgent(
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        AgentExtId agentExternalId)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT a_id, a_name
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

        return result.IsEmpty ? null : result.Value;
    }

    private static int? TryResolveStorageId(
        string externalId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: "SELECT s_id FROM s_storages WHERE s_external_id = $externalId",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", externalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private readonly record struct AgentRow(
        int Id,
        string Name);

    public record Result(
        ResultCode Code,
        string? AgentName = null,
        List<string>? UnknownExternalIds = null);

    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        UnknownStorageExternalIds
    }
}
