using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlikShare.AuditLog.Policy;

/// <summary>
/// A policy that controls audit-log event recording. Stored as JSON in three places:
/// <list type="bullet">
///   <item>app-level setting <c>audit-log-app-policy</c> — controls application-scoped events</item>
///   <item>app-level setting <c>audit-log-workspace-default-policy</c> — the template snapshotted
///         into <c>w_workspaces.w_audit_log_disabled_events_json</c> when a new workspace is created</item>
///   <item><c>w_workspaces.w_audit_log_disabled_events_json</c> — the live policy for that workspace</item>
/// </list>
/// JSON shape: <c>{"disabled":["event.type", ...], "severities":{"event.type":"warning", ...}}</c>.
/// Both fields are sparse:
/// <list type="bullet">
///   <item>An event type not in <c>disabled</c> is enabled.</item>
///   <item>An event type not in <c>severities</c> uses the natural severity stamped by the factory
///         method (the one in <c>AuditLogEventCatalog</c>).</item>
/// </list>
/// Unknown event types and unknown severity values are tolerated (no effect on any real event),
/// so partial UI rollouts and stale entries don't crash the evaluator.
/// </summary>
[ImmutableObject(true)]
public sealed record AuditLogPolicy
{
    public IReadOnlySet<string> DisabledEventTypes { get; }

    /// <summary>
    /// Sparse map of event-type → forced severity. When an event with this event-type is logged,
    /// the entry's <c>Severity</c> is replaced with this value. Values must be one of
    /// <see cref="AuditLogSeverities"/>; invalid values are dropped during parse.
    /// </summary>
    public IReadOnlyDictionary<string, string> SeverityOverrides { get; }

    public AuditLogPolicy(
        IEnumerable<string> disabledEventTypes,
        IEnumerable<KeyValuePair<string, string>>? severityOverrides = null)
    {
        DisabledEventTypes = disabledEventTypes.ToHashSet();
        SeverityOverrides = severityOverrides is null
            ? new Dictionary<string, string>()
            : severityOverrides.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static readonly AuditLogPolicy Empty = new([]);

    public bool IsEnabled(string eventType) => !DisabledEventTypes.Contains(eventType);

    public string? GetSeverityOverride(string eventType) =>
        SeverityOverrides.TryGetValue(eventType, out var severity) ? severity : null;

    public string Serialize()
    {
        var shape = new PolicyJsonShape(
            Disabled: DisabledEventTypes.ToArray(),
            Severities: SeverityOverrides.Count == 0
                ? null
                : SeverityOverrides.ToDictionary(kv => kv.Key, kv => kv.Value));

        return JsonSerializer.Serialize(shape);
    }

    public static AuditLogPolicy Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Empty;

        try
        {
            var shape = JsonSerializer.Deserialize<PolicyJsonShape>(json);
            if (shape is null)
                return Empty;

            var disabled = shape.Disabled ?? [];

            var severities = shape.Severities is null
                ? []
                : shape.Severities
                    .Where(kv => AuditLogSeverities.IsValid(kv.Value))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (disabled.Length == 0 && severities.Count == 0)
                return Empty;

            return new AuditLogPolicy(disabled, severities);
        }
        catch (JsonException)
        {
            // Fail-open: if the stored policy is corrupt, log everything rather than dropping
            // events silently. The fix is a re-save from the UI.
            return Empty;
        }
    }

    private sealed record PolicyJsonShape(
        [property: JsonPropertyName("disabled")] string[]? Disabled,
        [property: JsonPropertyName("severities")] Dictionary<string, string>? Severities);
}
