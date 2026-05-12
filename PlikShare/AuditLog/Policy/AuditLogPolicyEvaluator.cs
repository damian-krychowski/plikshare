using PlikShare.GeneralSettings;

namespace PlikShare.AuditLog.Policy;

/// <summary>
/// Decides whether a given <see cref="AuditLogEntry"/> should be persisted, and which severity
/// it should carry. Consults the app-level policy (<see cref="AppSettings.AuditLogAppPolicy"/>)
/// for application-scoped events, or the per-workspace policy via
/// <see cref="WorkspaceAuditLogPolicyCache"/> for workspace-scoped events. The split is based on
/// whether the entry carries a <c>WorkspaceExternalId</c> — this matches how the
/// <c>Audit.*Entry</c> factories stamp entries today and avoids classifying events by category
/// at this layer.
/// </summary>
public class AuditLogPolicyEvaluator(
    AppSettings appSettings,
    WorkspaceAuditLogPolicyCache workspacePolicyCache)
{
    /// <summary>
    /// Combined result: whether to persist, and (when persisting) the severity to use. A non-null
    /// <see cref="SeverityOverride"/> replaces the entry's natural severity before the entry
    /// hits the channel; a null override means "keep the natural severity from the factory".
    /// </summary>
    public readonly record struct EvaluationResult(bool ShouldLog, string? SeverityOverride);

    public async ValueTask<EvaluationResult> Evaluate(
        AuditLogEntry entry,
        CancellationToken cancellationToken)
    {
        var policy = entry.WorkspaceExternalId is not null
            ? await workspacePolicyCache.Get(entry.WorkspaceExternalId, cancellationToken)
            : appSettings.AuditLogAppPolicy;

        if (!policy.IsEnabled(entry.EventType))
            return new EvaluationResult(ShouldLog: false, SeverityOverride: null);

        return new EvaluationResult(
            ShouldLog: true,
            SeverityOverride: policy.GetSeverityOverride(entry.EventType));
    }
}
