namespace PlikShare.Storages.FileCopying;

public class CopyFileQueueOnCompletedActionDefinition
{
    public CopyFileQueueOnCompletedActionDefinition()
    {
        
    }

    public required string HandlerType { get; init; }
    public string? ActionHandlerDefinition { get; init; } = default;
}