using System.Buffers;
using System.IO.Compression;
using System.IO.Pipelines;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages.Exceptions;
using PlikShare.Workspaces.Cache;
using Serilog;
using static PlikShare.Files.PreSignedLinks.PreSignedUrlsService;

namespace PlikShare.Storages.Zip;

public static class ZipEntryReader
{
    const int NoCompression = 0;
    const int DeflateCompressionMethod = 8;

    //this value goal is to satisfy most scenarios of zip files without sacrificing performance too much
    //the most robust solution would be to get maximum possible value which is 65KBs but in most cases
    //that is actually very wasteful. So we assume smaller size and provide a fallback handling for all the edge cases
    //when this value is not enough.
    private const ushort ReasonableExtraFieldSize = 128; 


    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task ReadEntryAsync(
        FileRecord file,
        ZipEntryPayload entry,
        WorkspaceContext workspace,        
        PipeWriter output,
        Func<FileRecord, FileEncryptionMode> getFileEncryptionMode,
        CancellationToken cancellationToken = default)
    {
        var pipe = new Pipe();

        var (readTask, zipLfh) = await ReadFileData(
            file: file, 
            entry: entry, 
            workspace: workspace, 
            pipe: pipe, 
            getFileEncryptionMode: getFileEncryptionMode, 
            cancellationToken: cancellationToken);

        switch (entry.CompressionMethod)
        {
            case NoCompression:
            {
                await Task.WhenAll(
                    readTask, 
                    Extract(output));

                break;
            }

            case DeflateCompressionMethod:
            {
                var decompressionPipe = new Pipe();
                
                await Task.WhenAll(
                    readTask, 
                    Extract(
                        decompressionPipe.Writer,
                        () => decompressionPipe.Writer.CompleteAsync()), 
                    Decompress(
                        decompressionPipe.Reader));

                break;
            }

            default:
                throw new NotSupportedException(
                    $"Compression method {entry.CompressionMethod} not supported");
        }

        return;

        async Task Extract(
            PipeWriter extractOutput,
            Func<ValueTask>? finalization = null)
        {
            try
            {
                await ExtractFileData(
                    file: file,
                    entry: entry,
                    extraFieldLength: zipLfh.ExtraFieldLength,
                    reader: pipe.Reader,
                    output: extractOutput,
                    cancellationToken: cancellationToken);
            }
            finally
            {
                await pipe.Reader.CompleteAsync();
                
                if(finalization is not null)
                    await finalization();
            }
        }

        async Task Decompress(
            PipeReader decompressionReader)
        {
            try
            {
                await DecompressZipEntry(
                    file: file,
                    entry: entry,
                    reader: decompressionReader,
                    output: output);
            }
            finally
            {
                await decompressionReader.CompleteAsync();
            }
        }
    }

