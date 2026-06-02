using System.Diagnostics;
using System.IO.Pipelines;
using PlikShare.Core.Configuration;
using PlikShare.Files.Metadata;
using Serilog;

namespace PlikShare.MediaProcessing.Generation;

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
    /// failure is reported in <see cref="ThumbnailVariantResult.Error"/> rather than thrown, so one bad
    /// variant never sinks the others.
    /// </summary>
    public async Task<IReadOnlyList<ThumbnailVariantResult>> GenerateThumbnails(
        Func<PipeWriter, CancellationToken, ValueTask> writeSourceTo,
        IReadOnlyList<ThumbnailVariant> variants,
        CancellationToken cancellationToken)
    {
        var workers = variants
            .Select(variant => StartWorker(variant, cancellationToken))
            .ToList();

        try
        {
            await RunSourceFanOut(
                writeSourceTo,
                workers,
                cancellationToken);

            var results = new List<ThumbnailVariantResult>(workers.Count);

            foreach (var worker in workers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                results.Add(await CollectWorkerAsResult(
                    worker,
                    cancellationToken));
            }

            return results;
        }
        finally
        {
            foreach (var worker in workers)
                worker.Process.Dispose();
        }
    }

    /// <summary>
    /// Same outcome as <see cref="GenerateThumbnails"/>, but with the source already at a SEEKABLE
    /// path on disk. Each ffmpeg worker reads the file directly (<c>-i &lt;path&gt;</c>), so the
    /// demuxer can do random-access reads for moov-at-end mp4s — the case where stdin causes
    /// in-RAM buffering of the whole input plus a 100-frame thumbnail-filter window and blows up.
    /// Caller owns the temp file's lifecycle (typically delete in <c>finally</c>).
    /// </summary>
    public async Task<IReadOnlyList<ThumbnailVariantResult>> GenerateThumbnailsFromFile(
        string filePath,
        IReadOnlyList<ThumbnailVariant> variants,
        CancellationToken cancellationToken)
    {
        var workers = variants
            .Select(variant => StartFileWorker(variant, filePath, cancellationToken))
            .ToList();

        try
        {
            var results = new List<ThumbnailVariantResult>(workers.Count);

            foreach (var worker in workers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                results.Add(await CollectWorkerAsResult(
                    worker,
                    cancellationToken));
            }

            return results;
        }
        finally
        {
            foreach (var worker in workers)
                worker.Process.Dispose();
        }
    }

    private static async Task RunSourceFanOut(
        Func<PipeWriter, CancellationToken, ValueTask> writeSourceTo,
        List<Worker> workers,
        CancellationToken cancellationToken)
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

        try
        {
            await Task.WhenAll(
                produceTask,
                pumpTask);
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
        // -an/-dn/-sn = drop audio/data/subtitle streams that mp4 containers ship — ffmpeg would
        //   otherwise spin them up just to discard, and for some inputs that path leaks memory.
        // thumbnail = pick a representative frame (works on a non-seekable stdin, unlike -ss), so a
        //   video's black fade-in start isn't used; for a static image it just passes the one frame.
        // -frames:v 1 = emit a single frame.
        // scale=N:N:force_original_aspect_ratio=decrease = fit within NxN preserving aspect ratio.
        // -c:v libwebp = FORCE the static-image WebP encoder. Without it the webp muxer picks
        //   libwebp_anim for multi-frame sources (mp4/gif), which buffers all frames and OOMs
        //   ("Cannot allocate memory" / WebPAnimEncoderAssemble failure) for larger inputs.
        // -f webp -y pipe:1 = write WebP to stdout.
        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add("pipe:0");
        psi.ArgumentList.Add("-an");
        psi.ArgumentList.Add("-dn");
        psi.ArgumentList.Add("-sn");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add($"thumbnail=n=25,scale={targetPixelSize}:{targetPixelSize}:force_original_aspect_ratio=decrease");
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libwebp");
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

    private Worker StartFileWorker(
        ThumbnailVariant variant,
        string filePath,
        CancellationToken cancellationToken)
    {
        var targetPixelSize = GetTargetPixelSize(variant);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            // No stdin redirection — ffmpeg reads the source from disk and can seek freely
            // (matters for mp4 moov-at-end).
            RedirectStandardInput = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(filePath);
        psi.ArgumentList.Add("-an");
        psi.ArgumentList.Add("-dn");
        psi.ArgumentList.Add("-sn");
        psi.ArgumentList.Add("-vf");
        psi.ArgumentList.Add($"thumbnail=n=25,scale={targetPixelSize}:{targetPixelSize}:force_original_aspect_ratio=decrease");
        psi.ArgumentList.Add("-frames:v");
        psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("-c:v");
        psi.ArgumentList.Add("libwebp");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add("webp");
        psi.ArgumentList.Add("-y");
        psi.ArgumentList.Add("pipe:1");

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("ffmpeg failed to start.");

        var stdoutBuffer = new MemoryStream();

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
            StderrTask = stderrTask,
            // No stdin for file-based — the pump never touches StdinAlive, so leave default true.
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

    private static async Task<ThumbnailVariantResult> CollectWorkerAsResult(
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

                return new ThumbnailVariantResult(
                    worker.Variant,
                    Thumbnail: null,
                    Error: $"ffmpeg exited with code {worker.Process.ExitCode}: {stderr}");
            }

            if (worker.StdoutBuffer.Length == 0)
            {
                worker.StdoutBuffer.Dispose();

                return new ThumbnailVariantResult(
                    worker.Variant,
                    Thumbnail: null,
                    Error: $"ffmpeg produced empty output. stderr: {stderr}");
            }

            // Hand the buffer to the caller wrapped as IThumbnail — ownership transfers; the
            // consumer disposes via `await using` after the upload finishes.
            return new ThumbnailVariantResult(
                worker.Variant,
                Thumbnail: new FfmpegThumbnail(worker.StdoutBuffer),
                Error: null);
        }
        catch (OperationCanceledException)
        {
            TryKill(worker.Process);
            worker.StdoutBuffer.Dispose();
            throw;
        }
        catch (Exception ex)
        {
            TryKill(worker.Process);
            worker.StdoutBuffer.Dispose();
            
            return new ThumbnailVariantResult(
                worker.Variant,
                Thumbnail: null,
                Error: ex.Message);
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
        public required MemoryStream StdoutBuffer { get; init; } //ownership transfers into FfmpegThumbnail on success; freed inline on failure
        public required Task StdoutTask { get; init; }
        public required Task<string> StderrTask { get; init; }
        public bool StdinAlive { get; set; } = true;
    }

    /// <summary>
    /// Owns the <see cref="MemoryStream"/> that ffmpeg's stdout was drained into. <see cref="Dispose"/>
    /// frees the buffer; <c>await using</c> in the consumer guarantees release after upload.
    /// </summary>
    private sealed class FfmpegThumbnail : IThumbnail
    {
        private readonly MemoryStream _buffer;
        private int _disposed;

        public long SizeInBytes => _buffer.Length;
        public Stream Content => _buffer;

        public FfmpegThumbnail(MemoryStream buffer)
        {
            _buffer = buffer;
            _buffer.Position = 0;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _buffer.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return ValueTask.CompletedTask;

            return _buffer.DisposeAsync();
        }
    }

    private static int GetTargetPixelSize(ThumbnailVariant variant) => variant switch
    {
        ThumbnailVariant.Mini => 96,
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
