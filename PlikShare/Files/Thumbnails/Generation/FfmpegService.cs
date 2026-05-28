using System.Diagnostics;
using System.IO.Pipelines;
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
    /// Generates all requested WebP thumbnail variants from a single source stream. The source is
    /// read ONCE (eg. straight from S3 via <paramref name="writeSourceTo"/>) and fanned out to one
    /// ffmpeg process per variant over stdin — nothing is buffered in the managed heap or on disk,
    /// and the source is downloaded only once. Each variant succeeds or fails independently; a
    /// failure is reported in its <see cref="ThumbnailOutput.Error"/> rather than thrown, so one
    /// bad variant never sinks the others.
    /// </summary>
    public async Task<List<ThumbnailOutput>> GenerateThumbnails(
        Func<PipeWriter, CancellationToken, ValueTask> writeSourceTo,
        IReadOnlyList<ThumbnailVariant> variants,
        CancellationToken cancellationToken)
    {
        var workers = variants
            .Select(variant => StartWorker(variant, cancellationToken))
            .ToList();

        try
        {
            // Read the source once into a pipe; broadcast every chunk to all still-alive stdins.
            var sourcePipe = new Pipe();

            var produceTask = ProduceSource(
                writeSourceTo, 
                sourcePipe.Writer, 
                cancellationToken);

            var pumpTask = PumpToWorkers(
                sourcePipe.Reader, 
                workers, 
                cancellationToken);

            await Task.WhenAll(
                produceTask, 
                pumpTask);

            var outputs = new List<ThumbnailOutput>(
                workers.Count);

            foreach (var worker in workers)
            {
                var output = await CollectWorker(
                    worker,
                    cancellationToken);

                outputs.Add(output);
            }

            return outputs;
        }
        catch
        {
            // Source read (or pump) blew up — infra failure; tear down every process and bubble up
            // so the queue can retry the whole job. The failure happens before any output is
            // handed to the caller, so we still own every stdout buffer here.
            foreach (var worker in workers)
                TryKill(worker.Process);

            foreach (var worker in workers)
            {
                // Let the (now-killed) stdout copy finish before disposing the buffer it writes to,
                // so we never dispose underneath an in-flight CopyToAsync.
                try { await worker.StdoutTask; } catch { /* already faulted / cancelled */ }

                worker.StdoutBuffer.Dispose();
            }

            throw;
        }
        finally
        {
            foreach (var worker in workers)
            {
                worker.Process.Dispose();
            }
        }
    }

    private Worker StartWorker(
        ThumbnailVariant variant, 
        CancellationToken cancellationToken)
    {
        var targetPixelSize = GetTargetPixelSize(variant);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // -i pipe:0 = read source from stdin (fed by the shared pump).
        // -vframes 1 = single frame (image or first frame of video).
        // scale=N:N:force_original_aspect_ratio=decrease = fit within NxN preserving aspect ratio.
        // -f webp -y pipe:1 = write WebP to stdout.
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("pipe:0");
        psi.ArgumentList.Add("-vframes");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add($"scale={targetPixelSize}:{targetPixelSize}:force_original_aspect_ratio=decrease");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("webp");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("pipe:1");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg failed to start.");

        //purposefully not disposed in here - it will be disposed by the consumer of this method
        var stdoutBuffer = new MemoryStream();

        // Drain stdout/stderr immediately and concurrently — if either OS pipe buffer fills while
        // we're feeding stdin, ffmpeg blocks and the whole fan-out deadlocks.
        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(
            stdoutBuffer,
            cancellationToken);

        var stderrTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        return new Worker
        {
            Variant = variant,
            Process = process,
            StdoutBuffer = stdoutBuffer,
            StdoutTask = stdoutTask,
            StderrTask = stderrTask
        };
    }

    private static async Task ProduceSource(
        Func<PipeWriter, CancellationToken, ValueTask> writeSourceTo,
        PipeWriter writer,
        CancellationToken cancellationToken)
    {
        try
        {
            await writeSourceTo(
                writer, 
                cancellationToken);

            await writer.CompleteAsync();
        }
        catch (Exception ex)
        {
            await writer.CompleteAsync(ex);
            throw;
        }
    }

    private static async Task PumpToWorkers(
        PipeReader reader,
        List<Worker> workers,
        CancellationToken cancellationToken)
    {
        var writes = new List<Task>(workers.Count);

        try
        {
            while (true)
            {
                var result = await reader.ReadAsync(
                    cancellationToken);

                var buffer = result.Buffer;

                foreach (var segment in buffer)
                {
                    writes.Clear();

                    foreach (var worker in workers)
                    {
                        if (worker.StdinAlive)
                        {
                            var writeTask = WriteSegment(
                                worker,
                                segment,
                                cancellationToken);

                            writes.Add(writeTask);
                        }
                    }

                    if (writes.Count > 0)
                        await Task.WhenAll(writes);
                }

                reader.AdvanceTo(buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        finally
        {
            await reader.CompleteAsync();

            // EOF (or abort) — close every stdin so each ffmpeg finishes.
            foreach (var worker in workers)
            {
                try { worker.Process.StandardInput.Close(); } catch { /* already gone */ }
            }
        }
    }

    private static async Task WriteSegment(
        Worker worker,
        ReadOnlyMemory<byte> segment,
        CancellationToken cancellationToken)
    {
        try
        {
            await worker.Process.StandardInput.BaseStream.WriteAsync(
                segment, 
                cancellationToken);
        }
        catch
        {
            // Broken pipe — this ffmpeg exited (eg. it already has the frame it needs, or errored).
            // Stop feeding it; CollectWorker decides success/failure from its exit code + output.
            worker.StdinAlive = false;
        }
    }

    private static async Task<ThumbnailOutput> CollectWorker(
        Worker worker,
        CancellationToken cancellationToken)
    {
        try
        {
            await worker.StdoutTask;
            await worker.Process.WaitForExitAsync(cancellationToken);

            var stderr = await worker.StderrTask;

            if (worker.Process.ExitCode != 0)
            {
                worker.StdoutBuffer.Dispose();
                return new ThumbnailOutput(
                    worker.Variant,
                    null,
                    $"ffmpeg exited with code {worker.Process.ExitCode}: {stderr}");
            }

            if (worker.StdoutBuffer.Length == 0)
            {
                worker.StdoutBuffer.Dispose();
                return new ThumbnailOutput(
                    worker.Variant,
                    null,
                    $"ffmpeg produced empty output. stderr: {stderr}");
            }

            // Hand the buffer to the caller as the upload source. Rewind, since CopyToAsync left
            // the position at the end. Ownership (and disposal) transfers with the returned output.
            worker.StdoutBuffer.Position = 0;

            return new ThumbnailOutput(
                worker.Variant,
                worker.StdoutBuffer,
                Error: null);
        }
        catch (Exception ex)
        {
            TryKill(worker.Process);
            worker.StdoutBuffer.Dispose();
            return new ThumbnailOutput(worker.Variant, null, ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            /* best effort */
        }
    }

    private sealed class Worker
    {
        public required ThumbnailVariant Variant { get; init; }
        public required Process Process { get; init; }
        public required MemoryStream StdoutBuffer { get; init; } //should not be disposed as its lifecycle is manages outside of this file
        public required Task StdoutTask { get; init; }
        public required Task<string> StderrTask { get; init; }
        public bool StdinAlive { get; set; } = true;
    }

    public sealed record ThumbnailOutput(
        ThumbnailVariant Variant,
        MemoryStream? DataStream,
        string? Error): IDisposable, IAsyncDisposable
    {
        public void Dispose()
        {
            DataStream?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (DataStream != null) 
                await DataStream.DisposeAsync();
        }
    }

    private static int GetTargetPixelSize(ThumbnailVariant variant) => variant switch
    {
        ThumbnailVariant.Mini => 128,
        ThumbnailVariant.Small => 400,
        ThumbnailVariant.Large => 1600,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null)
    };

    /// <summary>
    /// Re-encodes the source image to the requested format at its full resolution, streaming the
    /// result straight into <paramref name="destination"/> (eg. the HTTP response body) — neither
    /// the source nor the output is buffered. Quality settings are hard-coded to sensible defaults
    /// (JPEG ≈ q4 / Q85, WebP 85, PNG lossless max compression); no per-request quality knob to
    /// keep the menu simple.
    ///
    /// <para>Because the output is streamed, a failure that surfaces after the first bytes have
    /// been written can't unwind an already-committed response — it throws and the connection is
    /// aborted. Validation that the source is convertible happens upstream before headers are set.</para>
    /// </summary>
    public async Task ConvertImage(
        Func<PipeWriter, CancellationToken, ValueTask> writeSourceTo,
        DownloadImageFormat targetFormat,
        PipeWriter destination,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // -i pipe:0 = read source from stdin (streamed, never buffered).
        // -vframes 1 = single frame (handles video sources too — extracts first frame).
        // No scale filter — preserve original resolution; this is "convert" not "thumbnail".
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("pipe:0");
        psi.ArgumentList.Add("-vframes");
        psi.ArgumentList.Add("1");
        AddFormatArgs(psi.ArgumentList, targetFormat);
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("pipe:1");

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg failed to start.");

        // Stream source into stdin and ffmpeg's stdout straight into the destination, draining
        // stderr concurrently — anything less and a full OS pipe buffer deadlocks ffmpeg.
        var sourcePipe = new Pipe();

        var produceTask = ProduceSource(
            writeSourceTo,
            sourcePipe.Writer,
            cancellationToken);

        var stdinTask = CopyToStdin(
            sourcePipe.Reader,
            process,
            cancellationToken);

        // PipeReader -> PipeWriter copy: ffmpeg's stdout flows straight into the destination
        // writer (eg. the response body) without an intermediate Stream adapter or buffer.
        var stdoutReader = PipeReader.Create(process.StandardOutput.BaseStream);
        var stdoutTask = stdoutReader.CopyToAsync(
            destination,
            cancellationToken);

        var stderrTask = process.StandardError.ReadToEndAsync(
            cancellationToken);

        try
        {
            await Task.WhenAll(
                produceTask,
                stdinTask,
                stdoutTask);

            await process.WaitForExitAsync(
                cancellationToken);
        }
        catch
        {
            TryKill(process);
            throw;
        }

        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg convert exited with code {process.ExitCode}: {stderr}");
        }
    }

    private static void AddFormatArgs(IList<string> args, DownloadImageFormat targetFormat)
    {
        switch (targetFormat)
        {
            // mjpeg muxer streams a single JPEG frame to stdout; q:v 4 ≈ visual Q 85
            case DownloadImageFormat.Jpeg:
                args.Add("-c:v"); args.Add("mjpeg");
                args.Add("-q:v"); args.Add("4");
                args.Add("-f"); args.Add("image2pipe");
                break;

            // PNG over image2pipe so ffmpeg emits raw PNG bytes (not a sequence); max zlib level
            case DownloadImageFormat.Png:
                args.Add("-c:v"); args.Add("png");
                args.Add("-compression_level"); args.Add("9");
                args.Add("-f"); args.Add("image2pipe");
                break;

            // WebP muxer handles single-image output natively
            case DownloadImageFormat.Webp:
                args.Add("-quality"); args.Add("85");
                args.Add("-f"); args.Add("webp");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(targetFormat), targetFormat, null);
        }
    }

    private static async Task CopyToStdin(
        PipeReader reader,
        Process process,
        CancellationToken cancellationToken)
    {
        try
        {
            await reader.CopyToAsync(
                process.StandardInput.BaseStream, 
                cancellationToken);

            await process.StandardInput.BaseStream.FlushAsync(
                cancellationToken);
        }
        finally
        {
            try { process.StandardInput.Close(); } catch { /* already gone */ }
            await reader.CompleteAsync();
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
