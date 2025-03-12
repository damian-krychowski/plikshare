namespace PlikShare.Boxes.Delete.QueueJob;

public record DeleteBoxesQueueJobDefinition(
    List<int> BoxIds, 
    int WorkspaceId);