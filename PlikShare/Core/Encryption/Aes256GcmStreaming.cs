using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Security.Cryptography;
using PlikShare.Core.Utils;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Storages.Encryption;
using Serilog;
using static PlikShare.Core.Encryption.EncryptedBytesRange;

namespace PlikShare.Core.Encryption;

/// <summary>
/// Motivation: To be able to efficiently encrypt and decrypt large files + be able to decrypt them only partially to be able to support range downloads
/// (for example to handle encrypted videos streaming) and an appropriate algorithm was needed. I found this: https://developers.google.com/tink/streaming-aead
/// and modified it slightly to contain an additional 1byte with keyVersion value to be able to implement rolling of storages encryption keys in the future.
///
/// Encryption consists of segments. Each segment can be encrypted and decrypted individually. First segment contains header with encryption details
///
/// First Segment:
/// HEADER | CIPHERTEXT | TAG
///
/// Header:
/// HEADER SIZE | KEY VERSION | SALT | NONCE PREFIX
///
/// Next Segments:
/// CIPHERTEXT | TAG
///  
/// </summary>
public static class Aes256GcmStreaming
{
    /// <summary>
    /// Size of a segment = ciphertext + tag
    /// </summary>
    public const int SegmentSize = 1 * SizeInBytes.Mb;

    /// <summary>
    /// In upload scenarios I split files in such a way that the maximum number of segments to encrypt is the value below
    /// </summary>
    public const int SegmentsPerFilePart = 10;
    public const int MaximumPayloadSize = SegmentsPerFilePart * SegmentSize;
    
    public const long FirstFilePartSizeInBytes = FirstSegmentCiphertextSize + (SegmentsPerFilePart - 1) * SegmentsCiphertextSize;
    public const long FilePartSizeInBytes = SegmentsPerFilePart * SegmentsCiphertextSize;
    
    private const int DerivedKeySize = 32;

    private const int NoncePrefixSize = 7;
    private const int SaltSize = DerivedKeySize;
    private const int KeyVersionSize = 1;

    /// <summary>
    /// Header is included only to the first segment of the file
    /// Header = HeaderSize | KeyVersion | Salt | NoncePrefix 
    /// </summary>
    private const int HeaderSize = KeyVersionSize + SaltSize + NoncePrefixSize + 1;

    private const int FirstSegmentCiphertextSize = SegmentSize - TagSize - HeaderSize;
    private const int SegmentsCiphertextSize = SegmentSize - TagSize;

    /// <summary>
    /// A 4-byte unique identifier for each segment in a sequence,
    /// embedded within the Initialization Vector (IV) to ensure IV uniqueness
    /// </summary>
    private const int SegmentNumberSize = 4;

    /// <summary>
    /// Size of the authentication tag (16 bytes) per Tink specification,
    /// used to verify ciphertext integrity and authenticity
    /// </summary>
    private const int TagSize = 16;

    /// <summary>
    /// 12-byte Initialization Vector consisting of:
    /// nonce prefix + segment number + last-segment indicator,
    /// ensuring unique IVs for each encryption operation
    /// </summary>
    private const int IvSize = NoncePrefixSize + SegmentNumberSize + 1;

    /// <summary>
    /// Flag value (0x00) added to the IV for all non-final segments
    /// </summary>
    private const byte IvByteForIntermediateSegments = 0x00;

    /// <summary>
    /// Flag value (0x01) added to the IV for the final segment,
    /// preventing truncation attacks by marking the end of the sequence
    /// </summary>
    private const byte IvByteForLastSegment = 0x01;


    public static EncryptedBytesRangeCalculator EncryptedBytesRangeCalculator = new(
        headerSize: HeaderSize,
        firstSegmentCiphertextSize: FirstSegmentCiphertextSize,
        nextSegmentsCiphertextSize: SegmentsCiphertextSize,
        tagSize: TagSize);

