using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Files.Records;
using PlikShare.Storages.Exceptions;
using PlikShare.Storages.FileReading;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Storages.Zip;

/// <summary>
/// End of central directory record (EOCD) structure:
/// [0-3]   4 bytes  - End of central directory signature (0x06054b50)
/// [4-5]   2 bytes  - Number of this disk (0xffff for ZIP64)
/// [6-7]   2 bytes  - Disk where central directory starts (0xffff for ZIP64)
/// [8-9]   2 bytes  - Number of central directory records on this disk (0xffff for ZIP64)
/// [10-11] 2 bytes  - Total number of central directory records (0xffff for ZIP64)
/// [12-15] 4 bytes  - Size of central directory in bytes (0xffffffff for ZIP64)
/// [16-19] 4 bytes  - Offset of central directory from archive start (0xffffffff for ZIP64)
/// [20-21] 2 bytes  - Comment length (n)
/// [22+]   n bytes  - Comment
/// 
/// Central directory file header structure (CDFH):
/// [0-3]   4 bytes  - Signature (0x02014b50)
/// [4-5]   2 bytes  - Version made by
/// [6-7]   2 bytes  - Minimum version needed to extract
/// [8-9]   2 bytes  - Bit flag
/// [10-11] 2 bytes  - Compression method
/// [12-13] 2 bytes  - File last modification time (MS-DOS format)
/// [14-15] 2 bytes  - File last modification date (MS-DOS format)
/// [16-19] 4 bytes  - CRC-32 of uncompressed data
/// [20-23] 4 bytes  - Compressed size
/// [24-27] 4 bytes  - Uncompressed size
/// [28-29] 2 bytes  - File name length (n)
/// [30-31] 2 bytes  - Extra field length (m)
/// [32-33] 2 bytes  - File comment length (k)
/// [34-35] 2 bytes  - Disk number where file starts
/// [36-37] 2 bytes  - Internal file attributes
/// [38-41] 4 bytes  - External file attributes
/// [42-45] 4 bytes  - Offset of local file header (from start of disk)
/// [46+]   n bytes  - File name
/// [46+n]  m bytes  - Extra field
/// [46+n+m] k bytes - File comment
///
/// ZIP64 End of central directory locator:
/// [0-3]   4 bytes  - ZIP64 locator signature (0x07064b50)
/// [4-7]   4 bytes  - Disk number with ZIP64 EOCD
/// [8-15]  8 bytes  - Offset to ZIP64 EOCD record
/// [16-19] 4 bytes  - Total number of disks
/// 
/// ZIP64 End of central directory record:
/// [0-3]   4 bytes  - ZIP64 EOCD signature (0x06064b50)
/// [4-11]  8 bytes  - Size of ZIP64 EOCD record (excluding signature and this field)
/// [12-13] 2 bytes  - Version made by
/// [14-15] 2 bytes  - Version needed to extract
/// [16-19] 4 bytes  - Number of this disk
/// [20-23] 4 bytes  - Disk with central directory start
/// [24-31] 8 bytes  - Number of entries on this disk
/// [32-39] 8 bytes  - Total number of entries
/// [40-47] 8 bytes  - Size of central directory
/// [48-55] 8 bytes  - Offset of central directory
/// [56+]   n bytes  - Extensible data sector (variable size)
///
/// /// Local file header structure (LFH):
/// [0-3]   4 bytes  - Signature (0x04034b50)
/// [4-5]   2 bytes  - Version needed to extract
/// [6-7]   2 bytes  - General purpose bit flag
/// [8-9]   2 bytes  - Compression method
/// [10-11] 2 bytes  - Last modification time (MS-DOS format)
/// [12-13] 2 bytes  - Last modification date (MS-DOS format)
/// [14-17] 4 bytes  - CRC-32 of uncompressed data
/// [18-21] 4 bytes  - Compressed size
/// [22-25] 4 bytes  - Uncompressed size
/// [26-27] 2 bytes  - File name length (n)
/// [28-29] 2 bytes  - Extra field length (m)
/// [30+]   n bytes  - File name
/// [30+n]  m bytes  - Extra field
/// [30+n+m] ...    - File data
/// </summary>
public static class ZipDecoder
{
    public const int EocdMinimumSize = ZipEocdRecord.MinimumSize + Zip64LocatorRecord.Size;
    public const int EocdMaximumSize = ushort.MaxValue + ZipEocdRecord.MinimumSize + Zip64LocatorRecord.Size;


