using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages.FileReading;
using Serilog;
using System.Buffers;
using System.IO.Pipelines;

namespace PlikShare.Core.Encryption;

public static class FileEncryption
{
    public static PreparedFilePart PrepareFilePartForUpload(
        Memory<byte> input,
        long fileSizeInBytes,
        FilePart filePart,
        FileEncryptionMode encryptionMode,
        CancellationToken cancellationToken)
    {
        if (input.Length != filePart.SizeInBytes)
            throw new ArgumentException(
                $"Input memory length ({input.Length}) does not match " +
                $"filePart.SizeInBytes ({filePart.SizeInBytes}). Part number: {filePart.Number}.",
                nameof(input));

        if (encryptionMode is NoEncryption)
            return new PreparedFilePart(input);

        var heapBufferSize = CalculateBufferSize(
            encryptionMode: encryptionMode,
            filePart: filePart);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        try
        {
            var heapBufferMemory = heapBuffer
                .AsMemory()
                .Slice(0, heapBufferSize);

            if (encryptionMode is AesGcmV1Encryption v1)
            {
                Aes256GcmStreamingV1.CopyIntoBufferReadyForInPlaceEncryption(
                    input: input.Span,
                    output: heapBufferMemory,
                    filePart: filePart);

                Aes256GcmStreamingV1.EncryptFilePartInPlace(
                    fileAesInputs: v1.Input,
                    filePart: filePart,
                    fullFileSizeInBytes: fileSizeInBytes,
                    inputOutputBuffer: heapBufferMemory,
                    cancellationToken: cancellationToken);
            }
            else if (encryptionMode is AesGcmV2Encryption v2)
            {
                Aes256GcmStreamingV2.CopyIntoBufferReadyForInPlaceEncryption(
                    input: input.Span,
                    output: heapBufferMemory,
                    filePart: filePart,
                    chainStepsCount: v2.Input.ChainStepSalts.Count);

                Aes256GcmStreamingV2.EncryptFilePartInPlace(
                    fileAesInputs: v2.Input,
                    filePart: filePart,
                    fullFileSizeInBytes: fileSizeInBytes,
                    inputOutputBuffer: heapBufferMemory,
                    cancellationToken: cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported file encryption mode '{encryptionMode.GetType().Name}'.");
            }

            return new PreparedFilePart(
                pooledBuffer: heapBuffer,
                length: heapBufferSize);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
            throw;
        }
    }

    public static async ValueTask<PreparedFilePart> PrepareFilePartForUpload(
        PipeReader input,
        long fileSizeInBytes,
        FilePart filePart,
        FileEncryptionMode encryptionMode,
        CancellationToken cancellationToken)
    {
        var heapBufferSize = CalculateBufferSize(
            encryptionMode: encryptionMode,
            filePart: filePart);

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: heapBufferSize);

        try
        {
            var heapBufferMemory = heapBuffer
                .AsMemory()
                .Slice(0, heapBufferSize);

            if (encryptionMode is NoEncryption)
            {
                await input.CopyTo(
                    output: heapBufferMemory,
                    sizeInBytes: filePart.SizeInBytes,
                    cancellationToken: cancellationToken);
            }
            else if (encryptionMode is AesGcmV1Encryption v1)
            {
                await Aes256GcmStreamingV1.CopyIntoBufferReadyForInPlaceEncryption(
                    input,
                    output: heapBufferMemory,
                    filePart: filePart);

                Aes256GcmStreamingV1.EncryptFilePartInPlace(
                    fileAesInputs: v1.Input,
                    filePart: filePart,
                    fullFileSizeInBytes: fileSizeInBytes,
                    inputOutputBuffer: heapBufferMemory,
                    cancellationToken: cancellationToken);
            }
            else if (encryptionMode is AesGcmV2Encryption v2)
            {
                await Aes256GcmStreamingV2.CopyIntoBufferReadyForInPlaceEncryption(
                    input,
                    output: heapBufferMemory,
                    filePart: filePart,
                    chainStepsCount: v2.Input.ChainStepSalts.Count);

                Aes256GcmStreamingV2.EncryptFilePartInPlace(
                    fileAesInputs: v2.Input,
                    filePart: filePart,
                    fullFileSizeInBytes: fileSizeInBytes,
                    inputOutputBuffer: heapBufferMemory,
                    cancellationToken: cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported file encryption mode '{encryptionMode.GetType().Name}'.");
            }

            return new PreparedFilePart(
                pooledBuffer: heapBuffer,
                length: heapBufferSize);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(heapBuffer);
            throw;
        }
    }

    private static int CalculateBufferSize(
        FileEncryptionMode encryptionMode,
        FilePart filePart)
    {
        return encryptionMode switch
        {
            NoEncryption =>
                filePart.SizeInBytes,

            AesGcmV1Encryption =>
                Aes256GcmStreamingV1.CalculateEncryptedPartSize(
                    filePart),

            AesGcmV2Encryption v2 =>
                Aes256GcmStreamingV2.CalculateEncryptedPartSize(
                    filePart,
                    v2.Input.ChainStepSalts.Count),

            _ => throw new InvalidOperationException(
                $"Unsupported file encryption mode '{encryptionMode.GetType()}'.")
        };
    }

    public static FileRangeReadPlan CalculateRangeReadPlan(
        FileEncryptionMode encryptionMode,
        long fileSizeInBytes,
        BytesRange range)
    {
        switch (encryptionMode)
        {
            case NoEncryption:
                return new FileRangeReadPlan(range, null);

            case AesGcmV1Encryption:
            {
                var er = Aes256GcmStreamingV1.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                    range, 
                    fileSizeInBytes);

                return new FileRangeReadPlan(
                    er.ToBytesRange(), 
                    er);
            }

            case AesGcmV2Encryption v2:
            {
                var er = Aes256GcmStreamingV2.EncryptedBytesRangeCalculator.FromUnencryptedRange(
                    range, 
                    fileSizeInBytes, 
                    v2.Input.ChainStepSalts.Count);

                return new FileRangeReadPlan(
                    er.ToBytesRange(), 
                    er);
            }

            default:
                throw new InvalidOperationException(
                    $"Unsupported file encryption mode '{encryptionMode.GetType().Name}'.");
        }
    }