    public static byte[] GenerateIkm()
    {
        var randomBytes = new byte[DerivedKeySize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return randomBytes;
    }
    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);
    public static byte[] GenerateNoncePrefix() => RandomNumberGenerator.GetBytes(NoncePrefixSize);


    /// <summary>
    /// This methods prepares buffer with plain text data and free spaces for encryption header and encryption tags
    /// it will allow to reuse the same buffer for in-place encryption, and algo will only need to buffer a single chunk of
    /// plaintext - which is much more memory efficient
    /// </summary>
    public static async ValueTask CopyIntoBufferReadyForInPlaceEncryption(
        this PipeReader reader,
        Memory<byte> output, 
        int partSizeInBytes, 
        int partNumber)
    {
        VerifyBufferSize(output, partNumber, partSizeInBytes);

        var offset = partNumber == 1
            ? HeaderSize
            : 0;

        var bytesToCopyLeft = partSizeInBytes;

        var nextSegmentBytesLeft = partNumber == 1
            ? FirstSegmentCiphertextSize
            : SegmentsCiphertextSize;

        while (bytesToCopyLeft > 0)
        {
            var bytesToCopyInThisSegment = Math.Min(nextSegmentBytesLeft, bytesToCopyLeft);

            var readResult = await reader.ReadAtLeastAsync(
                minimumSize: bytesToCopyInThisSegment);

            if (readResult.Buffer.Length == 0 && readResult.IsCompleted)
                throw new InvalidOperationException(
                    "Unexpected end of input stream while copying plaintext data");

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

            // For subsequent segments after first one
            nextSegmentBytesLeft = SegmentsCiphertextSize;

            if (readResult.IsCompleted && bytesToCopyLeft > 0)
                throw new InvalidOperationException(
                    "Input stream completed before all expected plaintext was copied");
        }
    }

    public static ValueTask CopyIntoBufferReadyForInPlaceEncryption(
        this Stream stream,
        Memory<byte> output,
        int partSizeInBytes,
        int partNumber)
    {
        return CopyIntoBufferReadyForInPlaceEncryption(
            reader: PipeReader.Create(stream),
            output, 
            partSizeInBytes, 
            partNumber);
    }

    public static int CalculateSafeBufferSizeForMultiFileUploads(int totalSizeInBytes, int numberOfFiles)
    {
        //each file for sure have at least header + 1 tag
        //additionally MultiFile uploads is only one part max at once, which means 10 segments which means 10 tags
        //so if there is 1 file only that will be 9 additional tags.

        return totalSizeInBytes + numberOfFiles * (HeaderSize + TagSize) + 9 * TagSize;
    }

    public static int CalculateEncryptedPartSize(int partSizeInBytes, int partNumber)
    {
        if (partNumber == 1)
        {
            var sum = HeaderSize;
            var firstSegment = Math.Min(partSizeInBytes, FirstSegmentCiphertextSize);
            sum += firstSegment + TagSize;
            var rest = partSizeInBytes - firstSegment;

            if (rest > 0)
            {
                sum += CalculateNextSegmentsSize(rest);
            }

            return sum;
        }

        return CalculateNextSegmentsSize(partSizeInBytes);
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
        byte KeyVersion,
        ReadOnlyMemory<byte> Salt,
        ReadOnlyMemory<byte> NoncePrefix);
    
    private readonly record struct Chunk(
        int Size,
        bool IsStreamComplete);