    /// <exception cref="FileNotFoundInStorageException"></exception>
    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<ZipEntriesLookupResult> ReadZipEntries(
        FileRecord file,
        WorkspaceContext workspace,
        CancellationToken cancellationToken = default)
    {
        if (file.SizeInBytes < ZipEocdRecord.MinimumSize)
            throw new InvalidOperationException(
                $"File '{file.ExternalId}' does not have a minimum size of a zip archive ({ZipEocdRecord.MinimumSize} bytes)");

        if(file.Extension != ".zip")
            throw new InvalidOperationException(
                $"File '{file.ExternalId}' is not a zip archive but '{file.Extension}')");

        var pipe = new Pipe();
        
        //for optimization purposes I assume at first that eocd is exactly at last 22 bytes of zip archive
        //that is the most common situation (meaning no comment is appended at the end of zip file) and allows
        //to read only 22 bytes of file instead of 65kb

        var zipEocdLookupResult = await TryReadZipEocdAssumingNoComment(
            file,
            workspace,
            pipe, 
            cancellationToken);

        if (zipEocdLookupResult.Code == ZipEocdLookupResultCode.EocdRecordNotFound)
        {
            Log.Debug(
                "ZIP End of Central Directory record not found at the end of file - will attempt searching with maximum comment length {FileExternalId}", 
                file.ExternalId);

            //if eocd record was not found when we assume there is no comment at the end of the zip
            //file we are going to attempt the extraction one more time - this time assuming the longest possible 
            //comment - so we will get additional 65kb of file and look there

            zipEocdLookupResult = await TryReadZipEocdAssumingLongestComment(
                file,
                workspace,
                pipe,
                cancellationToken);
        }

        var (eocdLookupResultCode, zipEocd, zip64Locator) = zipEocdLookupResult;

        if (eocdLookupResultCode == ZipEocdLookupResultCode.EocdRecordNotFound)
        {
            Log.Warning(
                "ZIP End of Central Directory record not found in file - archive appears to be corrupted {FileExternalId}", 
                file.ExternalId);
            
            return new ZipEntriesLookupResult { Code = ZipDecodingResultCode.ZipFileBroken };
        }

        if (eocdLookupResultCode == ZipEocdLookupResultCode.InvalidZip64LocatorSignature)
        {
            Log.Warning(
                "Invalid ZIP64 End of Central Directory Locator signature found in file {FileExternalId}", 
                file.ExternalId);

            return new ZipEntriesLookupResult { Code = ZipDecodingResultCode.ZipFileBroken };
        }

        ZipFinalEocdRecord zipFinalEocd;

        if (zip64Locator is not null)
        {
            //if the zip64 locator record was found alongside the zip eocd record it means we need
            //to read zip64 additional eocd record to find values which will help us then find to correct
            //location of central directory file header structure

            var (zip64EocdLookupResultCode, zip64Eocd) = await TryReadZip64Eocd(
                file, 
                workspace,
                zip64Locator, 
                pipe, 
                cancellationToken);

            if(zip64EocdLookupResultCode == Zip64EocdLookupResultCode.InvalidSignature)
            {
                Log.Warning(
                    "Invalid ZIP64 End of Central Directory signature found in file {FileExternalId}", 
                    file.ExternalId);

                return new ZipEntriesLookupResult { Code = ZipDecodingResultCode.ZipFileBroken };
            }

            zipFinalEocd = new ZipFinalEocdRecord(zipEocd!, zip64Eocd);
        }
        else
        {
            zipFinalEocd = new ZipFinalEocdRecord(zipEocd!);
        }
        
        //at the end when we know how the final location of CDFH looks like
        //we can start reading zip entries

        var zipEntries = await ReadZipEntries(
            file, 
            workspace,
            zipFinalEocd, 
            pipe, 
            cancellationToken);

        return new ZipEntriesLookupResult
        {
            Code = ZipDecodingResultCode.Ok,
            Entries = zipEntries
        };
    }

