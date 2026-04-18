namespace PlikShare.Core.Encryption;

public abstract record FileEncryptionMode;

public sealed record NoEncryption : FileEncryptionMode
{
    public static NoEncryption Instance { get; } = new();
}

public sealed record AesGcmV1Encryption(
    FileAesInputsV1 Input) : FileEncryptionMode;

public sealed record AesGcmV2Encryption(
    FileAesInputsV2 Input) : FileEncryptionMode;


public sealed record FileAesInputsV1(
    byte[] Ikm,
    byte KeyVersion,
    byte[] Salt,
    byte[] NoncePrefix);

/// <summary>
/// AES inputs for V2 encryption. The Ikm is a BORROWED reference owned by the
/// WorkspaceEncryptionSession — do NOT dispose it here.
/// </summary>
public sealed record FileAesInputsV2(
    SecureBytes Ikm,
    byte KeyVersion,
    IReadOnlyList<byte[]> ChainStepSalts,
    byte[] Salt,
    byte[] NoncePrefix);

public static class FileEncryptionModeExtensions
{
    extension(FileEncryptionMode mode)
    {
        public string FormatVersion
        {
            get
            {
                return mode switch
                {
                    NoEncryption => "None",
                    AesGcmV1Encryption => "V1",
                    AesGcmV2Encryption => "V2",
                    _ => throw new ArgumentOutOfRangeException(nameof(mode))
                };
            }
        }
    }
}