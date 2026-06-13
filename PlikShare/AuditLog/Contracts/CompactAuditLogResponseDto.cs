namespace PlikShare.AuditLog.Contracts;

public class CompactAuditLogResponseDto
{
    public required int DeletedCount { get; init; }
    public required long DbSizeInBytes { get; init; }
}
