namespace PlikShare.Core.Configuration;

public class AppConfig : IConfig
{
    public AppConfig(IConfiguration configuration)
    {
        QueueProcessingBatchSize = int.Parse(
            configuration.GetSection("Queue").GetSection("ProcessingBatchSize").Value ??
            throw new InvalidOperationException("Config for 'Queue.ProcessingBatchSize' not found."));

        var extremelyLowPriority = configuration
            .GetSection("Queue")
            .GetSection("ExtremelyLowPriority");

        ExtremelyLowPriorityIdleGracePeriod = TimeSpan.FromSeconds(
            extremelyLowPriority.GetValue<double?>("IdleGracePeriodSeconds") ?? 10);

        ExtremelyLowPriorityMaxWait = TimeSpan.FromSeconds(
            extremelyLowPriority.GetValue<double?>("MaxWaitSeconds") ?? 300);

        AppUrl = configuration.GetValue<string>("AppUrl") ??
                 throw new InvalidOperationException("Config for 'AppUrl' not found.");

        ForcePasswordLoginEnabled = configuration.GetValue<bool>("ForcePasswordLoginEnabled");

        var ffmpegPath = configuration.GetSection("Ffmpeg").GetSection("Path").Value;
        FfmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? null : ffmpegPath.Trim();
    }

    public int QueueProcessingBatchSize { get; }

    public TimeSpan ExtremelyLowPriorityIdleGracePeriod { get; }

    public TimeSpan ExtremelyLowPriorityMaxWait { get; }

    public string AppUrl { get; }

    public bool ForcePasswordLoginEnabled { get; }

    public string? FfmpegPath { get; }
}