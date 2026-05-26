namespace PlikShare.Core.Configuration;

public interface IConfig
{
    int QueueProcessingBatchSize { get; }

    string AppUrl { get; }

    bool ForcePasswordLoginEnabled { get; }

    /// <summary>
    /// Optional override for the ffmpeg binary location. When set, FfmpegService probes
    /// and invokes this exact path; when null or empty, the service falls back to "ffmpeg"
    /// (resolved through the process PATH). Configure via <c>Ffmpeg:Path</c>.
    /// </summary>
    string? FfmpegPath { get; }
}