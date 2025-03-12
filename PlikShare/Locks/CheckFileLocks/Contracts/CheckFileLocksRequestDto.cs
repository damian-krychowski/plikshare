namespace PlikShare.Locks.CheckFileLocks.Contracts;

public class CheckFileLocksRequestDto
{
    public required List<string> ExternalIds { get; init; }
}