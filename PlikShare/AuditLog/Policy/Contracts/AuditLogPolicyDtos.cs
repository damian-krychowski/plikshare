namespace PlikShare.AuditLog.Policy.Contracts;

/// <summary>
/// Static catalog returned from <c>GET /api/audit-log/policy/catalog</c>. Used by the frontend
/// to render the policy editor — we ship every known event type with its category, severity, and
/// human description so the UI can group, badge, and explain without round-tripping to the
/// backend per event.
/// </summary>
public record AuditLogEventCatalogDto
{
    public required List<AuditLogEventCatalogEntryDto> Events { get; init; }
}

public record AuditLogEventCatalogEntryDto
{
    public required string EventType { get; init; }
    public required string Category { get; init; }
    public required string Severity { get; init; }
    public required string Description { get; init; }
    /// <summary>Drives which policy controls this event — see <see cref="AuditLogEventScope"/>.</summary>
    public required AuditLogEventScope Scope { get; init; }
}

/// <summary>
/// The policy payload — sparse list of event types that are disabled plus a sparse map of severity
/// overrides. Anything not in <c>DisabledEventTypes</c> is enabled. Anything not in
/// <c>SeverityOverrides</c> uses the natural severity from <c>AuditLogEventCatalog</c>. Sent and
/// received by every policy GET/PUT.
/// </summary>
public record AuditLogPolicyDto
{
    public required List<string> DisabledEventTypes { get; init; }

    /// <summary>
    /// Map of event-type → severity. Values must be one of <c>verbose</c>, <c>info</c>,
    /// <c>warning</c>, <c>critical</c>; the endpoint rejects others with 400. Sparse — entries
    /// equal to the natural severity are still accepted (they have no effect, but the UI clears
    /// them out before sending).
    /// </summary>
    public Dictionary<string, string>? SeverityOverrides { get; init; }
}

/// <summary>
/// Returned from <c>GET /api/audit-log/policy/volume-stats</c>. Maps event-type → count of audit
/// log rows in the requested window. Drives the "X events / 30d" badge next to each toggle.
/// When <c>workspaceExternalId</c> is supplied the counts are scoped to that workspace; otherwise
/// they are global. Event types with zero events in the window are omitted.
/// </summary>
public record AuditLogVolumeStatsDto
{
    public required int DaysWindow { get; init; }
    public required string? WorkspaceExternalId { get; init; }
    public required Dictionary<string, int> CountsByEventType { get; init; }
}

/// <summary>
/// Returned from <c>GET /api/audit-log/policy/workspaces/{externalId}</c>. Wraps the policy with
/// the workspace's display name so the editor can show a friendly title.
/// </summary>
public record GetWorkspacePolicyResponseDto
{
    public required string WorkspaceExternalId { get; init; }
    public required string WorkspaceName { get; init; }
    public required List<string> DisabledEventTypes { get; init; }
    public Dictionary<string, string>? SeverityOverrides { get; init; }
}

/// <summary>
/// Returned from <c>GET /api/audit-log/policy/workspaces</c>. The whole admin-visible inventory
/// of workspaces with each one's policy summary — the frontend filters by name client-side and
/// sorts customized workspaces to the top.
/// </summary>
public record AuditLogPolicyWorkspacesDto
{
    public required List<AuditLogPolicyWorkspaceItemDto> Workspaces { get; init; }
}

public record AuditLogPolicyWorkspaceItemDto
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string OwnerExternalId { get; init; }
    public required string OwnerEmail { get; init; }
    public required int DisabledCount { get; init; }
    public required int SeverityOverrideCount { get; init; }
}