    // File download is split into two phases so callers can handle storage errors
    // separately from read/decrypt errors. Phase 1 (locating the file and opening
    // the Stream) happens in the caller - IStorageClient.DownloadFile /
    // DownloadFileRange - and is where "file does not exist on disk / in S3"
    // surfaces (e.g. FileNotFoundInStorageException), before any bytes reach the
    // HTTP response, so the caller can still return a clean 404. Phase 2
    // (streaming + AES-GCM decryption) is deferred to IStorageFile.ReadTo on the
    // returned instance; by the time it runs response headers may already be
    // flushed, so failures there must be handled differently (typically by
    // aborting the connection rather than returning a JSON error).
    public static IStorageFile ReadFileRange(
        long fileSizeInBytes,
        FileRangeReadPlan readPlan,
        FileEncryptionMode encryptionMode,
        Stream stream,
        Func<Serilog.ILogger, Serilog.ILogger>? enrichLogs = null)
    {
        return new StorageFileRange(
            fileSizeInBytes: fileSizeInBytes,
            encryptionMode: encryptionMode,
            encryptedRange: readPlan.EncryptedRange,
            stream: stream,
            enrichLogs: enrichLogs);
    }

    // File download is split into two phases so callers can handle storage errors
    // separately from read/decrypt errors. Phase 1 (locating the file and opening
    // the Stream) happens in the caller - IStorageClient.DownloadFile /
    // DownloadFileRange - and is where "file does not exist on disk / in S3"
    // surfaces (e.g. FileNotFoundInStorageException), before any bytes reach the
    // HTTP response, so the caller can still return a clean 404. Phase 2
    // (streaming + AES-GCM decryption) is deferred to IStorageFile.ReadTo on the
    // returned instance; by the time it runs response headers may already be
    // flushed, so failures there must be handled differently (typically by
    // aborting the connection rather than returning a JSON error).
    public static IStorageFile ReadFile(
        long fileSizeInBytes,
        FileEncryptionMode encryptionMode,
        Stream stream,
        Func<Serilog.ILogger, Serilog.ILogger>? enrichLogs = null) =>
        new StorageFile(fileSizeInBytes, encryptionMode, stream, enrichLogs);
    
