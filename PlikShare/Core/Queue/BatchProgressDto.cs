namespace PlikShare.Core.Queue;

public class BatchProgressDto
{
    public required int Total { get; init; }
    public required int Completed { get; init; }
    public required int Failed { get; init; }
    public required int Pending { get; init; }
}
