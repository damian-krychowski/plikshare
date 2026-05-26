using System.Diagnostics;
using PlikShare.Core.Configuration;
using PlikShare.Files.Metadata;
using Serilog;

namespace PlikShare.Files.Thumbnails.Generation;

/// <summary>
/// Single-source-of-truth for everything PlikShare does with ffmpeg: detection at startup
/// (which binary, is it available, what version) AND every actual subprocess invocation
/// (thumbnail generation today, future video extraction etc.).
///
/// <para>Resolution: if <c>IConfig.FfmpegPath</c> is set, that exact path is probed/used.
/// Otherwise the service falls back to the literal "ffmpeg" command, which the OS resolves
/// through the process PATH — covers Docker images that <c>COPY --from</c> a static ffmpeg
/// into <c>/usr/local/bin</c> and developer machines with ffmpeg installed normally.</para>
/// </summary>
public class FfmpegService
{
    private const string DefaultExecutable = "ffmpeg";

    private static readonly Serilog.ILogger Logger = Log.ForContext<FfmpegService>();

    private readonly string _ffmpegPath;

    public bool IsAvailable { get; }
    public string? VersionLine { get; }

    public FfmpegService(IConfig config)
    {
        _ffmpegPath = config.FfmpegPath ?? DefaultExecutable;

        var (isAvailable, version) = Probe(_ffmpegPath);

        IsAvailable = isAvailable;
        VersionLine = version;

        if (isAvailable)
        {
            Logger.Information(
                "ffmpeg detected at '{Path}' — thumbnail auto-generation enabled. {VersionLine}",
                _ffmpegPath,
                version);
        }
        else
        {
            Logger.Information(
                "ffmpeg not found at '{Path}' — thumbnail auto-generation disabled.",
                _ffmpegPath);
        }
    }

    /// <summary>
    /// Drives a single ffmpeg invocation to resize an image (or extract a video's first frame)
    /// into a WebP thumbnail at the variant's target dimension. Source bytes are written to
    /// ffmpeg stdin, output bytes are read from stdout, stderr is buffered for error reporting.
    /// </summary>
    public async Task<byte[]> GenerateThumbnail(
        ReadOnlyMemory<byte> sourceBytes,
        ThumbnailVariant variant,
        CancellationToken cancellationToken)
    {
        var targetPixelSize = GetTargetPixelSize(variant);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            // -vframes 1 = take a single frame (image or first frame of video).
            // scale=N:N:force_original_aspect_ratio=decrease = fit within NxN preserving aspect ratio.
            // -f webp -y pipe:1 = write WebP to stdout, overwrite (irrelevant for pipe, harmless).
            Arguments = $"-hide_banner -loglevel error -i pipe:0 -vframes 1 " +
                        $"-vf \"scale={targetPixelSize}:{targetPixelSize}:force_original_aspect_ratio=decrease\" " +
                        $"-f webp -y pipe:1",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg failed to start.");

        // stdin writer, stdout reader, stderr reader must run concurrently — ffmpeg will block
        // if the OS pipe buffer for stderr fills while we're sequentially waiting on stdout.
        var stdinTask = WriteToStdin(process, sourceBytes, cancellationToken);
        var stdoutBuffer = new MemoryStream();
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuffer, cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await Task.WhenAll(stdinTask, stdoutTask);
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw;
        }

        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg exited with code {process.ExitCode}: {stderr}");
        }

        if (stdoutBuffer.Length == 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg produced empty output. stderr: {stderr}");
        }

        Logger.Debug(
            "ffmpeg produced {OutputBytes} bytes WebP at target {Size}px (input {InputBytes} bytes).",
            stdoutBuffer.Length,
            targetPixelSize,
            sourceBytes.Length);

        return stdoutBuffer.ToArray();
    }

    private static int GetTargetPixelSize(ThumbnailVariant variant) => variant switch
    {
        ThumbnailVariant.Small => 400,
        ThumbnailVariant.Large => 1600,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
    };

    private static async Task WriteToStdin(
        Process process,
        ReadOnlyMemory<byte> sourceBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(
                sourceBytes, 
                cancellationToken);

            await process.StandardInput.BaseStream.FlushAsync(
                cancellationToken);
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static (bool IsAvailable, string? VersionLine) Probe(string ffmpegPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);

            if (process is null)
                return (false, null);

            var output = process.StandardOutput.ReadToEnd();

            if (!process.WaitForExit(milliseconds: 5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
                Logger.Warning("ffmpeg probe timed out after 5s — treating as unavailable.");
                return (false, null);
            }

            if (process.ExitCode != 0)
                return (false, null);

            // ffmpeg -version output starts with: "ffmpeg version <ver> Copyright (c) <years> ..."
            var firstLine = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();

            return (true, firstLine);
        }
        catch (Exception ex)
        {
            Logger.Debug(
                ex,
                "ffmpeg probe failed.");

            return (false, null);
        }
    }
}
