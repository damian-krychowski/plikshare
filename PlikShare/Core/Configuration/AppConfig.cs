using PlikShare.Core.SQLite;

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

        var dbWriteLanes = configuration
            .GetSection("Queue")
            .GetSection("DbWriteLaneMaxWaitMs");

        DbWritePriorityMaxWaits =
        [
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(dbWriteLanes.GetValue<double?>("JobExtremelyHigh") ?? 50),
            TimeSpan.FromMilliseconds(dbWriteLanes.GetValue<double?>("JobHigh") ?? 200),
            TimeSpan.FromMilliseconds(dbWriteLanes.GetValue<double?>("JobNormal") ?? 1_000),
            TimeSpan.FromMilliseconds(dbWriteLanes.GetValue<double?>("JobLow") ?? 5_000),
            TimeSpan.FromMilliseconds(dbWriteLanes.GetValue<double?>("JobExtremelyLow") ?? 30_000),
        ];

        AppUrl = configuration.GetValue<string>("AppUrl") ??
                 throw new InvalidOperationException("Config for 'AppUrl' not found.");

        ForcePasswordLoginEnabled = configuration.GetValue<bool>("ForcePasswordLoginEnabled");

        var ffmpegPath = configuration.GetSection("Ffmpeg").GetSection("Path").Value;
        FfmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? null : ffmpegPath.Trim();
    }

    public int QueueProcessingBatchSize { get; }

    public IReadOnlyList<TimeSpan> DbWritePriorityMaxWaits { get; }

    public TimeSpan ExtremelyLowPriorityIdleGracePeriod { get; }

    public TimeSpan ExtremelyLowPriorityMaxWait { get; }

    public string AppUrl { get; }

    public bool ForcePasswordLoginEnabled { get; }

    public string? FfmpegPath { get; }
}