    private class StorageFile(
        long fileSizeInBytes,
        FileEncryptionMode encryptionMode,
        Stream stream,
        Func<Serilog.ILogger, Serilog.ILogger>? enrichLogs = null) : IStorageFile
    {
        private readonly Serilog.ILogger _logger = CreateLogger(enrichLogs);

        private static Serilog.ILogger CreateLogger(Func<Serilog.ILogger, Serilog.ILogger>? enrich)
        {
            var logger = Log.ForContext<StorageFile>();
            return enrich?.Invoke(logger) ?? logger;
        }

        public async ValueTask ReadTo(
            PipeWriter output,
            CancellationToken cancellationToken)
        {
            var logger = Log.ForContext(typeof(FileEncryption));

            if (enrichLogs is not null)
                logger = enrichLogs(logger);

            var startTime = DateTime.UtcNow;

            try
            {
                if (encryptionMode is NoEncryption)
                {
                    logger.Debug("Starting unencrypted file transfer");

                    await stream.CopyToAsync(
                        destination: output,
                        cancellationToken: cancellationToken);

                    var streamDuration = DateTime.UtcNow - startTime;
                    var streamSpeed = fileSizeInBytes / Math.Max(1, streamDuration.TotalSeconds);

                    logger.Debug(
                        "Completed unencrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        streamDuration.TotalMilliseconds,
                        streamSpeed / 1024.0 / 1024.0);
                }
                else if (encryptionMode is AesGcmV1Encryption v1)
                {
                    logger.Debug("Starting encrypted file transfer using AES-256-GCM");

                    await Aes256GcmStreamingV1.Decrypt(
                        fileAesInputs: v1.Input,
                        fileSizeInBytes: fileSizeInBytes,
                        input: PipeReader.Create(
                            stream,
                            new StreamPipeReaderOptions(
                                bufferSize: PlikShareStreams.DefaultBufferSize,
                                leaveOpen: false)),
                        output: output,
                        cancellationToken);

                    var decryptDuration = DateTime.UtcNow - startTime;
                    var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                    logger.Debug(
                        "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        decryptDuration.TotalMilliseconds,
                        decryptSpeed / 1024.0 / 1024.0);
                }
                else if (encryptionMode is AesGcmV2Encryption v2)
                {
                    logger.Debug("Starting encrypted file transfer using AES-256-GCM");

                    await Aes256GcmStreamingV2.Decrypt(
                        fileAesInputs: v2.Input,
                        fileSizeInBytes: fileSizeInBytes,
                        input: PipeReader.Create(
                            stream,
                            new StreamPipeReaderOptions(
                                bufferSize: PlikShareStreams.DefaultBufferSize,
                                leaveOpen: false)),
                        output: output,
                        cancellationToken);

                    var decryptDuration = DateTime.UtcNow - startTime;
                    var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                    logger.Debug(
                        "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        decryptDuration.TotalMilliseconds,
                        decryptSpeed / 1024.0 / 1024.0);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported file encryption mode '{encryptionMode.GetType().Name}'.");
                }

                var totalDuration = DateTime.UtcNow - startTime;
                var averageSpeed = fileSizeInBytes / Math.Max(1, totalDuration.TotalSeconds);

                _logger.Information(
                    "Successfully completed download operation. Size: {FileSize:N2} MB, " +
                    "Duration: {DurationSec:N1}s, Average speed: {Speed:N2} MB/s",
                    fileSizeInBytes / 1024.0 / 1024.0,
                    totalDuration.TotalSeconds,
                    averageSpeed / 1024.0 / 1024.0);
            }
            catch (UnauthorizedAccessException e)
            {
                _logger.Error(e, "Access denied while downloading file");
                throw;
            }
            catch (IOException e)
            {
                _logger.Error(e, "IO error while downloading file");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Download operation cancelled");
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to download file. Error: {ErrorMessage}", e.Message);
                throw;
            }
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
        }
    }

