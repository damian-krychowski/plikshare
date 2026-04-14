namespace PlikShare.Core.Encryption;

public readonly record struct FileAesInputsV1(
    byte[] Ikm,
    byte KeyVersion,
    byte[] Salt,
    byte[] NoncePrefix);

public readonly record struct FileAesInputsV2(
    byte[] Ikm,
    byte KeyVersion,
    IReadOnlyList<byte[]> ChainStepSalts,
    byte[] Salt,
    byte[] NoncePrefix);
