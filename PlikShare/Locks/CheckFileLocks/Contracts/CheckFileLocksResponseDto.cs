namespace PlikShare.Locks.CheckFileLocks.Contracts;

public record CheckFileLocksResponseDto(
    List<string> LockedExternalIds);