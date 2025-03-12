namespace PlikShare.Storages.Encryption;

/// <summary>
/// Versioned encryption key used in storage encryption algorithms
/// </summary>
/// <param name="Version">byte value of the given key version. Indexation starts from 0</param>
/// <param name="Ikm">Input keying material for HKDM DeriveKey method used in AES_GCM storage encryption</param>
public record StorageEncryptionKey(
    byte Version,
    byte[] Ikm);