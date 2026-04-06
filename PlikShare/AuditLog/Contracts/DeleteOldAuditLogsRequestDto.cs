namespace PlikShare.AuditLog.Contracts;

public class DeleteOldAuditLogsRequestDto
{
    public required string OlderThanDate { get; init; }
}

public class DeleteOldAuditLogsResponseDto
{
    public required int DeletedCount { get; init; }
}
