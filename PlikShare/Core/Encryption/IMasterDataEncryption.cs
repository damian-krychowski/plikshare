namespace PlikShare.Core.Encryption;

public interface IMasterDataEncryption
{
    /// <summary>
    /// Fast-path AEAD encryption. Uses a stretched master key derived once at process startup via
    /// PBKDF2 and cached in a pinned+mlocked <see cref="SecureBytes"/> for the rest of the process
    /// lifetime. Each call does only AES-GCM (microseconds) — no PBKDF2.
    ///
    /// Output frame:
    ///   [FormatVersion(1)=0x01 | MasterKeyId(1) | Nonce(12) | Tag(16) | Ciphertext(N)]
    /// No per-encryption salt and no iteration factor — the stretched key is already salted with a
    /// fixed domain separator at startup, and AES-GCM only needs a unique nonce per encryption,
    /// not a unique key.
    /// </summary>
    byte[] EncryptBytes(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Allocation-free overload of <see cref="EncryptBytes(ReadOnlySpan{byte})"/>. Writes the full
    /// ciphertext frame directly into <paramref name="destination"/>, which must be exactly
    /// <see cref="GetEncryptedLength"/> bytes long. Lets callers (e.g. the Base64 helpers) place
    /// the output on the stack instead of the heap for small payloads.
    /// </summary>
    void EncryptBytes(ReadOnlySpan<byte> plaintext, Span<byte> destination);

    /// <summary>
    /// Returns the ciphertext frame length that <see cref="EncryptBytes(ReadOnlySpan{byte}, Span{byte})"/>
    /// will produce for a plaintext of the given size, so the caller can size its destination
    /// buffer (e.g. a stackalloc'd span) before encrypting.
    /// </summary>
    int GetEncryptedLength(int plaintextLength);

    /// <summary>
    /// Fast-path AEAD decryption. Reads the frame produced by <see cref="EncryptBytes(ReadOnlySpan{byte})"/>
    /// and writes the plaintext directly into the caller-supplied destination span — typical usage
    /// is the pinned buffer of a <see cref="SecureBytes"/> created via
    /// <see cref="SecureBytes.Create{TState}"/>, so plaintext never lands on the unpinned managed
    /// heap.
    ///
    /// Destination length must equal <see cref="GetDecryptedLength"/> for the payload.
    /// </summary>
    void DecryptBytes(ReadOnlySpan<byte> versionedEncryptedBytes, Span<byte> destination);

    /// <summary>
    /// Returns the plaintext length that <see cref="DecryptBytes"/> will write, so the caller
    /// can size its destination buffer (e.g. a <see cref="SecureBytes"/>) before decrypting.
    /// </summary>
    int GetDecryptedLength(ReadOnlySpan<byte> versionedEncryptedBytes);
}
