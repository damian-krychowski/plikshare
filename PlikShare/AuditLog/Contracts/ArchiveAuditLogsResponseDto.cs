namespace PlikShare.AuditLog.Contracts;

public class ArchiveAuditLogsRequestDto
{
    public string? OlderThanDate { get; init; }
}

public class ArchiveAuditLogsResponseDto
{
    public required string FileName { get; init; }
    public required int ArchivedCount { get; init; }
}
