namespace PlikShare.Core.Encryption;

public interface IMasterDataEncryption
{
    byte[] Encrypt(string plainText);
    string Decrypt(byte[] versionedEncryptedBytes);

    /// <summary>
    /// Fast-path AEAD encryption for secrets that live in a hot loop (e.g. presigned URL
    /// payload encoding). Uses a stretched master key derived once at process startup via
    /// PBKDF2, then cached in a pinned+mlocked <see cref="SecureBytes"/> for the rest of
    /// the process lifetime. Each call does only AES-GCM (microseconds) — no PBKDF2.
    ///
    /// Output frame differs from <see cref="Encrypt(string)"/>:
    ///   [MasterKeyId(1) | Nonce(12) | Tag(16) | Ciphertext(N)]
    /// No per-encryption salt and no iteration factor — the stretched key is already
    /// salted with a fixed domain separator at startup, and AES-GCM only needs a unique
    /// nonce per encryption, not a unique key.
    ///
    /// Use ONLY in combination with <see cref="FastDecryptBytes"/>. The two frames are
    /// incompatible with <see cref="Encrypt(string)"/> / <see cref="Decrypt(byte[])"/>.
    /// </summary>
    byte[] FastEncryptBytes(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Fast-path AEAD decryption. Reads the frame produced by <see cref="FastEncryptBytes"/>
    /// and writes the plaintext directly into the caller-supplied destination span — typical
    /// usage is the pinned buffer of a <see cref="SecureBytes"/> created via
    /// <see cref="SecureBytes.Create{TState}"/>, so plaintext never lands on the unpinned
    /// managed heap.
    ///
    /// Destination length must equal <see cref="GetFastDecryptedLength"/> for the payload.
    /// </summary>
    void FastDecryptBytes(byte[] versionedEncryptedBytes, Span<byte> destination);

    /// <summary>
    /// Returns the plaintext length that <see cref="FastDecryptBytes"/> will write, so the
    /// caller can size its destination buffer (e.g. a <see cref="SecureBytes"/>) before
    /// decrypting.
    /// </summary>
    int GetFastDecryptedLength(byte[] versionedEncryptedBytes);

    IDerivedMasterDataEncryption NewDerived();
    IDerivedMasterDataEncryption DerivedFrom(byte[] versionedEncryptedBytes);
    IDerivedMasterDataEncryption DeserializeDerived(byte[] serialized);
}