    private class StorageFileRange(
        long fileSizeInBytes,
        FileEncryptionMode encryptionMode,
        EncryptedBytesRange? encryptedRange,
        Stream stream,
        Func<Serilog.ILogger, Serilog.ILogger>? enrichLogs = null) : IStorageFile
    {
        private readonly Serilog.ILogger _logger = CreateLogger(enrichLogs);
        private static Serilog.ILogger CreateLogger(Func<Serilog.ILogger, Serilog.ILogger>? enrich)
        {
            var logger = Log.ForContext<StorageFile>();
            return enrich?.Invoke(logger) ?? logger;
        }

        public async ValueTask ReadTo(
            PipeWriter output, 
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.Debug(
                    "Reading file ({FileSize:N0} bytes)",
                    fileSizeInBytes);

                if (encryptionMode is NoEncryption)
                {
                    _logger.Debug("Starting unencrypted file transfer");

                    await stream.CopyToAsync(
                        destination: output,
                        cancellationToken: cancellationToken);

                    var streamDuration = DateTime.UtcNow - startTime;
                    var streamSpeed = fileSizeInBytes / Math.Max(1, streamDuration.TotalSeconds);

                    _logger.Debug(
                        "Completed unencrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        streamDuration.TotalMilliseconds,
                        streamSpeed / 1024.0 / 1024.0);
                }
                else if (encryptionMode is AesGcmV1Encryption v1)
                {
                    _logger.Debug("Starting encrypted file transfer using AES-256-GCM");

                    if (encryptedRange is null)
                    {
                        throw new InvalidOperationException(
                            $"EncryptedRange is required for encryption mode '{encryptionMode.GetType().Name}' but was null. " +
                            $"Ensure FileRangeReadPlan was built from a matching FileEncryptionMode.");
                    }

                    await Aes256GcmStreamingV1.DecryptRange(
                        fileAesInputs: v1.Input,
                        range: encryptedRange,
                        fileSizeInBytes: fileSizeInBytes,
                        input: PipeReader.Create(
                            stream,
                            new StreamPipeReaderOptions(
                                bufferSize: PlikShareStreams.DefaultBufferSize,
                                leaveOpen: false)),
                        output: output,
                        cancellationToken);

                    var decryptDuration = DateTime.UtcNow - startTime;
                    var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                    _logger.Debug(
                        "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        decryptDuration.TotalMilliseconds,
                        decryptSpeed / 1024.0 / 1024.0);
                }
                else if (encryptionMode is AesGcmV2Encryption v2)
                {
                    _logger.Debug("Starting encrypted file transfer using AES-256-GCM");

                    if (encryptedRange is null)
                    {
                        throw new InvalidOperationException(
                            $"EncryptedRange is required for encryption mode '{encryptionMode.GetType().Name}' but was null. " +
                            $"Ensure FileRangeReadPlan was built from a matching FileEncryptionMode.");
                    }

                    await Aes256GcmStreamingV2.DecryptRange(
                        fileAesInputs: v2.Input,
                        range: encryptedRange,
                        fileSizeInBytes: fileSizeInBytes,
                        input: PipeReader.Create(
                            stream,
                            new StreamPipeReaderOptions(
                                bufferSize: PlikShareStreams.DefaultBufferSize,
                                leaveOpen: false)),
                        output: output,
                        cancellationToken);

                    var decryptDuration = DateTime.UtcNow - startTime;
                    var decryptSpeed = fileSizeInBytes / Math.Max(1, decryptDuration.TotalSeconds);

                    _logger.Debug(
                        "Completed encrypted file transfer: {FileSize:N0} bytes in {DurationMs}ms. Speed: {Speed:N2} MB/s",
                        fileSizeInBytes,
                        decryptDuration.TotalMilliseconds,
                        decryptSpeed / 1024.0 / 1024.0);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Unsupported file encryption format version '{encryptionMode.GetType().Name}'.");
                }

                var totalDuration = DateTime.UtcNow - startTime;
                var averageSpeed = fileSizeInBytes / Math.Max(1, totalDuration.TotalSeconds);

                _logger.Information(
                    "Successfully completed download operation. Size: {FileSize:N2} MB, " +
                    "Duration: {DurationSec:N1}s, Average speed: {Speed:N2} MB/s",
                    fileSizeInBytes / 1024.0 / 1024.0,
                    totalDuration.TotalSeconds,
                    averageSpeed / 1024.0 / 1024.0);
            }
            catch (UnauthorizedAccessException e)
            {
                _logger.Error(e, "Access denied while downloading file");
                throw;
            }
            catch (IOException e)
            {
                _logger.Error(e, "IO error while downloading file");
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("Download operation cancelled");
                //we don't throw here as constant cancelling is normal behavior for video players
                //throw;
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to download file. Error: {ErrorMessage}", e.Message);
                throw;
            }
        }

        public void Dispose()
        {
            stream.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await stream.DisposeAsync();
        }
    }
}

public sealed class PreparedFilePart : IDisposable
{
    private readonly byte[]? _pooledBuffer;
    private int _disposed;

    internal PreparedFilePart(byte[] pooledBuffer, int length)
    {
        _pooledBuffer = pooledBuffer;
        Memory = pooledBuffer.AsMemory(0, length);
    }

    internal PreparedFilePart(ReadOnlyMemory<byte> memory)
    {
        _pooledBuffer = null;
        Memory = memory;
    }

    public ReadOnlyMemory<byte> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            return field;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_pooledBuffer is not null)
            ArrayPool<byte>.Shared.Return(_pooledBuffer);
    }
}