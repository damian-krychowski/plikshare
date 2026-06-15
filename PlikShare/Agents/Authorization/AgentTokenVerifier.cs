using PlikShare.Agents.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Agents.Authorization;

public class AgentTokenVerifier(
    PlikShareDb plikShareDb,
    AgentTokenService agentTokenService,
    IClock clock)
{
    public VerifiedAgent? TryVerify(
        string token)
    {
        var tokenHash = agentTokenService.Hash(token);

        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         a_external_id,
                         at_expires_at,
                         at_revoked_at
                     FROM at_agent_tokens
                     INNER JOIN a_agents
                         ON a_id = at_agent_id
                     WHERE at_token_hash = $tokenHash
                         AND a_is_enabled = TRUE
                     LIMIT 1
                     """,
                readRowFunc: reader => new TokenRow(
                    AgentExternalId: reader.GetExtId<AgentExtId>(0),
                    ExpiresAt: reader.GetDateTimeOffsetOrNull(1),
                    RevokedAt: reader.GetDateTimeOffsetOrNull(2)))
            .WithParameter("$tokenHash", tokenHash)
            .Execute();

        if (result.IsEmpty)
            return null;

        var row = result.Value;

        if (row.RevokedAt is not null)
            return null;

        if (row.ExpiresAt is not null && row.ExpiresAt.Value <= clock.UtcNow)
            return null;

        return new VerifiedAgent(
            ExternalId: row.AgentExternalId);
    }

    private readonly record struct TokenRow(
        AgentExtId AgentExternalId,
        DateTimeOffset? ExpiresAt,
        DateTimeOffset? RevokedAt);
}

public record VerifiedAgent(
    AgentExtId ExternalId);
