namespace PlikShare.AuditLog.Contracts;

public class AuditLogStatsResponseDto
{
    public required long DbSizeInBytes { get; init; }
    public required int TotalLogCount { get; init; }
    public string? OldestEntryDate { get; init; }
    public string? NewestEntryDate { get; init; }
}