    private static async Task<List<ZipCdfhRecord>> ReadZipEntries(
        FileRecord file,
        WorkspaceContext workspace,
        ZipFinalEocdRecord zipFinalEocd, 
        Pipe pipe, 
        CancellationToken cancellationToken)
    {
        var zipCentralDirectoryFileReading = Try.Execute(
            @try: () => FileReader.ReadRange(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                fileEncryption: file.Encryption,
                fileSizeInBytes: file.SizeInBytes,
                range: zipFinalEocd.CentralDirectoryBytesRange,
                workspace: workspace,
                output: pipe.Writer,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Writer.CompleteAsync());

        var zipEntriesReading = Try.Execute(
            @try: ()=> ReadZipEntries(
                centralDir: zipFinalEocd,
                input: pipe.Reader,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Reader.CompleteAsync());

        await Task.WhenAll(
            zipCentralDirectoryFileReading,
            zipEntriesReading);

        pipe.Reset();

        return await zipEntriesReading;
    }
    
    private static async Task<Zip64EocdLookupResult> TryReadZip64Eocd(
        FileRecord file,
        WorkspaceContext workspace,
        Zip64LocatorRecord zip64Locator, 
        Pipe pipe, 
        CancellationToken cancellationToken)
    {
        var zip64EocdFileReading = Try.Execute(
            @try: () => FileReader.ReadRange(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                fileEncryption: file.Encryption,
                fileSizeInBytes: file.SizeInBytes,
                range: new BytesRange(
                    zip64Locator.Zip64EocdOffset,
                    zip64Locator.Zip64EocdOffset + Zip64EocdRecord.MinimumSize - 1),
                workspace: workspace,
                output: pipe.Writer,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Writer.CompleteAsync());

        var zip64EocdLookup = Try.Execute(
            @try: () => ReadZip64EocdRecord(
                input: pipe.Reader,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Reader.CompleteAsync());

        await Task.WhenAll(
            zip64EocdFileReading,
            zip64EocdLookup);

        pipe.Reset();
        
        return await zip64EocdLookup;
    }


    private static async Task<ZipEocdLookupResult> TryReadZipEocdAssumingNoComment(
        FileRecord file,
        WorkspaceContext workspace,
        Pipe pipe, 
        CancellationToken cancellationToken)
    {
        var zipEocdFileReading = Try.Execute(
            @try: () => FileReader.ReadRange(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                fileEncryption: file.Encryption,
                fileSizeInBytes: file.SizeInBytes,
                range: new BytesRange(
                    //we assume that someone could have used zip64 so we are getting ready to read its locator
                    file.SizeInBytes - EocdMinimumSize,
                    file.SizeInBytes - 1),
                workspace: workspace,
                output: pipe.Writer,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Writer.CompleteAsync());
            
        var zipEocdLookup = Try.Execute(
            @try: () => FindAndReadZipEocdAndZip64LocatorRecords(
                input: pipe.Reader,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Reader.CompleteAsync());

        await Task.WhenAll(
            zipEocdFileReading,
            zipEocdLookup);

        pipe.Reset();

        return await zipEocdLookup;
    }
    
    private static async Task<ZipEocdLookupResult> TryReadZipEocdAssumingLongestComment(
        FileRecord file,
        WorkspaceContext workspace,
        Pipe pipe,
        CancellationToken cancellationToken)
    {
        // Read the last 64KB or the whole file if smaller
        // (EOCD could be anywhere in the last 64KB due to optional zip file comment)
        var endBytes = Math.Min(EocdMaximumSize, file.SizeInBytes);
        var eocdPossibleStartPosition = file.SizeInBytes - endBytes;

        var zipEocdWithCommentFileReading = Try.Execute(
            @try: () => FileReader.ReadRange(
                s3FileKey: new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                fileEncryption: file.Encryption,
                fileSizeInBytes: file.SizeInBytes,
                range: new BytesRange(
                    eocdPossibleStartPosition,
                    file.SizeInBytes - 1),
                workspace: workspace,
                output: pipe.Writer,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Writer.CompleteAsync());

        var zipEocdWithCommentLookup = Try.Execute(
            @try: () => FindAndReadZipEocdAndZip64LocatorRecords(
                input: pipe.Reader,
                cancellationToken: cancellationToken),
            @finally: () => pipe.Reader.CompleteAsync());

        await Task.WhenAll(
            zipEocdWithCommentFileReading,
            zipEocdWithCommentLookup);

        pipe.Reset();

        return await zipEocdWithCommentLookup;
    }

    /// <exception cref="OperationCanceledException"></exception>
    public static async Task<ZipEocdLookupResult> FindAndReadZipEocdAndZip64LocatorRecords(
        PipeReader input,
        CancellationToken cancellationToken)
    {
        var zip64LocatorBuffer = new SteppingBuffer(Zip64LocatorRecord.Size);
        var wasEocdSignatureFound = false;

        while (true)
        {
            var readResult = await input.ReadAsync(
                cancellationToken);

            if (readResult.IsCanceled)
                throw new OperationCanceledException(
                    "ZIP EOCD signature search was cancelled before completion",
                    cancellationToken);

            var buffer = readResult.Buffer;

            var reader = new SequenceReader<byte>(buffer);

            while (reader.Remaining >= ZipEocdRecord.Signature.Length)
            {
                if (reader.IsNext(ZipEocdRecord.Signature))
                {
                    wasEocdSignatureFound = true;
                    break;
                }

                if (reader.TryRead(out var currentByte))
                    zip64LocatorBuffer.Push(currentByte);
            }

            input.AdvanceTo(
                consumed: buffer.GetPosition(reader.Consumed),
                examined: buffer.End);

            if (readResult.IsCompleted && !wasEocdSignatureFound)
                return new ZipEocdLookupResult { Code = ZipEocdLookupResultCode.EocdRecordNotFound };

            if (wasEocdSignatureFound)
                break;
        }

        var eocdReadResult = await input.ReadAtLeastAsync(
            ZipEocdRecord.MinimumSize,
            cancellationToken);

        if (eocdReadResult.IsCanceled)
            throw new OperationCanceledException(
                "Reading ZIP EOCD record data was cancelled before completion",
                cancellationToken);

        var eocdBuffer = eocdReadResult.Buffer;
        var sequenceReader = new SequenceReader<byte>(eocdBuffer);

        //skip eocd signature, we have already checked it in the loop above
        sequenceReader.Advance(ZipEocdRecord.Signature.Length);

        var numberOfThisDisk = sequenceReader.ReadUInt16LittleEndian();
        var diskWhereCentralDirectoryStarts = sequenceReader.ReadUInt16LittleEndian();
        var numberOfCentralDirectoryRecordsOnThisDisk = sequenceReader.ReadUInt16LittleEndian();
        var totalNumberOfCentralDirectoryRecords = sequenceReader.ReadUInt16LittleEndian();
        var sizeOfCentralDirectoryInBytes = sequenceReader.ReadUInt32LittleEndian();
        var offsetToStartCentralDirectory = sequenceReader.ReadUInt32LittleEndian();
        var commentLength = sequenceReader.ReadUInt16LittleEndian();

        input.AdvanceTo(eocdBuffer.GetPosition(sequenceReader.Consumed));

        var zipEocdRecord = new ZipEocdRecord
        {
            NumberOfThisDisk = numberOfThisDisk,
            DiskWhereCentralDirectoryStarts = diskWhereCentralDirectoryStarts,
            NumbersOfCentralDirectoryRecordsOnThisDisk = numberOfCentralDirectoryRecordsOnThisDisk,
            TotalNumberOfCentralDirectoryRecords = totalNumberOfCentralDirectoryRecords,
            SizeOfCentralDirectoryInBytes = sizeOfCentralDirectoryInBytes,
            OffsetToStartCentralDirectory = offsetToStartCentralDirectory,
            CommentLength = commentLength
        };

        if (!zipEocdRecord.HasZip64Markers())
        {
            return new ZipEocdLookupResult
            {
                Code = ZipEocdLookupResultCode.Ok,
                Eocd = new ZipEocdRecords
                {
                    ZipEocdRecord = zipEocdRecord,
                    Zip64LocatorRecord = null
                }
            };
        }

        var zipSpan = zip64LocatorBuffer.GetSpan();
        var offset = 0;

        var zip64Signature = BinaryPrimitives.ReadUInt32LittleEndian(
            zipSpan.Slice(offset, Zip64LocatorRecord.Signature.Length));

        if (zip64Signature != Zip64LocatorRecord.SignatureValue)
            return new ZipEocdLookupResult { Code = ZipEocdLookupResultCode.InvalidZip64LocatorSignature };

        offset += Zip64LocatorRecord.Signature.Length;

        var diskWithZip64Eocd = BinaryPrimitives.ReadUInt32LittleEndian(
            zipSpan.Slice(offset, 4));

        offset += 4;

        var zip64EocdOffset = BinaryPrimitives.ReadUInt64LittleEndian(
            zipSpan.Slice(offset, 8));

        offset += 8;

        var totalNumberOfDisks = BinaryPrimitives.ReadUInt32LittleEndian(
            zipSpan.Slice(offset, 4));

        return new ZipEocdLookupResult
        {
            Code = ZipEocdLookupResultCode.Ok,
            Eocd = new ZipEocdRecords
            {
                ZipEocdRecord = zipEocdRecord,
                Zip64LocatorRecord = new Zip64LocatorRecord
                {
                    DiskWithZip64Eocd = diskWithZip64Eocd,
                    Zip64EocdOffset = (long)zip64EocdOffset,
                    TotalNumberOfDisks = totalNumberOfDisks,
                }
            }
        };
    }
    
    public static async Task<Zip64EocdLookupResult> ReadZip64EocdRecord(
        PipeReader input,
        CancellationToken cancellationToken)
    {
        var readResult = await input.ReadAtLeastAsync(
            Zip64EocdRecord.MinimumSize,
            cancellationToken);

        if (readResult.IsCanceled)
            throw new OperationCanceledException(
                "Reading ZIP64 End of Central Directory record was cancelled before completion",
                cancellationToken);

        var sequenceReader = new SequenceReader<byte>(readResult.Buffer);

        var signature = sequenceReader.ReadUInt32LittleEndian();

        if (signature != Zip64EocdRecord.SignatureValue)
            return new Zip64EocdLookupResult { Code = Zip64EocdLookupResultCode.InvalidSignature };

        var sizeOfZip64EocdRecord = sequenceReader.ReadUInt64LittleEndian();
        var versionMadeBy = sequenceReader.ReadUInt16LittleEndian();
        var versionNeededToExtract = sequenceReader.ReadUInt16LittleEndian();
        var numberOfThisDisk = sequenceReader.ReadUInt32LittleEndian();
        var diskWithCentralDirectoryStart = sequenceReader.ReadUInt32LittleEndian();
        var numberOfEntriesOnThisDisk = sequenceReader.ReadUInt64LittleEndian();
        var totalNumberOfEntries = sequenceReader.ReadUInt64LittleEndian();
        var sizeOfCentralDirectory = sequenceReader.ReadUInt64LittleEndian();
        var offsetOfCentralDirectory = sequenceReader.ReadUInt64LittleEndian();

        input.AdvanceTo(readResult.Buffer.GetPosition(sequenceReader.Consumed));

        return new Zip64EocdLookupResult
        {
            Code = Zip64EocdLookupResultCode.Ok,
            Zip64EocdRecord = new Zip64EocdRecord
            {
                SizeOfZip64EocdRecord = (long) sizeOfZip64EocdRecord,
                VersionMadeBy = versionMadeBy,
                VersionNeededToExtract = versionNeededToExtract,
                DiskNumber = numberOfThisDisk,
                DiskWithCentralDirectoryStart = diskWithCentralDirectoryStart,
                NumberOfEntriesOnDisk = (long) numberOfEntriesOnThisDisk,
                TotalNumberOfEntries = (long) totalNumberOfEntries,
                SizeOfCentralDirectory = (long) sizeOfCentralDirectory,
                OffsetToCentralDirectory = (long) offsetOfCentralDirectory,
            }
        };
    }
    
    public static async Task<List<ZipCdfhRecord>> ReadZipEntries(
        ZipFinalEocdRecord centralDir,
        PipeReader input,
        CancellationToken cancellationToken)
    {
        var readingHeapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: ushort.MaxValue); //maximum file name length in zip archives

        var readingBuffer = readingHeapBuffer
            .AsMemory(0, ushort.MaxValue);
        
        var result = new List<ZipCdfhRecord>();

        try
        {
            for (uint index = 0; index < centralDir.TotalNumberOfEntries; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = await ReadCdfhEntry(
                    index: index,
                    reader: input,
                    readingBuffer: readingBuffer,
                    cancellationToken: cancellationToken);

                result.Add(entry);
            }

            return result;

        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to read ZIP central directory entries. Processed {ProcessedEntries} out of {TotalEntries} entries",
                result.Count,
                centralDir.TotalNumberOfEntries);

            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readingHeapBuffer);
        }
    }

    private static async Task<ZipCdfhRecord> ReadCdfhEntry(
        uint index,
        PipeReader reader,
        Memory<byte> readingBuffer,
        CancellationToken cancellationToken)
    {
        var headerResult = await reader.ReadAtLeastAsync(
            ZipCdfhRecord.MinimumSize,
            cancellationToken);

        if (headerResult.IsCanceled)
            throw new OperationCanceledException(
                "Reading ZIP Central Directory File Header was canceled",
                cancellationToken);

        var sequenceReader = new SequenceReader<byte>(headerResult.Buffer);

        var signature = sequenceReader.ReadUInt32LittleEndian();

        if (signature != ZipCdfhRecord.SignatureValue)
            throw new InvalidDataException("Invalid Central Directory Entry signature");

        var versionMadeBy = sequenceReader.ReadUInt16LittleEndian();
        var minimumVersionNeededToExtract = sequenceReader.ReadUInt16LittleEndian();
        var bitFlag = sequenceReader.ReadUInt16LittleEndian();
        var compressionMethod = sequenceReader.ReadUInt16LittleEndian();
        var filetLastModificationTime = sequenceReader.ReadUInt16LittleEndian();
        var fileLastModificationDate = sequenceReader.ReadUInt16LittleEndian();
        var crc32OfUncompressedData = sequenceReader.ReadUInt32LittleEndian();
        var compressedSize = sequenceReader.ReadUInt32LittleEndian();
        var uncompressedSize = sequenceReader.ReadUInt32LittleEndian();
        var fileNameLength = sequenceReader.ReadUInt16LittleEndian();
        var extraFieldLength = sequenceReader.ReadUInt16LittleEndian();
        var fileCommentLength = sequenceReader.ReadUInt16LittleEndian();
        var diskNumberWhereFileStarts = sequenceReader.ReadUInt16LittleEndian();
        var internalFileAttributes = sequenceReader.ReadUInt16LittleEndian();
        var externalFileAttributes = sequenceReader.ReadUInt32LittleEndian();
        var offsetToLocalFileHeader = sequenceReader.ReadUInt32LittleEndian();

        reader.AdvanceTo(headerResult.Buffer.GetPosition(sequenceReader.Consumed));

        var fileName = await ReadStringFromCdfh(
            reader,
            fileNameLength,
            readingBuffer,
            cancellationToken);

        var extraField = await ReadBytesFromCdfh(
            reader,
            extraFieldLength,
            readingBuffer,
            cancellationToken);

        var fileComment = await ReadStringFromCdfh(
            reader,
            fileCommentLength,
            readingBuffer,
            cancellationToken);

        // Check which fields need ZIP64 values
        var needsZip64UncompressedSize = uncompressedSize == 0xFFFFFFFF;
        var needsZip64CompressedSize = compressedSize == 0xFFFFFFFF;
        var needsZip64Offset = offsetToLocalFileHeader == 0xFFFFFFFF;
        var needsZip64Disk = diskNumberWhereFileStarts == 0xFFFF;

        var zip64Extra = needsZip64UncompressedSize || needsZip64CompressedSize || needsZip64Offset || needsZip64Disk
            ? ParseZip64ExtraField(
                extraField,
                needsZip64UncompressedSize,
                needsZip64CompressedSize,
                needsZip64Offset,
                needsZip64Disk)
            : null;

        return new ZipCdfhRecord
        {
            //fields which are always read from original zip CDFH record
            VersionMadeBy = versionMadeBy,
            MinimumVersionNeededToExtract = minimumVersionNeededToExtract,
            BitFlag = bitFlag,
            CompressionMethod = compressionMethod,
            FileLastModificationTime = filetLastModificationTime,
            FileLastModificationDate = fileLastModificationDate,
            Crc32OfUncompressedData = crc32OfUncompressedData,
            FileNameLength = fileNameLength,
            ExtraFieldLength = extraFieldLength,
            FileCommentLength = fileCommentLength,
            InternalFileAttributes = internalFileAttributes,
            ExternalFileAttributes = externalFileAttributes,
            FileName = fileName,
            ExtraField = extraField,
            FileComment = fileComment,
            IndexInArchive = index,

            //fields which are getting replaced in case of ZIP64 format
            CompressedSize = needsZip64CompressedSize
                ? zip64Extra?.CompressedSize ?? throw new InvalidOperationException("ZIP64 compressed size required but not provided")
                : compressedSize,

            UncompressedSize = needsZip64UncompressedSize
                ? zip64Extra?.UncompressedSize ?? throw new InvalidOperationException("ZIP64 uncompressed size required but not provided")
                : uncompressedSize,

            OffsetToLocalFileHeader = needsZip64Offset
                ? zip64Extra?.LocalHeaderOffset ?? throw new InvalidOperationException("ZIP64 offset required but not provided")
                : offsetToLocalFileHeader,

            DiskNumberWhereFileStarts = needsZip64Disk
                ? zip64Extra?.DiskStart ?? throw new InvalidOperationException("ZIP64 disk number required but not provided")
                : diskNumberWhereFileStarts,
        };
    }

    private static async Task<string> ReadStringFromCdfh(
        PipeReader reader,
        ushort stringLength,
        Memory<byte> readingBuffer,
        CancellationToken cancellationToken)
    {
        if(stringLength == 0)
            return string.Empty;

        var readStringResult = await reader.ReadAtLeastAsync(
            minimumSize: stringLength,
            cancellationToken: cancellationToken);

        if (readStringResult.IsCanceled)
            throw new OperationCanceledException(
                "Reading data from ZIP Central Directory File Header was canceled",
                cancellationToken);

        var readBuffer = readStringResult.Buffer;

        var actualSize = (int) Math.Min(
            stringLength,
            readBuffer.Length);

        if (actualSize < stringLength)
        {
            throw new InvalidOperationException(
                $"Not enough bytes were retrieved to read a string from CDFH record. Expected bytes: {stringLength}, actual bytes: {actualSize}");
        }

        readBuffer
            .Slice(0, stringLength)
            .CopyTo(readingBuffer.Span.Slice(0, stringLength));

        reader.AdvanceTo(
            readBuffer.GetPosition(stringLength));

        return Encoding.UTF8.GetString(
            readingBuffer.Span.Slice(0, stringLength));
    }

    private static async Task<byte[]> ReadBytesFromCdfh(
        PipeReader reader,
        ushort length,
        Memory<byte> readingBuffer,
        CancellationToken cancellationToken)
    {
        if (length == 0)
            return [];

        var readResult = await reader.ReadAtLeastAsync(
            minimumSize: length,
            cancellationToken: cancellationToken);

        if (readResult.IsCanceled)
            throw new OperationCanceledException(
                "Reading data from ZIP Central Directory File Header was canceled",
                cancellationToken);

        var readBuffer = readResult.Buffer;

        var actualSize = (int)Math.Min(
            length,
            readBuffer.Length);

        if (actualSize < length)
        {
            throw new InvalidOperationException(
                $"Not enough bytes were retrieved to read a string from CDFH record. Expected bytes: {length}, actual bytes: {actualSize}");
        }

        readBuffer
            .Slice(0, length)
            .CopyTo(readingBuffer.Span.Slice(0, length));

        reader.AdvanceTo(
            readBuffer.GetPosition(length));

        return readingBuffer
            .Span
            .Slice(0, length)
            .ToArray();
    }

    private static Zip64ExtraField ParseZip64ExtraField(
        byte[] extraField,
        bool needsUncompressed,
        bool needsCompressed,
        bool needsOffset,
        bool needsDisk)
    {
        var offset = 0;
        while (offset + 4 <= extraField.Length)
        {
            var headerId = BinaryPrimitives.ReadUInt16LittleEndian(
                extraField.AsSpan(offset, 2));

            var dataSize = BinaryPrimitives.ReadUInt16LittleEndian(
                extraField.AsSpan(offset + 2, 2));

            if (headerId == Zip64ExtraField.HeaderId)
            {
                var position = offset + 4;

                ulong? uncompressedSize = null;
                ulong? compressedSize = null;
                ulong? localHeaderOffset = null;
                uint? diskStart = null;

                if (needsUncompressed)
                {
                    uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(
                        extraField.AsSpan(position, 8));

                    position += 8;
                }

                if (needsCompressed)
                {
                    compressedSize = BinaryPrimitives.ReadUInt64LittleEndian(
                        extraField.AsSpan(position, 8));

                    position += 8;
                }

                if (needsOffset)
                {
                    localHeaderOffset = BinaryPrimitives.ReadUInt64LittleEndian(
                        extraField.AsSpan(position, 8));

                    position += 8;
                }

                if (needsDisk)
                {
                    diskStart = BinaryPrimitives.ReadUInt32LittleEndian(
                        extraField.AsSpan(position, 4));
                }

                return new Zip64ExtraField
                {
                    UncompressedSize = (long?) uncompressedSize,
                    CompressedSize = (long?) compressedSize,
                    LocalHeaderOffset = (long?) localHeaderOffset,
                    DiskStart = diskStart
                };
            }

            offset += 4 + dataSize;
        }

        throw new InvalidDataException("Required ZIP64 extra field not found");
    }

    public static async Task<ZipLfhRecord> ReadZipLfhRecordMinimumBytes(
        PipeReader reader,
        CancellationToken cancellationToken)
        {
            var readResult = await reader.ReadAtLeastAsync(
                   minimumSize: ZipLfhRecord.MinimumSize,
                   cancellationToken: cancellationToken);

            var sequenceReader = new SequenceReader<byte>(
                readResult.Buffer);

            var signature = sequenceReader.ReadUInt32LittleEndian();

            if (signature != ZipLfhRecord.SignatureValue)
                throw new InvalidOperationException(
                    $"Wrong signature for Zip Local File Header Record.");

            var versionNeededToExtract = sequenceReader.ReadUInt16LittleEndian();
            var generalPurposeBitFlat = sequenceReader.ReadUInt16LittleEndian();
            var compressionMethod = sequenceReader.ReadUInt16LittleEndian();
            var lastModificationTime = sequenceReader.ReadUInt16LittleEndian();
            var lastModificationDate = sequenceReader.ReadUInt16LittleEndian();
            var crc32 = sequenceReader.ReadUInt32LittleEndian();
            var compressedSize = sequenceReader.ReadUInt32LittleEndian();
            var uncompressedSize = sequenceReader.ReadUInt32LittleEndian();
            var fileNameLength = sequenceReader.ReadUInt16LittleEndian();
            var extraFieldLength = sequenceReader.ReadUInt16LittleEndian();

            reader.AdvanceTo(readResult.Buffer.GetPosition(sequenceReader.Consumed));

            return new ZipLfhRecord
            {
                VersionNeededToExtract = versionNeededToExtract,
                GeneralPurposeBitFlag = generalPurposeBitFlat,
                CompressionMethod = compressionMethod,
                LastModificationTime = lastModificationTime,
                LastModificationDate = lastModificationDate,
                Crc32OfUncompressedData = crc32,
                CompressedSize = compressedSize,
                UncompressedSize = uncompressedSize,
                FileNameLength = fileNameLength,
                ExtraFieldLength = extraFieldLength
            };
        }

    public class Zip64EocdLookupResult
    {
        public required Zip64EocdLookupResultCode Code { get; init; }
        public Zip64EocdRecord? Zip64EocdRecord { get; init; }

        public void Deconstruct(
            out Zip64EocdLookupResultCode code,
            out Zip64EocdRecord? zip64EocdRecord)
        {
            code = Code;
            zip64EocdRecord = Zip64EocdRecord;
        }
    }

    public enum Zip64EocdLookupResultCode
    {
        Ok = 0,
        InvalidSignature
    }

    public class ZipEocdLookupResult
    {
        public required ZipEocdLookupResultCode Code { get; init; }
        public ZipEocdRecords? Eocd { get; init; }

        public void Deconstruct(
            out ZipEocdLookupResultCode code,
            out ZipEocdRecord? zipEocdRecord,
            out Zip64LocatorRecord? zip64LocatorRecord)
        {
            code = Code;
            zipEocdRecord = Eocd?.ZipEocdRecord;
            zip64LocatorRecord = Eocd?.Zip64LocatorRecord;
        }
    }

    public class ZipEocdRecords
    {
        public required ZipEocdRecord ZipEocdRecord { get; init; }
        public required Zip64LocatorRecord? Zip64LocatorRecord { get; init; }
    }

    public enum ZipEocdLookupResultCode
    {
        Ok = 0,
        EocdRecordNotFound,
        InvalidZip64LocatorSignature
    }

    public enum ZipDecodingResultCode
    {
        Ok = 0,
        ZipFileBroken
    }

    public class ZipEntriesLookupResult
    {
        public required ZipDecodingResultCode Code { get; init; }
        public List<ZipCdfhRecord>? Entries { get; init; }
    }
}