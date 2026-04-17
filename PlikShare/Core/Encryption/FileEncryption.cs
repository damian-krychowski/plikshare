using PlikShare.Files.Records;
using System.IO.Pipelines;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Encryption;

public static class FileEncryption
{
    public static int CalculateBufferSize(
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

    public static ValueTask CopyPlaintextIntoBuffer(
        FileEncryptionMode encryptionMode,
        PipeReader input,
        Memory<byte> buffer,
        FilePart filePart,
        CancellationToken cancellationToken)
    {
        if (encryptionMode is NoEncryption)
        {
            return input.CopyTo(
                output: buffer,
                sizeInBytes: filePart.SizeInBytes,
                cancellationToken: cancellationToken);
        }

        if (encryptionMode is AesGcmV1Encryption)
        {
            return Aes256GcmStreamingV1.CopyIntoBufferReadyForInPlaceEncryption(
                input, 
                output: buffer,
                filePart: filePart);
        }

        if (encryptionMode is AesGcmV2Encryption v2)
        {
            return Aes256GcmStreamingV2.CopyIntoBufferReadyForInPlaceEncryption(
                input, 
                output: buffer,
                filePart: filePart,
                chainStepsCount: v2.Input.ChainStepSalts.Count);
        }

        throw new InvalidOperationException(
            $"Unsupported file encryption mode '{encryptionMode.GetType()}'.");
    }

    public static void CopyPlaintextIntoBuffer(
        FileEncryptionMode encryptionMode,
        ReadOnlySpan<byte> input,
        Memory<byte> buffer,
        FilePart filePart)
    {
        if (encryptionMode is NoEncryption)
        {
            input.CopyTo(buffer.Span);
        }
        else if (encryptionMode is AesGcmV1Encryption)
        {
            Aes256GcmStreamingV1.CopyIntoBufferReadyForInPlaceEncryption(
                input: input,
                output: buffer,
                filePart: filePart);
        }
        else if (encryptionMode is AesGcmV2Encryption v2)
        {
            Aes256GcmStreamingV2.CopyIntoBufferReadyForInPlaceEncryption(
                input: input,
                output: buffer,
                filePart: filePart,
                chainStepsCount: v2.Input.ChainStepSalts.Count);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported file encryption mode '{encryptionMode.GetType()}'.");
        }
    }


    public static void PrepareFilePartUploadBuffer(
        Memory<byte> buffer,
        long fileSizeInBytes,
        FilePart filePart,
        FileEncryptionMode encryptionMode,
        CancellationToken cancellationToken)
    {
        if (encryptionMode is NoEncryption)
            return;

        if (encryptionMode is AesGcmV1Encryption v1)
        {
            Aes256GcmStreamingV1.EncryptFilePartInPlace(
                fileAesInputs: v1.Input,
                filePart: filePart,
                fullFileSizeInBytes: fileSizeInBytes,
                inputOutputBuffer: buffer,
                cancellationToken: cancellationToken);
        }
        else if (encryptionMode is AesGcmV2Encryption v2)
        {
            Aes256GcmStreamingV2.EncryptFilePartInPlace(
                fileAesInputs: v2.Input,
                filePart: filePart,
                fullFileSizeInBytes: fileSizeInBytes,
                inputOutputBuffer: buffer,
                cancellationToken: cancellationToken);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported file encryption mode '{encryptionMode.GetType()}'.");
        }
    }
}