    public static void EncryptFilePartInPlace(
        StorageEncryptionKey key,
        ReadOnlyMemory<byte> salt,
        ReadOnlyMemory<byte> noncePrefix,
        int partNumber, //part numbers indexing start from 1 (same as in S3 specification)
        int partSizeInBytes,
        long fullFileSizeInBytes,
        Memory<byte> inputOutputBuffer,
        CancellationToken cancellationToken = default)
    {
        VerifyInputKeyMaterialSize(key.Ikm.Length);
        VerifyPartNumberSize(partNumber, partSizeInBytes, fullFileSizeInBytes);
        VerifyBufferSize(inputOutputBuffer, partNumber, partSizeInBytes);

        var plaintextSegmentBufferSize = Math.Min(
            SegmentsCiphertextSize,
            partSizeInBytes);

        var encryptionHeapBufferSize = DerivedKeySize + IvSize + plaintextSegmentBufferSize;

        //all heap acquired form shared pool as one block;
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
            if (partNumber == 1)
            {
                WriteHeader(
                    keyVersion: key.Version,
                    salt: salt.Span,
                    noncePrefix: noncePrefix.Span,
                    output: inputOutputBuffer);
            }

            using var aesGcm = PrepareAesGcm(
                ikm: key.Ikm,
                salt: salt.Span,
                deriveKeyBuffer: derivedKeyBuffer.Span);

            var expectedLastSegmentNumber = GetExpectedLastSegmentNumber(
                fullFileSizeInBytes);

            var segmentNumber = (partNumber - 1) * SegmentsPerFilePart;

            var plaintextBytesLeft = partSizeInBytes;

            var offset = partNumber == 1
                ? HeaderSize
                : 0;

            while (plaintextBytesLeft > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ComputeIvForSegment(
                    noncePrefix: noncePrefix.Span,
                    segmentNumberInFile: segmentNumber,
                    isLastSegmentInFile: segmentNumber == expectedLastSegmentNumber,
                    iv: ivBuffer.Span);

                var actualChunkSize = Math.Min(
                    GetPlaintextChunkSize(
                        isFirstSegment: segmentNumber == 0),
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
            Log.Error(e, "Something went wrong while encrypting file part");
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
        ReadOnlySpan<byte> salt,
        Span<byte> deriveKeyBuffer)
    {
        HKDF.DeriveKey(
            hashAlgorithmName: HashAlgorithmName.SHA256,
            ikm: ikm,
            output: deriveKeyBuffer,
            salt: salt,
            info: null);

        return new AesGcm(
            key: deriveKeyBuffer,
            tagSizeInBytes: TagSize);
    }
    
    private static void WriteHeader(
        byte keyVersion,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> noncePrefix,
        Memory<byte> output)
    {
        WriteHeaderToSpan(
            keyVersion: keyVersion,
            salt: salt,
            noncePrefix: noncePrefix,
            outputSpan: output.Span.Slice(0, HeaderSize));
    }
    
    private static void WriteHeaderToSpan(
        byte keyVersion,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> noncePrefix,
        Span<byte> outputSpan)
    {
        outputSpan[0] = HeaderSize;
        outputSpan[1] = keyVersion;
        salt.CopyTo(outputSpan.Slice(1 + KeyVersionSize));
        noncePrefix.CopyTo(outputSpan.Slice(1 + KeyVersionSize + SaltSize));
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

        var actualSegmentSize = (int) Math.Min(
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

    private static int GetPlaintextChunkSize(bool isFirstSegment)
    {
        return isFirstSegment
            ? FirstSegmentCiphertextSize
            : SegmentsCiphertextSize;
    }

    //assumption: pipe reader need to already start from the first segment beginning
    public static async ValueTask DecryptRange(
       StorageEncryptionKeyProvider keyProvider,
       FileEncryptionMetadata encryptionMetadata,
       EncryptedBytesRange range,
       long fileSizeInBytes,
       PipeReader input,
       PipeWriter output,
       CancellationToken cancellationToken = default)
    {
        //in case of small files, if they will all fit into one segment, 
        //there is no need to declare whole 1MB of memory
        var inputBufferSize = (int) Math.Min(
            fileSizeInBytes + TagSize,
            SegmentSize);

        var decryptionHeapBufferSize = DerivedKeySize + IvSize + inputBufferSize;

        //all heap acquired form shared pool as one block;
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
        
        var noncePrefix = encryptionMetadata
            .NoncePrefix
            .AsMemory();

        try
        {
            var key = keyProvider.GetEncryptionKey(
                version: encryptionMetadata.KeyVersion);

            using var aesGcm = PrepareAesGcm(
                ikm: key.Ikm,
                salt: encryptionMetadata.Salt.AsSpan(),
                deriveKeyBuffer: derivedKeyBuffer.Span);

            var segmentNumber = range.FirstSegment.Number;

            var expectedLastSegmentNumber = GetExpectedLastSegmentNumber(
                fullFileSizeInBytes: fileSizeInBytes);
            
            while (segmentNumber <= range.LastSegment.Number)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var chunkSize = GetCiphertextChunkSize(
                    isFirstSegment: segmentNumber == 0);

                var chunk = await ReadChunk(
                    chunkSize: chunkSize,
                    input: input,
                    inputStreamBuffer: inputBuffer,
                    cancellationToken: cancellationToken);

                if (chunk is { Size: 0, IsStreamComplete: true })
                    break;

                ComputeIvForSegment(
                    noncePrefix: noncePrefix.Span,
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
                
                //when i was passing the actual cancellation token when operation was cancelled during flush it was throwing errors 
                //on the content leght - there were too many bytes written to the output - however i cannot explain why.
                //its probably because of the fact i was completing the pipewriter which had some unflashed data.
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
            Log.Error(e, "Something went wrong while decrypting file range");

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
        StorageEncryptionKeyProvider keyProvider,
        long fileSizeInBytes,
        PipeReader input,
        PipeWriter output,
        CancellationToken cancellationToken = default)
    {
        //in case of small files, if they will all fit into one segment, 
        //there is no need to declare whole 1MB of memory
        var inputBufferSize = (int) Math.Min(
            fileSizeInBytes + TagSize,
            SegmentSize);

        var decryptionHeapBufferSize = SaltSize + NoncePrefixSize + DerivedKeySize + IvSize + inputBufferSize;

        //all heap acquired form shared pool as one block;
        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: decryptionHeapBufferSize);
        
        Memory<byte> heapBufferMemory = heapBuffer.AsMemory();

        Memory<byte> headerBuffer = heapBufferMemory.Slice(
            start: 0, 
            length: SaltSize + NoncePrefixSize);
        
        Memory<byte> derivedKeyBuffer = heapBufferMemory.Slice(
            start: SaltSize + NoncePrefixSize,
            length: DerivedKeySize);
        
        Memory<byte> ivBuffer = heapBufferMemory.Slice(
            start: SaltSize + NoncePrefixSize + DerivedKeySize,
            length: IvSize);

        Memory<byte> inputBuffer = heapBufferMemory.Slice(
            start: SaltSize + NoncePrefixSize + DerivedKeySize + IvSize, 
            length: inputBufferSize);
        
        try
        {
            var header = await ReadHeader(
                input, 
                headerBuffer,
                cancellationToken);

            var key = keyProvider.GetEncryptionKey(
                version: header.KeyVersion);

            using var aesGcm = PrepareAesGcm(
                ikm: key.Ikm,
                salt: header.Salt.Span,
                deriveKeyBuffer: derivedKeyBuffer.Span);

            var expectedLastSegmentNumber = GetExpectedLastSegmentNumber(
                fullFileSizeInBytes: fileSizeInBytes);

            var segmentNumber = 0;

            while (true)
            {
                var chunkSize = GetCiphertextChunkSize(
                    isFirstSegment: segmentNumber == 0);

                var chunk = await ReadChunk(
                    chunkSize: chunkSize,
                    input: input,
                    inputStreamBuffer: inputBuffer,
                    cancellationToken: cancellationToken);
                
                if(chunk is { Size: 0, IsStreamComplete: true })
                    break;

                ComputeIvForSegment(
                    noncePrefix: header.NoncePrefix.Span,
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
            Log.Error(e, "Something went wrong while decrypting file part");
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
        bool isFirstSegment)
    {
        return SegmentSize - (isFirstSegment ? HeaderSize : 0);
    }

    private static async ValueTask<Header> ReadHeader(
        PipeReader input,
        Memory<byte> headerBuffer,
        CancellationToken cancellationToken)
    {
        var readResult = await input.ReadAtLeastAsync(
            minimumSize: HeaderSize,
            cancellationToken: cancellationToken);

        var retrievedHeader = readResult
            .Buffer
            .Slice(0, HeaderSize);

        if (!retrievedHeader.IsSingleSegment)
            throw new InvalidOperationException(
                "Header was expected to be found in single memory segment, but it was not.");

        if (retrievedHeader.FirstSpan[0] != HeaderSize)
        {
            throw new InvalidOperationException(
                $"Invalid header length. Found {retrievedHeader.FirstSpan[0]}, but expected {HeaderSize}.");
        }

        retrievedHeader
            .FirstSpan
            .Slice(2)
            .CopyTo(headerBuffer.Span.Slice(0, SaltSize + NoncePrefixSize));

        input.AdvanceTo(
            readResult.Buffer.GetPosition(HeaderSize));

        return new Header(
            KeyVersion: retrievedHeader.FirstSpan[1],
            Salt: headerBuffer.Slice(0, SaltSize),
            NoncePrefix: headerBuffer.Slice(SaltSize, NoncePrefixSize));
    }

    private static void VerifyInputKeyMaterialSize(int size)
    {
        if (size < DerivedKeySize)
        {
            throw new ArgumentException($"The key material must be at least {DerivedKeySize} bytes long.");
        }
    }

    public static void VerifyBufferSize(ReadOnlyMemory<byte> buffer, int partNumber, int partSizeInBytes)
    {
        var expectedLength = CalculateEncryptedPartSize(
            partSizeInBytes, partNumber);

        if (buffer.Length != expectedLength)
            throw new ArgumentException(
                $"For in-place encryption ready buffer expected length was {expectedLength} bytes, but found {buffer.Length}");
    }

    /// <summary>
    /// PartNumber size should be a multiplication of single segment size. it should also include
    /// that first part (1-based indexed)
    /// </summary>
    public static void VerifyPartNumberSize(int partNumber, long partSizeInBytes, long fullFileSizeInBytes)
    {
        if (partNumber <= 0)
        {
            throw new ArgumentException("Invalid partNumber = 0. Parts should be 1-based indexed.");
        }
        
        if (partNumber == 1)
        {
            if (fullFileSizeInBytes > FirstFilePartSizeInBytes)
            {
                if (partSizeInBytes != FirstFilePartSizeInBytes)
                {
                    throw new ArgumentException(
                        $"Invalid {partNumber} size in bytes. Expected value: {FirstFilePartSizeInBytes} but found {partSizeInBytes}");
                }

                return;
            }

            if (partSizeInBytes != fullFileSizeInBytes)
            {
                throw new ArgumentException(
                    $"Invalid {partNumber} size in bytes. Expected value: {fullFileSizeInBytes} but found {partSizeInBytes}");
            }

            return;
        }

        var expectedNumberOfParts = GetExpectedPartsCount(fullFileSizeInBytes);

        if (partNumber == expectedNumberOfParts)
        {
            var remainingSize = fullFileSizeInBytes - FirstFilePartSizeInBytes -
                                (expectedNumberOfParts - 2) * FilePartSizeInBytes;


            if (partSizeInBytes != remainingSize)
            {
                throw new ArgumentException(
                    $"Invalid {partNumber} size in bytes. Expected value: {remainingSize} but found {partSizeInBytes}");
            }

            return;
        }

        if (partSizeInBytes != FilePartSizeInBytes)
        {
            throw new ArgumentException(
                $"Invalid {partNumber} size in bytes. Expected value: {FilePartSizeInBytes} but found {partSizeInBytes}");
        }
    }

    public static int GetExpectedPartsCount(long fullFileSizeInBytes)
    {
        if (fullFileSizeInBytes < FirstFilePartSizeInBytes)
            return 1;

        var remainingSize = fullFileSizeInBytes - FirstFilePartSizeInBytes;
        return 1 + (int)Math.Ceiling((double)remainingSize / FilePartSizeInBytes);
    }

    public static int GetExpectedSegmentsCount(long fullFileSizeInBytes)
    {
        if (fullFileSizeInBytes < FirstSegmentCiphertextSize)
            return 1;

        var remainingSize = fullFileSizeInBytes - FirstSegmentCiphertextSize;
        return 1 + (int)Math.Ceiling((double)remainingSize / SegmentsCiphertextSize);
    }

    public static int GetExpectedLastSegmentNumber(long fullFileSizeInBytes)
    {
        return GetExpectedSegmentsCount(fullFileSizeInBytes) - 1;
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

public class EncryptedBytesRangeCalculator(
    long headerSize,
    long firstSegmentCiphertextSize,
    long nextSegmentsCiphertextSize,
    long tagSize)
{
    private readonly long _firstSegmentFullSize = headerSize + firstSegmentCiphertextSize + tagSize;
    private readonly long _nextSegmentFullSize = nextSegmentsCiphertextSize + tagSize;

    public EncryptedBytesRange FromUnencryptedRange(
        BytesRange unencryptedRange, 
        long unencryptedFileSize)
    {
        var startIndex = FindEncryptedIndex(unencryptedRange.Start);
        var endIndex = FindEncryptedIndex(unencryptedRange.End);
        var encryptedFileLastByteIndex = FindEncryptedIndex(unencryptedFileSize - 1) + tagSize;

        var firstSegment = FindSegment(startIndex, encryptedFileLastByteIndex);
        var lastSegment = FindSegment(endIndex, encryptedFileLastByteIndex);

        return new EncryptedBytesRange(
            FirstSegment: firstSegment,
            LastSegment: lastSegment,
            FirstSegmentReadOffset: (int)(startIndex - firstSegment.Start),
            LastSegmentReadOffset: (int)(endIndex - lastSegment.Start));
    }

    public Segment FindSegment(long encryptedIndex, long encryptedFileLastByteIndex)
    {
        if (encryptedIndex < firstSegmentCiphertextSize)
        {
            return new Segment(
                Number: 0,
                Start: headerSize,
                End: Math.Min(
                    _firstSegmentFullSize - 1,
                    encryptedFileLastByteIndex));
        }
        
        var remainingBytes = encryptedIndex - _firstSegmentFullSize;
        var fullSegments = remainingBytes / _nextSegmentFullSize;

        var segmentNumber = (int)fullSegments + 1;
        var segmentStart = _firstSegmentFullSize + (segmentNumber - 1) * _nextSegmentFullSize;

        return new Segment(
            Number: segmentNumber,
            Start: segmentStart,
            End: Math.Min(
                segmentStart + _nextSegmentFullSize - 1,
                encryptedFileLastByteIndex));
    }

    public long FindEncryptedIndex(long unencryptedIndex)
    {
        var offset = CalculateOffset(unencryptedIndex);

        return offset + unencryptedIndex;
    }

    private long CalculateOffset(long unencryptedIndex)
    {
        var offset = headerSize;

        if (unencryptedIndex < firstSegmentCiphertextSize)
        {
            return offset;
        }

        offset += tagSize;

        var remainingLength = unencryptedIndex - firstSegmentCiphertextSize;

        var fullNextSegments = remainingLength / nextSegmentsCiphertextSize;

        offset += fullNextSegments * tagSize;

        return offset;
    }
}

public record EncryptedBytesRange(
    Segment FirstSegment,
    Segment LastSegment,
    int FirstSegmentReadOffset,
    int LastSegmentReadOffset)
{
    public readonly record struct Segment(
        int Number,
        long Start,
        long End);
}

