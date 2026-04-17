using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Security.Cryptography;
using PlikShare.Core.Utils;
using PlikShare.Files.Records;
using PlikShare.Storages.Encryption;
using Serilog;

namespace PlikShare.Core.Encryption;

/// <summary>
/// V2 variant of the streaming AEAD. Format frame:
///
/// First Segment:
/// HEADER | CIPHERTEXT | TAG
///
/// Header (variable length, depends on number of chain steps N):
/// FORMAT_VERSION(1)=0x02 | STORAGE_DEK_VERSION(1) | CHAIN_STEPS_COUNT(1)
/// | N × STEP_SALT(32) | FILE_SALT(32) | NONCE_PREFIX(7)
///
/// Total header size = 42 + 32 * N bytes (deterministic from CHAIN_STEPS_COUNT).
///
/// Next Segments:
/// CIPHERTEXT | TAG
///
/// Key derivation walks the chain via <see cref="KeyDerivationChain.Derive"/> from the storage
/// DEK down to a terminal DEK, then HKDF(terminalDek, salt=fileSalt) produces the AES-256-GCM key.
/// Empty chain reduces to V1 semantics but still uses the V2 header layout.
/// </summary>
public static class Aes256GcmStreamingV2
{
    public const int SegmentSize = 1 * SizeInBytes.Mb;

    public const int SegmentsPerFilePart = 10;
    public const int MaximumPayloadSize = SegmentsPerFilePart * SegmentSize;

    private const int DerivedKeySize = 32;

    private const int NoncePrefixSize = 7;
    private const int SaltSize = DerivedKeySize;

    public const byte FormatVersion = 0x02;

    private const int FormatVersionSize = 1;
    private const int StorageDekVersionSize = 1;
    private const int ChainStepsCountSize = 1;

    public const int BaseHeaderSize =
        FormatVersionSize + StorageDekVersionSize + ChainStepsCountSize
        + SaltSize + NoncePrefixSize;

    public const int SegmentsCiphertextSize = SegmentSize - TagSize;

