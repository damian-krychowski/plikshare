using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuditLog.Policy;

/// <summary>
/// In-memory cache of per-workspace audit-log policies, keyed by workspace external id. Sits on
/// the audit-log hot path: every <see cref="AuditLogService.Log"/> call for a workspace-scoped
/// event consults this cache. Cache misses do a single indexed lookup against
/// <c>w_workspaces.w_external_id</c>; deleted/unknown workspaces resolve to
/// <see cref="AuditLogPolicy.Empty"/> so we never drop events because of a stale id.
/// Invalidation is manual — the policy-update endpoint must call <see cref="Set"/> after writing
/// the new policy to the database. (Workspace deletion leaves a stale entry behind, but the
/// workspace is gone so no further events will be logged against it.)
/// </summary>
public class WorkspaceAuditLogPolicyCache(
    PlikShareDb plikShareDb,
    HybridCache cache)
{
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string PolicyKey(string workspaceExternalId) =>
        $"workspace-audit-log-policy:extid:{workspaceExternalId}";

    public async ValueTask<AuditLogPolicy> Get(
        string workspaceExternalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbePolicyCache(
            workspaceExternalId,
            cancellationToken);

        if (cached is not null)
            return cached;

        var loaded = LoadFromDatabase(workspaceExternalId) ?? AuditLogPolicy.Empty;

        await Store(
            workspaceExternalId,
            loaded,
            cancellationToken);

        return loaded;
    }

    public ValueTask Set(
        string workspaceExternalId,
        AuditLogPolicy policy,
        CancellationToken cancellationToken)
    {
        return Store(
            workspaceExternalId,
            policy,
            cancellationToken);
    }

    private ValueTask<AuditLogPolicy?> ProbePolicyCache(
        string workspaceExternalId,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<AuditLogPolicy?>(
            key: PolicyKey(workspaceExternalId),
            factory: _ => ValueTask.FromResult<AuditLogPolicy?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private ValueTask Store(
        string workspaceExternalId,
        AuditLogPolicy policy,
        CancellationToken cancellationToken)
    {
        return cache.SetAsync(
            key: PolicyKey(workspaceExternalId),
            value: policy,
            cancellationToken: cancellationToken);
    }

    private AuditLogPolicy? LoadFromDatabase(string workspaceExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT w_audit_log_disabled_events_json
                     FROM w_workspaces
                     WHERE w_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetStringOrNull(0))
            .WithParameter("$externalId", workspaceExternalId)
            .Execute();

        if (result.IsEmpty)
            return null;

        return AuditLogPolicy.Parse(result.Value);
    }
}
