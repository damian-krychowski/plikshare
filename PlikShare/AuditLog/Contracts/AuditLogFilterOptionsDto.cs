namespace PlikShare.AuditLog.Contracts;

public class AuditLogFilterOptionsDto
{
    public required List<string> EventTypes { get; init; }
    public required List<string> Actors { get; init; }
}
