using PlikShare.Agents.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Agents.Delete;

public class DeleteAgentQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        AgentExtId externalId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId externalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
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
                    readRowFunc: reader => new DeletedAgent(
                        Id: reader.GetInt32(0),
                        Name: reader.GetString(1)),
                    transaction: transaction)
                .WithParameter("$externalId", externalId.Value)
                .Execute();

            if (agent.IsEmpty)
            {
                transaction.Rollback();

                return new Result(
                    Code: ResultCode.NotFound);
            }

            var agentId = agent.Value.Id;

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: "DELETE FROM ba_box_agents WHERE ba_agent_id = $agentId",
                    transaction: transaction)
                .WithParameter("$agentId", agentId)
                .Execute();

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: "DELETE FROM wa_workspace_agents WHERE wa_agent_id = $agentId",
                    transaction: transaction)
                .WithParameter("$agentId", agentId)
                .Execute();

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: "DELETE FROM at_agent_tokens WHERE at_agent_id = $agentId",
                    transaction: transaction)
                .WithParameter("$agentId", agentId)
                .Execute();

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: "DELETE FROM a_agents WHERE a_id = $agentId",
                    transaction: transaction)
                .WithParameter("$agentId", agentId)
                .Execute();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId} ({AgentId})' was deleted.",
                externalId,
                agentId);

            return new Result(
                Code: ResultCode.Ok,
                Name: agent.Value.Name);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while deleting Agent '{AgentExternalId}'.",
                externalId);

            throw;
        }
    }

    private readonly record struct DeletedAgent(
        int Id,
        string Name);

    public readonly record struct Result(
        ResultCode Code,
        string? Name = null);

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
