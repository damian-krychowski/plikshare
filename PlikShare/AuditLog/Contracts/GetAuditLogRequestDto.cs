namespace PlikShare.AuditLog.Contracts;

public class GetAuditLogRequestDto
{
    public int? Cursor { get; init; }
    public int PageSize { get; init; } = 50;
    public string? FromDate { get; init; }
    public string? ToDate { get; init; }
    public List<string>? EventCategories { get; init; }
    public List<string>? EventTypes { get; init; }
    public List<string>? Severities { get; init; }
    public List<string>? ActorIdentities { get; init; }
    public string? ResourceType { get; init; }
    public string? WorkspaceExternalId { get; init; }
    public string? Search { get; init; }
}
