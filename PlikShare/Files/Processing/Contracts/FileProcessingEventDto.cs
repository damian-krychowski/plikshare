using PlikShare.Core.Queue;
using PlikShare.Files.Id;

namespace PlikShare.Files.Processing.Contracts;

public class FileProcessingEventDto
{
    public required Dictionary<QueueJobType, HashSet<FileExtId>> Processing { get; init; }
    public required Dictionary<QueueJobType, HashSet<FileExtId>> ProcessingFinished { get; init; }
}
