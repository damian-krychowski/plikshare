using PlikShare.Agents.Authorization;
using PlikShare.Agents.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Agents.Create;

public class CreateAgentQuery(
    DbWriteQueue dbWriteQueue,
    AgentTokenService agentTokenService,
    IClock clock)
{
    public Task<Result> Execute(
        string name,
        int ownerUserId,
        CancellationToken cancellationToken)
    {
        var token = agentTokenService.Generate();

        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                name: name,
                ownerUserId: ownerUserId,
                token: token),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        string name,
        int ownerUserId,
        AgentTokenParts token)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var externalId = AgentExtId.NewId();
            var now = clock.UtcNow;

            var agentId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO a_agents (
                             a_external_id,
                             a_name,
                             a_is_enabled,
                             a_created_at,
                             a_owner_user_id
                         ) VALUES (
                             $externalId,
                             $name,
                             TRUE,
                             $now,
                             $ownerUserId
                         )
                         RETURNING
                             a_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", externalId.Value)
                .WithParameter("$name", name)
                .WithParameter("$now", now)
                .WithParameter("$ownerUserId", ownerUserId)
                .ExecuteOrThrow();

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
                .WithParameter("$agentId", agentId)
                .WithParameter("$tokenHash", token.Hash)
                .WithParameter("$tokenMasked", token.Masked)
                .WithParameter("$now", now)
                .ExecuteOrThrow();

            transaction.Commit();

            Log.Information("Agent '{AgentExternalId} ({AgentId})' was created.",
                externalId,
                agentId);

            return new Result(
                Code: ResultCode.Ok,
                ExternalId: externalId,
                Token: token.Token,
                TokenMasked: token.Masked);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating Agent.");

            throw;
        }
    }

    public readonly record struct Result(
        ResultCode Code,
        AgentExtId ExternalId = default,
        string? Token = null,
        string? TokenMasked = null);

    public enum ResultCode
    {
        Ok = 0
    }
}
