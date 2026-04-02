namespace PlikShare.Core.Configuration;

public class AppConfig : IConfig
{
    public AppConfig(IConfiguration configuration)
    {
        QueueProcessingBatchSize = int.Parse(
            configuration.GetSection("Queue").GetSection("ProcessingBatchSize").Value ??
            throw new InvalidOperationException("Config for 'Queue.ProcessingBatchSize' not found."));

        AppUrl = configuration.GetValue<string>("AppUrl") ??
                 throw new InvalidOperationException("Config for 'AppUrl' not found.");

        ForcePasswordLoginEnabled = configuration.GetValue<bool>("ForcePasswordLoginEnabled");
    }

    public int QueueProcessingBatchSize { get; }

    public string AppUrl { get; }

    public bool ForcePasswordLoginEnabled { get; }
}