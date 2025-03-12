using System.Buffers;
using System.IO.Pipelines;
using Serilog;

namespace PlikShare.Storages.HardDrive.Download;

public class FileRangeProcessing
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<FileRangeProcessing>();

    public static async Task CopyBytes(
        PipeReader input,
        long lengthToCopy,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        var bytesRemaining = lengthToCopy;
        var totalBytesCopied = 0L;
        var startTime = DateTime.UtcNow;

        try
        {
            Logger.Information(
                "Starting range copy of {BytesToCopy} bytes",
                lengthToCopy);

            while (bytesRemaining > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await input.ReadAsync(cancellationToken);
                var buffer = result.Buffer;

                if (buffer.Length == 0 && result.IsCompleted)
                {
                    Logger.Warning(
                        "Input stream completed unexpectedly after {BytesCopied} of {TotalBytes} bytes",
                        totalBytesCopied, lengthToCopy);

                    break;
                }

                var bytesToCopy = (int)Math.Min(buffer.Length, bytesRemaining);
                var outputSpan = output.GetSpan(bytesToCopy);

                buffer.Slice(0, bytesToCopy).CopyTo(outputSpan.Slice(0, bytesToCopy));
                output.Advance(bytesToCopy);

                var flushResult = await output.FlushAsync(
                    CancellationToken.None);

                if (flushResult.IsCanceled)
                    throw new OperationCanceledException(
                        "CopyBytes could not be finished because output stream was cancelled.",
                        cancellationToken);

                if (flushResult.IsCompleted)
                {
                    Logger.Information(
                        "Consumer completed range request after {BytesCopied} of {TotalBytes} bytes. Duration: {Duration:g}",
                        totalBytesCopied, lengthToCopy, DateTime.UtcNow - startTime);

                    break;
                }

                input.AdvanceTo(buffer.GetPosition(bytesToCopy));
                bytesRemaining -= bytesToCopy;
                totalBytesCopied += bytesToCopy;
            }

            Logger.Information(
                "Range copy completed successfully. {BytesCopied} bytes copied in {Duration:g}",
                totalBytesCopied, DateTime.UtcNow - startTime);
        }
        catch (OperationCanceledException)
        {
            Logger.Information(
                "Range copy cancelled after {BytesCopied} of {TotalBytes} bytes. Duration: {Duration:g}",
                totalBytesCopied, lengthToCopy, DateTime.UtcNow - startTime);

            throw;
        }
        catch (Exception e)
        {
            Logger.Error(
                e,
                "Range copy failed after {BytesCopied} of {TotalBytes} bytes. Duration: {Duration:g}",
                totalBytesCopied, lengthToCopy, DateTime.UtcNow - startTime);

            throw;
        }
        finally
        {
            await input.CompleteAsync();
        }
    }
}