    private static async Task<(Task readTask, ZipLfhRecord zipLfh)> ReadFileData(
        FileRecord file, 
        ZipEntryPayload entry, 
        WorkspaceContext workspace,
        Pipe pipe,
        Func<FileRecord, FileEncryptionMode> getFileEncryptionMode,
        CancellationToken cancellationToken)
    {
        var reasonableFileRange = CalculateRangeIncludingLfhWithReasonableExtraFieldAndFileData(
            file,
            entry);

        var readTaskContinuationState = new TaskWithResourceFinalization();
        
        var readTask = readTaskContinuationState.Execute(
            @try: () => workspace.ReadRange(
                details: new DownloadFileRangeDetails(
                    Range: reasonableFileRange,
                    FileKey: file.FileKey,
                    FileSizeInBytes: file.SizeInBytes,
                    EncryptionMode: getFileEncryptionMode(file)),
                output: pipe.Writer,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Writer.CompleteAsync());

        var zipLfh = await ZipDecoder.ReadZipLfhRecordMinimumBytes(
            reader: pipe.Reader,
            cancellationToken: cancellationToken);
        
        if (zipLfh.ExtraFieldLength > ReasonableExtraFieldSize)
        {
            //it seems that assumed reasonable extra field length was not enough
            //it means that the original file range we asked for will not contain all file data bytes
            //so we will continue the original file read with another one that will get missing bytes only
            //that will be nicely transparent for all following operations: data extraction and decompression

            var actualFileDataStart = entry.OffsetToLocalFileHeader
                                      + ZipLfhRecord.MinimumSize
                                      + entry.FileNameLength
                                      + zipLfh.ExtraFieldLength;

            var actualFileDataEnd = actualFileDataStart
                                    + entry.CompressedSizeInBytes
                                    - 1;

            if (actualFileDataEnd > reasonableFileRange.End)
            {
                var missingBytesRange = FileBytesRange.Create(
                    start: reasonableFileRange.End + 1,
                    end: actualFileDataEnd,
                    fileSizeInBytes: file.SizeInBytes);


                readTask = readTaskContinuationState.ContinueWith(
                    continuationFunction: () => workspace.ReadRange(
                        details: new DownloadFileRangeDetails(
                            Range: missingBytesRange,
                            FileKey: file.FileKey,
                            FileSizeInBytes: file.SizeInBytes,
                            EncryptionMode: getFileEncryptionMode(file)),
                        output: pipe.Writer,
                        cancellationToken: cancellationToken),
                    cancellationToken: cancellationToken);
            }
        }

        return (
            readTask,
            zipLfh
        );
    }

    private static BytesRange CalculateRangeIncludingLfhWithReasonableExtraFieldAndFileData(
        FileRecord file, 
        ZipEntryPayload entry)
    {
        return FileBytesRange.Create(
            start: entry.OffsetToLocalFileHeader,
            end: entry.OffsetToLocalFileHeader
                 + entry.FileNameLength
                 + ZipLfhRecord.MinimumSize
                 + ReasonableExtraFieldSize
                 + entry.CompressedSizeInBytes
                 - 1,
            fileSizeInBytes: file.SizeInBytes);
    }
    
    private static async Task ExtractFileData(
        FileRecord file,
        ZipEntryPayload entry,
        ushort extraFieldLength,
        PipeReader reader,
        PipeWriter output,
        CancellationToken cancellationToken)
    {
        try
        {
            // Skip filename and extra field
            var toSkip = entry.FileNameLength + extraFieldLength;
            while (toSkip > 0)
            {
                var readResult = await reader.ReadAsync(
                    cancellationToken);

                if (readResult.IsCanceled)
                    throw new OperationCanceledException(
                        $"Zip file '{file.ExternalId}' entry '{entry.FileName}' reading could not be finished because input stream was cancelled.",
                        cancellationToken);

                var bytesToSkip = (int)Math.Min(
                toSkip,
                readResult.Buffer.Length);

                reader.AdvanceTo(readResult.Buffer.GetPosition(bytesToSkip));
                toSkip -= bytesToSkip;

                //if readResul is completed but toSkip is already equal to null it means 
                //that there may be no more data to read, but we still may have unconsumed data in the buffer
                //that's why we don't want to return here, but we want to continue processing to consume this remaining data
                if (readResult.IsCompleted && toSkip > 0)
                    return;
            }

            // Copy file data
            var remainingBytes = entry.CompressedSizeInBytes;
            while (remainingBytes > 0)
            {
                var readResult = await reader.ReadAsync(cancellationToken);

                if (readResult.IsCanceled)
                    throw new OperationCanceledException(
                        $"Zip file '{file.ExternalId}' entry '{entry.FileName}' reading could not be finished because input stream was cancelled.",
                        cancellationToken);

                var bytesToCopy = (int) Math.Min(
                    remainingBytes, 
                    readResult.Buffer.Length);

                var outputSpan = output.GetSpan(
                    sizeHint: bytesToCopy);

                readResult.Buffer.Slice(0, bytesToCopy).CopyTo(
                    outputSpan.Slice(0, bytesToCopy));

                reader.AdvanceTo(
                    readResult.Buffer.GetPosition(bytesToCopy));

                output.Advance(bytesToCopy);
                remainingBytes -= bytesToCopy;

                var flushResult = await output.FlushAsync(
                    CancellationToken.None);

                if (flushResult.IsCanceled)
                    throw new OperationCanceledException(
                        $"Zip file '{file.ExternalId}' entry '{entry.FileName}' reading could not be finished because output stream was cancelled.",
                        cancellationToken);

                if (flushResult.IsCompleted)
                    break;
                
                if(readResult.IsCompleted)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while downloading zip entry '{ZipEntry}' from File '{FileExternalId}'",
                entry.FileName,
                file.ExternalId);

            throw;
        }
    }

    private static async Task DecompressZipEntry(
        FileRecord file,
        ZipEntryPayload entry,
        PipeReader reader,
        PipeWriter output)
    {
        try
        {
            await using var deflateStream = new DeflateStream(
                reader.AsStream(),
                CompressionMode.Decompress);
            
            await deflateStream.CopyToAsync(
                output.AsStream());
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while decompressing zip entry '{ZipEntry}' from File '{FileExternalId}'",
                entry.FileName,
                file.ExternalId);

            throw;
        }
    }
}