    private const int SegmentNumberSize = 4;
    private const int TagSize = 16;
    private const int IvSize = NoncePrefixSize + SegmentNumberSize + 1;
    private const byte IvByteForIntermediateSegments = 0x00;
    private const byte IvByteForLastSegment = 0x01;
    
    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);
    public static byte[] GenerateNoncePrefix() => RandomNumberGenerator.GetBytes(NoncePrefixSize);

    public static int GetHeaderSize(int chainStepsCount)
    {
        if (chainStepsCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(chainStepsCount),
                chainStepsCount,
                "Chain steps count must be non-negative.");

        return BaseHeaderSize + chainStepsCount * KeyDerivationChain.StepSaltSize;
    }

    public static int GetFirstSegmentCiphertextSize(int chainStepsCount)
        => SegmentSize - TagSize - GetHeaderSize(chainStepsCount);

    public static long GetFirstFilePartSizeInBytes(int chainStepsCount)
        => GetFirstSegmentCiphertextSize(chainStepsCount)
           + (SegmentsPerFilePart - 1) * (long)SegmentsCiphertextSize;

    public const long FilePartSizeInBytes = SegmentsPerFilePart * (long)SegmentsCiphertextSize;

    public static EncryptedBytesRangeCalculatorV2 EncryptedBytesRangeCalculator { get; } = new(
        segmentSize: SegmentSize,
        tagSize: TagSize,
        baseHeaderSize: BaseHeaderSize,
        stepSaltSize: KeyDerivationChain.StepSaltSize);

    public static async ValueTask CopyIntoBufferReadyForInPlaceEncryption(
        PipeReader reader,
        Memory<byte> output,
        FilePart filePart,
        int chainStepsCount)
    {
        VerifyBufferSize(output, filePart, chainStepsCount);

        var headerSize = GetHeaderSize(chainStepsCount);
        var firstSegmentCiphertextSize = GetFirstSegmentCiphertextSize(chainStepsCount);

        var offset = filePart.Number == 1
            ? headerSize
            : 0;

        var bytesToCopyLeft = filePart.SizeInBytes;

        var nextSegmentBytesLeft = filePart.Number == 1
            ? firstSegmentCiphertextSize
            : SegmentsCiphertextSize;

        while (bytesToCopyLeft > 0)
        {
            var bytesToCopyInThisSegment = Math.Min(
                nextSegmentBytesLeft,
                bytesToCopyLeft);

            var readResult = await reader.ReadAtLeastAsync(
                minimumSize: bytesToCopyInThisSegment);

            if (readResult.Buffer.Length == 0 && readResult.IsCompleted)
                throw new InvalidOperationException(
                    $"Unexpected end of input stream while copying plaintext data. " +
                    $"BytesToCopyLeft: {bytesToCopyLeft}, PartNumber: {filePart.Number}, PartSizeInBytes: {filePart.SizeInBytes}");

            var actualBytesToCopy = (int)Math.Min(
                readResult.Buffer.Length,
                bytesToCopyInThisSegment);

            readResult.Buffer
                .Slice(0, actualBytesToCopy)
                .CopyTo(output.Span.Slice(offset, actualBytesToCopy));

            reader.AdvanceTo(
                readResult.Buffer.GetPosition(actualBytesToCopy));

            offset += actualBytesToCopy + TagSize;
            bytesToCopyLeft -= actualBytesToCopy;

            nextSegmentBytesLeft = SegmentsCiphertextSize;
        }
    }

    public static void CopyIntoBufferReadyForInPlaceEncryption(
        ReadOnlySpan<byte> input,
        Memory<byte> output,
        FilePart filePart,
        int chainStepsCount)
    {
        VerifyBufferSize(output, filePart, chainStepsCount);

        var headerSize = GetHeaderSize(chainStepsCount);
        var firstSegmentCiphertextSize = GetFirstSegmentCiphertextSize(chainStepsCount);

        var offset = filePart.Number == 1
            ? headerSize
            : 0;

        var sourceOffset = 0;

        var bytesToCopyLeft = filePart.SizeInBytes;

        var nextSegmentBytesLeft = filePart.Number == 1
            ? firstSegmentCiphertextSize
            : SegmentsCiphertextSize;

        while (bytesToCopyLeft > 0)
        {
            var bytesToCopy = Math.Min(
                nextSegmentBytesLeft,
                bytesToCopyLeft);

            input.Slice(sourceOffset, bytesToCopy)
                .CopyTo(output.Span.Slice(offset, bytesToCopy));

            sourceOffset += bytesToCopy;
            offset += bytesToCopy + TagSize;
            bytesToCopyLeft -= bytesToCopy;
            nextSegmentBytesLeft = SegmentsCiphertextSize;
        }
    }

    public static ValueTask CopyIntoBufferReadyForInPlaceEncryption(
        this Stream stream,
        Memory<byte> output,
        FilePart filePart,
        int chainStepsCount)
    {
        return CopyIntoBufferReadyForInPlaceEncryption(
            reader: PipeReader.Create(stream),
            output,
            filePart,
            chainStepsCount);
    }

    /// <summary>                                                   
    /// Upper bound on the buffer needed to hold a multi-file batch after encryption.
    /// Uses a worst-case header (V2 with the deepest chain we'd ever realistically use:
    /// workspace + box + user/link gives 3 steps, the cap is set with a 2× safety margin).
    /// Slight over-allocation on V1 or shorter V2 chains is intentional — we'd rather waste
    /// a few KB than thread per-file plans through the whole upload pipeline.                                                                              /// </summary>                                              
    private const int SafeUpperBoundChainSteps = 8;

    public static int CalculateSafeBufferSizeForMultiFileUploads(int totalSizeInBytes, int numberOfFiles)
    {
        var worstHeaderSize = GetHeaderSize(SafeUpperBoundChainSteps);
        var worstPerFileOverhead = worstHeaderSize + TagSize;

        return totalSizeInBytes + numberOfFiles * worstPerFileOverhead + 9 * TagSize;
    }

    public static int CalculateEncryptedPartSize(
        FilePart filePart,
        int chainStepsCount)
    {
        if (filePart.Number == 1)
        {
            var headerSize = GetHeaderSize(chainStepsCount);
            var firstSegmentCiphertextSize = GetFirstSegmentCiphertextSize(chainStepsCount);

            var sum = headerSize;
            var firstSegment = Math.Min(filePart.SizeInBytes, firstSegmentCiphertextSize);
            sum += firstSegment + TagSize;
            var rest = filePart.SizeInBytes - firstSegment;

            if (rest > 0)
            {
                sum += CalculateNextSegmentsSize(rest);
            }

            return sum;
        }

        return CalculateNextSegmentsSize(filePart.SizeInBytes);
    }

    private static int CalculateNextSegmentsSize(int totalBytes)
    {
        var fullSegments = totalBytes / SegmentsCiphertextSize;
        var lastSegmentSize = totalBytes % SegmentsCiphertextSize;

        var sum = (fullSegments * SegmentsCiphertextSize) + (fullSegments * TagSize);

        if (lastSegmentSize > 0)
        {
            sum += lastSegmentSize + TagSize;
        }

        return sum;
    }

    private readonly record struct Header(
        byte StorageDekVersion,
        IReadOnlyList<byte[]> ChainStepSalts,
        ReadOnlyMemory<byte> FileSalt,
        ReadOnlyMemory<byte> NoncePrefix);

    private readonly record struct Chunk(
        int Size,
        bool IsStreamComplete);

    public static void EncryptFilePartInPlace(
        FileAesInputsV2 fileAesInputs,
        FilePart filePart,
        long fullFileSizeInBytes,
        Memory<byte> inputOutputBuffer,
        CancellationToken cancellationToken)
    {
        var (ikm, storageDekVersion, chainStepSalts, salt, noncePrefix) = fileAesInputs;

        VerifyInputKeyMaterialSize(ikm.Length);
        VerifyPartNumberSize(filePart, fullFileSizeInBytes, chainStepSalts.Count);
        VerifyBufferSize(inputOutputBuffer, filePart, chainStepSalts.Count);

        var chainStepsCount = chainStepSalts.Count;
        var headerSize = GetHeaderSize(chainStepsCount);

        var plaintextSegmentBufferSize = Math.Min(
            SegmentsCiphertextSize,
            filePart.SizeInBytes);

        var encryptionHeapBufferSize = DerivedKeySize + IvSize + plaintextSegmentBufferSize;

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: encryptionHeapBufferSize);

        Memory<byte> heapBufferMemory = heapBuffer.AsMemory();

        Memory<byte> derivedKeyBuffer = heapBufferMemory.Slice(
            start: 0,
            length: DerivedKeySize);

        Memory<byte> ivBuffer = heapBufferMemory.Slice(
            start: DerivedKeySize,
            length: IvSize);

        Memory<byte> plaintextSegmentBuffer = heapBufferMemory.Slice(
            start: DerivedKeySize + IvSize,
            length: plaintextSegmentBufferSize);

        try
        {
            if (filePart.Number == 1)
            {
                WriteHeader(
                    storageDekVersion: storageDekVersion,
                    chainStepSalts: chainStepSalts,
                    fileSalt: salt.AsSpan(),
                    noncePrefix: noncePrefix.AsSpan(),
                    output: inputOutputBuffer);
            }

            using var aesGcm = PrepareAesGcm(
                ikm: ikm,
                fileSalt: salt.AsSpan(),
                deriveKeyBuffer: derivedKeyBuffer.Span);

            var expectedLastSegmentNumber = GetExpectedLastSegmentNumber(
                fullFileSizeInBytes,
                chainStepsCount);

            var segmentNumber = (filePart.Number - 1) * SegmentsPerFilePart;

            var plaintextBytesLeft = filePart.SizeInBytes;

            var offset = filePart.Number == 1
                ? headerSize
                : 0;

            while (plaintextBytesLeft > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ComputeIvForSegment(
                    noncePrefix: noncePrefix.AsSpan(),
                    segmentNumberInFile: segmentNumber,
                    isLastSegmentInFile: segmentNumber == expectedLastSegmentNumber,
                    iv: ivBuffer.Span);

                var actualChunkSize = Math.Min(
                    GetPlaintextChunkSize(
                        isFirstSegment: segmentNumber == 0,
                        chainStepsCount: chainStepsCount),
                    plaintextBytesLeft);

                var plaintext = plaintextSegmentBuffer
                    .Span
                    .Slice(0, actualChunkSize);

                inputOutputBuffer
                    .Span
                    .Slice(offset, actualChunkSize)
                    .CopyTo(plaintext);

                var outputSegmentSize = actualChunkSize + TagSize;

                aesGcm.Encrypt(
                    nonce: ivBuffer.Span,
                    plaintext: plaintext,
                    ciphertext: inputOutputBuffer.Span.Slice(offset, actualChunkSize),
                    tag: inputOutputBuffer.Span.Slice(offset + actualChunkSize, TagSize),
                    associatedData: null
                );

                segmentNumber++;

                plaintextBytesLeft -= actualChunkSize;
                offset += outputSegmentSize;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while encrypting V2 file part");
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                array: heapBuffer);
        }
    }

    private static AesGcm PrepareAesGcm(
        ReadOnlySpan<byte> ikm,
        ReadOnlySpan<byte> fileSalt,
        Span<byte> deriveKeyBuffer)
    {
        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: ikm,
            output: deriveKeyBuffer,
            salt: fileSalt,
            info: null);

        return new AesGcm(
            key: deriveKeyBuffer,
            tagSizeInBytes: TagSize);
    }

    private static void WriteHeader(
        byte storageDekVersion,
        IReadOnlyList<byte[]> chainStepSalts,
        ReadOnlySpan<byte> fileSalt,
        ReadOnlySpan<byte> noncePrefix,
        Memory<byte> output)
    {
        var headerSize = GetHeaderSize(chainStepSalts.Count);

        WriteHeaderToSpan(
            storageDekVersion: storageDekVersion,
            chainStepSalts: chainStepSalts,
            fileSalt: fileSalt,
            noncePrefix: noncePrefix,
            outputSpan: output.Span.Slice(0, headerSize));
    }

    private static void WriteHeaderToSpan(
        byte storageDekVersion,
        IReadOnlyList<byte[]> chainStepSalts,
        ReadOnlySpan<byte> fileSalt,
        ReadOnlySpan<byte> noncePrefix,
        Span<byte> outputSpan)
    {
        if (chainStepSalts.Count > byte.MaxValue)
            throw new ArgumentException(
                $"Chain steps count {chainStepSalts.Count} exceeds the byte field capacity ({byte.MaxValue}).",
                nameof(chainStepSalts));

        outputSpan[0] = FormatVersion;
        outputSpan[FormatVersionSize] = storageDekVersion;
        outputSpan[FormatVersionSize + StorageDekVersionSize] = (byte)chainStepSalts.Count;

        var offset = FormatVersionSize + StorageDekVersionSize + ChainStepsCountSize;

        foreach (var salt in chainStepSalts)
        {
            if (salt.Length != KeyDerivationChain.StepSaltSize)
                throw new ArgumentException(
                    $"Each chain step salt must be {KeyDerivationChain.StepSaltSize} bytes, got {salt.Length}.",
                    nameof(chainStepSalts));

            salt.CopyTo(outputSpan.Slice(offset, KeyDerivationChain.StepSaltSize));
            offset += KeyDerivationChain.StepSaltSize;
        }

        fileSalt.CopyTo(outputSpan.Slice(offset, SaltSize));
        offset += SaltSize;

        noncePrefix.CopyTo(outputSpan.Slice(offset, NoncePrefixSize));
    }

    private static async ValueTask<Chunk> ReadChunk(
        int chunkSize,
        PipeReader input,
        Memory<byte> inputStreamBuffer,
        CancellationToken cancellationToken)
    {
        var readResult = await input.ReadAtLeastAsync(
            minimumSize: chunkSize,
            cancellationToken: cancellationToken);

        var readBuffer = readResult.Buffer;

        var actualSegmentSize = (int)Math.Min(
            chunkSize,
            readBuffer.Length);

        readBuffer
            .Slice(0, actualSegmentSize)
            .CopyTo(inputStreamBuffer.Span.Slice(0, actualSegmentSize));

        input.AdvanceTo(
            readResult.Buffer.GetPosition(actualSegmentSize));

        return new Chunk(
            Size: actualSegmentSize,
            IsStreamComplete: readResult.IsCompleted);
    }

    private static int GetPlaintextChunkSize(bool isFirstSegment, int chainStepsCount)
    {
        return isFirstSegment
            ? GetFirstSegmentCiphertextSize(chainStepsCount)
            : SegmentsCiphertextSize;
    }

    //assumption: pipe reader needs to already start from the first segment beginning (after the header)
    public static async ValueTask DecryptRange(
        FileAesInputsV2 fileAesInputs,
        EncryptedBytesRange range,
        long fileSizeInBytes,
        PipeReader input,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        var (ikm, _, chainStepSalts, salt, noncePrefix) = fileAesInputs;
        var chainStepsCount = chainStepSalts.Count;

        var inputBufferSize = (int)Math.Min(
            fileSizeInBytes + TagSize,
            SegmentSize);

        var decryptionHeapBufferSize = DerivedKeySize + IvSize + inputBufferSize;

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: decryptionHeapBufferSize);

        Memory<byte> heapBufferMemory = heapBuffer.AsMemory();

        Memory<byte> derivedKeyBuffer = heapBufferMemory.Slice(
            start: 0,
            length: DerivedKeySize);

        Memory<byte> ivBuffer = heapBufferMemory.Slice(
            start: DerivedKeySize,
            length: IvSize);

        Memory<byte> inputBuffer = heapBufferMemory.Slice(
            start: DerivedKeySize + IvSize,
            length: inputBufferSize);

        try
        {
            using var aesGcm = PrepareAesGcm(
                ikm: ikm,
                fileSalt: salt.AsSpan(),
                deriveKeyBuffer: derivedKeyBuffer.Span);

            var segmentNumber = range.FirstSegment.Number;

            var expectedLastSegmentNumber = GetExpectedLastSegmentNumber(
                fullFileSizeInBytes: fileSizeInBytes,
                chainStepsCount: chainStepsCount);

            while (segmentNumber <= range.LastSegment.Number)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkSize = GetCiphertextChunkSize(
                    isFirstSegment: segmentNumber == 0,
                    chainStepsCount: chainStepsCount);

                var chunk = await ReadChunk(
                    chunkSize: chunkSize,
                    input: input,
                    inputStreamBuffer: inputBuffer,
                    cancellationToken: cancellationToken);

                if (chunk is { Size: 0, IsStreamComplete: true })
                    break;

                ComputeIvForSegment(
                    noncePrefix: noncePrefix.AsSpan(),
                    segmentNumberInFile: segmentNumber,
                    isLastSegmentInFile: segmentNumber == expectedLastSegmentNumber,
                    iv: ivBuffer.Span);

                var ciphertextSize = chunk.Size - TagSize;

                if (segmentNumber == range.FirstSegment.Number || segmentNumber == range.LastSegment.Number)
                {
                    var startIndex = segmentNumber == range.FirstSegment.Number
                        ? range.FirstSegmentReadOffset
                        : 0;

                    var readLength = segmentNumber == range.LastSegment.Number
                        ? (range.LastSegmentReadOffset + 1) - startIndex
                        : ciphertextSize - startIndex;

                    var outputSpan = output.GetSpan(
                        sizeHint: ciphertextSize);

                    aesGcm.Decrypt(
                        nonce: ivBuffer.Span,
                        ciphertext: inputBuffer.Span.Slice(0, ciphertextSize),
                        tag: inputBuffer.Span.Slice(ciphertextSize, TagSize),
                        plaintext: outputSpan.Slice(0, ciphertextSize),
                        associatedData: null);

                    if (startIndex > 0)
                    {
                        outputSpan
                            .Slice(startIndex, readLength)
                            .CopyTo(outputSpan.Slice(0, readLength));
                    }

                    output.Advance(readLength);
                }
                else
                {
                    var outputSpan = output.GetSpan(
                        sizeHint: ciphertextSize);

                    aesGcm.Decrypt(
                        nonce: ivBuffer.Span,
                        ciphertext: inputBuffer.Span.Slice(0, ciphertextSize),
                        tag: inputBuffer.Span.Slice(ciphertextSize, TagSize),
                        plaintext: outputSpan.Slice(0, ciphertextSize),
                        associatedData: null);

                    output.Advance(ciphertextSize);
                }

                var flushResult = await output.FlushAsync(
                    CancellationToken.None);

                if (flushResult.IsCanceled)
                    throw new OperationCanceledException(
                        "Range decryption could not be finished because output stream was cancelled.",
                        cancellationToken);

                if (flushResult.IsCompleted)
                    break;

                if (chunk.IsStreamComplete)
                    break;

                segmentNumber++;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while decrypting V2 file range");
            throw;
        }
        finally
        {
            await input.CompleteAsync();

            ArrayPool<byte>.Shared.Return(
                array: heapBuffer);
        }
    }

    public static async ValueTask Decrypt(
        FileAesInputsV2 fileAesInputs,
        long fileSizeInBytes,
        PipeReader input,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        var (ikm, _, chainStepSalts, salt, noncePrefix) = fileAesInputs;
        var chainStepsCount = chainStepSalts.Count;

        var inputBufferSize = (int)Math.Min(
            fileSizeInBytes + TagSize,
            SegmentSize);

        // Header buffer sized to the file's actual chain length — header is read past
        // (to advance the pipe) but its content is discarded; AES inputs come from FileAesInputs.
        var headerBufferSize = GetHeaderSize(chainStepsCount);
        var decryptionHeapBufferSize = headerBufferSize + DerivedKeySize + IvSize + inputBufferSize;

        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: decryptionHeapBufferSize);

        Memory<byte> heapBufferMemory = heapBuffer.AsMemory();

        Memory<byte> headerBuffer = heapBufferMemory.Slice(
            start: 0,
            length: headerBufferSize);

        Memory<byte> derivedKeyBuffer = heapBufferMemory.Slice(
            start: headerBufferSize,
            length: DerivedKeySize);

        Memory<byte> ivBuffer = heapBufferMemory.Slice(
            start: headerBufferSize + DerivedKeySize,
            length: IvSize);

        Memory<byte> inputBuffer = heapBufferMemory.Slice(
            start: headerBufferSize + DerivedKeySize + IvSize,
            length: inputBufferSize);

        try
        {
            //todo do we read it now?
            _ = await ReadHeader(
                input,
                headerBuffer,
                cancellationToken);

            using var aesGcm = PrepareAesGcm(
                ikm: ikm,
                fileSalt: salt.AsSpan(),
                deriveKeyBuffer: derivedKeyBuffer.Span);

            var expectedLastSegmentNumber = GetExpectedLastSegmentNumber(
                fullFileSizeInBytes: fileSizeInBytes,
                chainStepsCount: chainStepsCount);

            var segmentNumber = 0;

            while (true)
            {
                var chunkSize = GetCiphertextChunkSize(
                    isFirstSegment: segmentNumber == 0,
                    chainStepsCount: chainStepsCount);

                var chunk = await ReadChunk(
                    chunkSize: chunkSize,
                    input: input,
                    inputStreamBuffer: inputBuffer,
                    cancellationToken: cancellationToken);

                if (chunk is { Size: 0, IsStreamComplete: true })
                    break;

                ComputeIvForSegment(
                    noncePrefix: noncePrefix.AsSpan(),
                    segmentNumberInFile: segmentNumber,
                    isLastSegmentInFile: segmentNumber == expectedLastSegmentNumber,
                    iv: ivBuffer.Span);

                var ciphertextSize = chunk.Size - TagSize;

                var outputSpan = output.GetSpan(
                    sizeHint: ciphertextSize);

                aesGcm.Decrypt(
                    nonce: ivBuffer.Span,
                    ciphertext: inputBuffer.Span.Slice(0, ciphertextSize),
                    tag: inputBuffer.Span.Slice(ciphertextSize, TagSize),
                    plaintext: outputSpan.Slice(0, ciphertextSize),
                    associatedData: null);

                output.Advance(ciphertextSize);

                var flushResult = await output.FlushAsync(
                    cancellationToken: CancellationToken.None);

                if (flushResult.IsCanceled)
                    throw new OperationCanceledException(
                        "Decryption could not be finished because output stream was cancelled.",
                        cancellationToken);

                if (flushResult.IsCompleted)
                    break;

                if (chunk.IsStreamComplete)
                    break;

                segmentNumber++;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while decrypting V2 file");
            throw;
        }
        finally
        {
            await input.CompleteAsync();

            ArrayPool<byte>.Shared.Return(
                array: heapBuffer);
        }
    }

    private static int GetCiphertextChunkSize(
        bool isFirstSegment,
        int chainStepsCount)
    {
        return SegmentSize - (isFirstSegment ? GetHeaderSize(chainStepsCount) : 0);
    }

    private static async ValueTask<Header> ReadHeader(
        PipeReader input,
        Memory<byte> headerBuffer,
        CancellationToken cancellationToken)
    {
        // Phase 1: read FormatVersion + StorageDekVersion + ChainStepsCount (3 bytes) to learn the chain length.
        const int prefixSize = FormatVersionSize + StorageDekVersionSize + ChainStepsCountSize;

        var prefixRead = await input.ReadAtLeastAsync(
            minimumSize: prefixSize,
            cancellationToken: cancellationToken);

        var prefixBuffer = prefixRead.Buffer.Slice(0, prefixSize);

        if (!prefixBuffer.IsSingleSegment)
            throw new InvalidOperationException(
                "V2 header prefix was expected to be found in single memory segment, but it was not.");

        var formatVersion = prefixBuffer.FirstSpan[0];
        if (formatVersion != FormatVersion)
            throw new InvalidOperationException(
                $"Invalid V2 frame format version. Found 0x{formatVersion:X2}, expected 0x{FormatVersion:X2}.");

        var storageDekVersion = prefixBuffer.FirstSpan[FormatVersionSize];
        var chainStepsCount = prefixBuffer.FirstSpan[FormatVersionSize + StorageDekVersionSize];

        var totalHeaderSize = GetHeaderSize(chainStepsCount);

        if (totalHeaderSize > headerBuffer.Length)
            throw new InvalidOperationException(
                $"V2 header size {totalHeaderSize} (chain steps {chainStepsCount}) exceeds the read buffer ({headerBuffer.Length}).");

        // Phase 2: ensure the entire header is buffered, then parse the variable section.
        var fullRead = await input.ReadAtLeastAsync(
            minimumSize: totalHeaderSize,
            cancellationToken: cancellationToken);

        var fullHeader = fullRead.Buffer.Slice(0, totalHeaderSize);

        if (!fullHeader.IsSingleSegment)
            throw new InvalidOperationException(
                "V2 header was expected to be found in single memory segment, but it was not.");

        fullHeader.FirstSpan.CopyTo(headerBuffer.Span.Slice(0, totalHeaderSize));

        input.AdvanceTo(
            fullRead.Buffer.GetPosition(totalHeaderSize));

        var chainStartOffset = FormatVersionSize + StorageDekVersionSize + ChainStepsCountSize;

        var chainSalts = new byte[chainStepsCount][];
        for (var i = 0; i < chainStepsCount; i++)
        {
            var stepSalt = new byte[KeyDerivationChain.StepSaltSize];
            headerBuffer.Span
                .Slice(chainStartOffset + i * KeyDerivationChain.StepSaltSize, KeyDerivationChain.StepSaltSize)
                .CopyTo(stepSalt);
            chainSalts[i] = stepSalt;
        }

        var fileSaltOffset = chainStartOffset + chainStepsCount * KeyDerivationChain.StepSaltSize;
        var noncePrefixOffset = fileSaltOffset + SaltSize;

        return new Header(
            StorageDekVersion: storageDekVersion,
            ChainStepSalts: chainSalts,
            FileSalt: headerBuffer.Slice(fileSaltOffset, SaltSize),
            NoncePrefix: headerBuffer.Slice(noncePrefixOffset, NoncePrefixSize));
    }

    private static void VerifyInputKeyMaterialSize(int size)
    {
        if (size < DerivedKeySize)
        {
            throw new ArgumentException($"The key material must be at least {DerivedKeySize} bytes long.");
        }
    }

    public static void VerifyBufferSize(
        ReadOnlyMemory<byte> buffer,
        FilePart filePart,
        int chainStepsCount)
    {
        var expectedLength = CalculateEncryptedPartSize(
            filePart, 
            chainStepsCount);

        if (buffer.Length != expectedLength)
            throw new ArgumentException(
                $"For in-place encryption ready buffer expected length was {expectedLength} bytes, but found {buffer.Length}");
    }

    public static void VerifyPartNumberSize(
        FilePart filePart,
        long fullFileSizeInBytes,
        int chainStepsCount)
    {
        if (filePart.Number <= 0)
        {
            throw new ArgumentException("Invalid partNumber = 0. Parts should be 1-based indexed.");
        }

        var firstFilePartSize = GetFirstFilePartSizeInBytes(chainStepsCount);

        if (filePart.Number == 1)
        {
            if (fullFileSizeInBytes > firstFilePartSize)
            {
                if (filePart.SizeInBytes != firstFilePartSize)
                {
                    throw new ArgumentException(
                        $"Invalid {filePart.Number} size in bytes. " +
                        $"Expected value: {firstFilePartSize} but found {filePart.SizeInBytes}");
                }

                return;
            }

            if (filePart.SizeInBytes != fullFileSizeInBytes)
            {
                throw new ArgumentException(
                    $"Invalid {filePart.Number} size in bytes. Expected value: {fullFileSizeInBytes} but found {filePart.SizeInBytes}");
            }

            return;
        }

        var expectedNumberOfParts = GetExpectedPartsCount(fullFileSizeInBytes, chainStepsCount);

        if (filePart.Number == expectedNumberOfParts)
        {
            var remainingSize = fullFileSizeInBytes - firstFilePartSize -
                                (expectedNumberOfParts - 2) * FilePartSizeInBytes;

            if (filePart.SizeInBytes != remainingSize)
            {
                throw new ArgumentException(
                    $"Invalid {filePart.Number} size in bytes. " +
                    $"Expected value: {remainingSize} but found {filePart.SizeInBytes}");
            }

            return;
        }

        if (filePart.SizeInBytes != FilePartSizeInBytes)
        {
            throw new ArgumentException(
                $"Invalid {filePart.Number} size in bytes. " +
                $"Expected value: {FilePartSizeInBytes} but found {filePart.SizeInBytes}");
        }
    }

    public static int GetExpectedPartsCount(long fullFileSizeInBytes, int chainStepsCount)
    {
        var firstFilePartSize = GetFirstFilePartSizeInBytes(chainStepsCount);

        if (fullFileSizeInBytes < firstFilePartSize)
            return 1;

        var remainingSize = fullFileSizeInBytes - firstFilePartSize;
        return 1 + (int)Math.Ceiling((double)remainingSize / FilePartSizeInBytes);
    }

    public static int GetExpectedSegmentsCount(long fullFileSizeInBytes, int chainStepsCount)
    {
        var firstSegmentCiphertextSize = GetFirstSegmentCiphertextSize(chainStepsCount);

        if (fullFileSizeInBytes < firstSegmentCiphertextSize)
            return 1;

        var remainingSize = fullFileSizeInBytes - firstSegmentCiphertextSize;
        return 1 + (int)Math.Ceiling((double)remainingSize / SegmentsCiphertextSize);
    }

    public static int GetExpectedLastSegmentNumber(long fullFileSizeInBytes, int chainStepsCount)
    {
        return GetExpectedSegmentsCount(fullFileSizeInBytes, chainStepsCount) - 1;
    }

    private static void ComputeIvForSegment(
        ReadOnlySpan<byte> noncePrefix,
        int segmentNumberInFile,
        bool isLastSegmentInFile,
        Span<byte> iv)
    {
        Span<byte> prefixSpan = iv.Slice(0, NoncePrefixSize);
        Span<byte> segmentNumberSpan = iv.Slice(NoncePrefixSize, SegmentNumberSize);
        Span<byte> isLastSegmentSpan = iv.Slice(NoncePrefixSize + SegmentNumberSize, 1);

        noncePrefix.CopyTo(prefixSpan);
        BinaryPrimitives.WriteInt32BigEndian(segmentNumberSpan, segmentNumberInFile);
        isLastSegmentSpan[0] = isLastSegmentInFile ? IvByteForLastSegment : IvByteForIntermediateSegments;
    }
}
