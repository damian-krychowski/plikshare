using PlikShare.Agents.Authorization;
using PlikShare.Agents.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Agents.RotateToken;

public class RotateAgentTokenQuery(
    DbWriteQueue dbWriteQueue,
    AgentTokenService agentTokenService,
    IClock clock)
{
    public Task<Result> Execute(
        AgentExtId agentExternalId,
        CancellationToken cancellationToken)
    {
        var token = agentTokenService.Generate();

        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                agentExternalId: agentExternalId,
                token: token),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        AgentExtId agentExternalId,
        AgentTokenParts token)
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
                    readRowFunc: reader => new AgentRow(
                        Id: reader.GetInt32(0),
                        Name: reader.GetString(1)),
                    transaction: transaction)
                .WithParameter("$externalId", agentExternalId.Value)
                .Execute();

            if (agent.IsEmpty)
            {
                transaction.Rollback();

                return new Result(
                    Code: ResultCode.NotFound);
            }

            var now = clock.UtcNow;

            dbWriteContext.Connection
                .NonQueryCmd(
                    sql: """
                         UPDATE at_agent_tokens
                         SET at_revoked_at = $now
                         WHERE at_agent_id = $agentId
                             AND at_revoked_at IS NULL
                         """,
                    transaction: transaction)
                .WithParameter("$now", now)
                .WithParameter("$agentId", agent.Value.Id)
                .Execute();

            dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO at_agent_tokens (
                             at_agent_id,
                             at_token_hash,
                             at_token_masked,
                             at_created_at,
                             at_expires_at,
                             at_last_used_at,
                             at_revoked_at
                         ) VALUES (
                             $agentId,
                             $tokenHash,
                             $tokenMasked,
                             $now,
                             NULL,
                             NULL,
                             NULL
                         )
                         RETURNING
                             at_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$agentId", agent.Value.Id)
                .WithParameter("$tokenHash", token.Hash)
                .WithParameter("$tokenMasked", token.Masked)
                .WithParameter("$now", now)
                .ExecuteOrThrow();

            transaction.Commit();

            Log.Information("Token for Agent '{AgentExternalId} ({AgentId})' was rotated.",
                agentExternalId,
                agent.Value.Id);

            return new Result(
                Code: ResultCode.Ok,
                Token: token.Token,
                TokenMasked: token.Masked,
                AgentName: agent.Value.Name);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while rotating token for Agent '{AgentExternalId}'.",
                agentExternalId);

            throw;
        }
    }

    private readonly record struct AgentRow(
        int Id,
        string Name);

    public readonly record struct Result(
        ResultCode Code,
        string? Token = null,
        string? TokenMasked = null,
        string? AgentName = null);

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
