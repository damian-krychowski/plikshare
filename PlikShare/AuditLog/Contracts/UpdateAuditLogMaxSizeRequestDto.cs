namespace PlikShare.AuditLog.Contracts;

public class UpdateAuditLogMaxSizeRequestDto
{
    public long? MaxSizeInBytes { get; init; }
}
