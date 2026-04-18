using System.Security.Cryptography;

namespace PlikShare.Core.Encryption;

public class MasterEncryptionKeyProvider
{
    /// <summary>
    /// Fixed domain separator used as the salt for the one-time PBKDF2 stretch of each
    /// master password. Using a constant string (instead of a random salt) is what lets
    /// us cache the derived key across operations and across restarts — the stretched
    /// key is deterministic for a given master password, so ciphertexts produced in a
    /// previous process lifetime are still decryptable after a restart.
    ///
    /// Security note: a fixed salt does NOT weaken PBKDF2 here. Per-encryption salts
    /// matter when the same password is used in many independent systems (defense
    /// against shared rainbow tables). This codebase has one master password per
    /// installation; the only thing an attacker with the stretched key's salt can do is
    /// brute-force THIS specific (password, salt) pair — exactly the same work as with
    /// a random salt.
    /// </summary>
    private static readonly byte[] StretchedKeySalt =
        "plikshare-master-stretch-v1\0"u8.ToArray();

    private const int StretchedKeySize = 32;
    private const int StretchedKeyIterations = 650_000;

    private readonly List<MasterEncryptionKey> _masterKeys;

    /// <summary>
    /// Per-master-key-id cache of stretched keys. Populated eagerly at construction time
    /// via PBKDF2 and kept in pinned+mlocked <see cref="SecureBytes"/> for the rest of
    /// the process lifetime. Eager init trades ~500ms × key-count of startup time for
    /// predictable first-request latency and fail-fast on bad passwords.
    ///
    /// Core performance mechanism for the fast-path AEAD in
    /// <see cref="IMasterDataEncryption.FastEncryptBytes"/> /
    /// <see cref="IMasterDataEncryption.FastDecryptBytes"/>: every encrypt/decrypt is
    /// just AES-GCM with the cached stretched key, no PBKDF2.
    /// </summary>
    private readonly Dictionary<byte, SecureBytes> _stretchedKeys;

    public MasterEncryptionKeyProvider(IList<string> encryptionPasswords)
    {
        _masterKeys = encryptionPasswords
            .Select((password, index) => ToEncryptionKey(index, password))
            .ToList();

        _stretchedKeys = _masterKeys.ToDictionary(
            keySelector: mk => mk.Id,
            elementSelector: DeriveStretchedKey);
    }

    private static SecureBytes DeriveStretchedKey(MasterEncryptionKey masterKey)
    {
        return masterKey.PasswordBytes.Use(
            static pwSpan => SecureBytes.Create(
                length: StretchedKeySize,
                state: pwSpan,
                initializer: static (output, pw) =>
                {
                    Rfc2898DeriveBytes.Pbkdf2(
                        password: pw,
                        salt: StretchedKeySalt,
                        destination: output,
                        iterations: StretchedKeyIterations,
                        hashAlgorithm: HashAlgorithmName.SHA256);
                }));
    }

    private static MasterEncryptionKey ToEncryptionKey(int index, string password)
    {
        var keyId = index + 1;

        if (keyId > byte.MaxValue)
            throw new InvalidOperationException("To many encryption passwords. Only 255 are supported;");

        return new MasterEncryptionKey(
            (byte)keyId,
            password);
    }

    public MasterEncryptionKey GetCurrentEncryptionKey() => _masterKeys.Last();
    public MasterEncryptionKey GetEncryptionKeyById(byte keyId) => _masterKeys[keyId - 1];

    /// <summary>
    /// Returns the stretched key for the given master key id. The returned
    /// <see cref="SecureBytes"/> is owned by this provider and lives for the process
    /// lifetime — callers MUST NOT dispose it. Typical usage is inside a
    /// <see cref="SecureBytes.Use"/> scope to read the key span for AES-GCM.
    /// </summary>
    public SecureBytes GetStretchedKey(byte masterKeyId) => _stretchedKeys[masterKeyId];
}