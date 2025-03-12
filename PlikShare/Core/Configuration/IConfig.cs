namespace PlikShare.Core.Configuration;

public interface IConfig
{
    int QueueProcessingBatchSize { get; }
    
    string AppUrl { get; }
}