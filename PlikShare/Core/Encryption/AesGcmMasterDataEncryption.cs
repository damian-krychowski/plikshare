using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PlikShare.Core.Encryption;

public class AesGcmMasterDataEncryption(MasterEncryptionKeyProvider masterEncryptionKeyProvider): IMasterDataEncryption
{
    private const int MasterKeyIdSize = 1;
    private const int SaltSize = 16;
    private const int TagSize = 16;
    private const int NonceSize = 12;
    private const int IterationsFactorSize = 2;
    private const int EncryptionKeySize = 32; // 256-bit key

    private const int IterationsFactorWeight = 10000;
    private const int IterationsCountForNewEncryption = 650000;

    public byte[] Encrypt(string plainText)
    {
        var masterKey = masterEncryptionKeyProvider
            .GetCurrentEncryptionKey();

        Span<byte> salt = stackalloc byte[SaltSize];
        Span<byte> encryptionKey = stackalloc byte[EncryptionKeySize];

        RandomNumberGenerator.Fill(salt);
        
        Rfc2898DeriveBytes.Pbkdf2(
            password: masterKey.PasswordBytes.Span,
            salt: salt,
            destination: encryptionKey,
            iterations: IterationsCountForNewEncryption,
            hashAlgorithm: HashAlgorithmName.SHA256);

        return Encrypt(
            plainText: plainText, 
            masterKeyId: masterKey.Id, 
            salt: salt, 
            encryptionKey: encryptionKey);
    }

    private static byte[] Encrypt(
        string plainText,
        byte masterKeyId,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> encryptionKey)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);

        var versionedEncryptedBytesSize = 
            MasterKeyIdSize 
            + IterationsFactorSize 
            + SaltSize 
            + NonceSize 
            + TagSize 
            + plaintextBytes.Length;

        var versionedEncryptedBytes = new byte[versionedEncryptedBytesSize];
        var versionedEncryptedBytesSpan = versionedEncryptedBytes.AsSpan();

        using var aes = new AesGcm(
            key: encryptionKey,
            tagSizeInBytes: TagSize);

        var position = 0;

        //1. MasterKeyId
        versionedEncryptedBytesSpan[position] = masterKeyId;
        position += MasterKeyIdSize;

        //2. IterationsFactor
        var iterationsFactorValue = (ushort)(IterationsCountForNewEncryption / IterationsFactorWeight);

        BinaryPrimitives.WriteUInt16LittleEndian(
            versionedEncryptedBytesSpan.Slice(position, IterationsFactorSize),
            iterationsFactorValue);

        position += IterationsFactorSize;

        //3. Salt
        salt.CopyTo(versionedEncryptedBytesSpan.Slice(position, SaltSize));
        position += SaltSize;

        //4. Nonce
        var nonceSpan = versionedEncryptedBytesSpan.Slice(position, NonceSize);
        RandomNumberGenerator.Fill(nonceSpan);
        position += NonceSize;

        //5. Tag
        var tagSpan = versionedEncryptedBytesSpan.Slice(position, TagSize);
        position += TagSize;

        //6. Ciphertext
        var ciphertextSpan = versionedEncryptedBytesSpan.Slice(position, plaintextBytes.Length);
        
        aes.Encrypt(
            nonce: nonceSpan, 
            plaintext: plaintextBytes, 
            ciphertext: ciphertextSpan, 
            tag: tagSpan);

        return versionedEncryptedBytes;
    }

    public string Decrypt(byte[] versionedEncryptedBytes)
    {
        var gcmCiphertext = AesGcmCiphertextStr.FromBytes(
            versionedEncryptedBytes: versionedEncryptedBytes);

        var masterKey = masterEncryptionKeyProvider.GetEncryptionKeyById(
            gcmCiphertext.MasterKeyId);

        Span<byte> encryptionKey = stackalloc byte[EncryptionKeySize];

        Rfc2898DeriveBytes.Pbkdf2(
            password: masterKey.PasswordBytes.Span,
            salt: gcmCiphertext.Salt,
            destination: encryptionKey,
            iterations: gcmCiphertext.Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);
        
        return Decrypt(encryptionKey, gcmCiphertext);
    }

    private static string Decrypt(
        ReadOnlySpan<byte> encryptionKey,
        AesGcmCiphertextStr gcmCiphertext)
    {
        var heapBuffer = ArrayPool<byte>.Shared.Rent(
            minimumLength: gcmCiphertext.Ciphertext.Length);

        var plaintextBuffer = heapBuffer.AsSpan().Slice(
            start: 0,
            length: gcmCiphertext.Ciphertext.Length);

        try
        {
            using var aes = new AesGcm(
                key: encryptionKey,
                tagSizeInBytes: TagSize);
            
            aes.Decrypt(
                nonce: gcmCiphertext.Nonce,
                ciphertext: gcmCiphertext.Ciphertext,
                tag: gcmCiphertext.Tag,
                plaintext: plaintextBuffer);

            return Encoding.UTF8.GetString(
                plaintextBuffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                array: heapBuffer);
        }
    }

    public IDerivedMasterDataEncryption NewDerived()
    {
        var masterKey = masterEncryptionKeyProvider
            .GetCurrentEncryptionKey();

        var salt = new byte[SaltSize];
        var encryptionKey = new byte[EncryptionKeySize];
        
        RandomNumberGenerator.Fill(salt);
        
        Rfc2898DeriveBytes.Pbkdf2(
            password: masterKey.PasswordBytes.Span,
            salt: salt,
            destination: encryptionKey.AsSpan(),
            iterations: IterationsCountForNewEncryption,
            hashAlgorithm: HashAlgorithmName.SHA256);

        return new AesGcmDerivedMasterDataEncryption(
            masterKeyId: masterKey.Id,
            salt: salt,
            encryptionKey: encryptionKey);
    }

    public IDerivedMasterDataEncryption DerivedFrom(byte[] versionedEncryptedBytes)
    {
        var gcmCiphertext = AesGcmCiphertextStr.FromBytes(
            versionedEncryptedBytes: versionedEncryptedBytes);

        var masterKey = masterEncryptionKeyProvider.GetEncryptionKeyById(
            gcmCiphertext.MasterKeyId);

        var encryptionKey = new byte[EncryptionKeySize];

        Rfc2898DeriveBytes.Pbkdf2(
            password: masterKey.PasswordBytes.Span,
            salt: gcmCiphertext.Salt,
            destination: encryptionKey.AsSpan(),
            iterations: gcmCiphertext.Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256);
        
        return new AesGcmDerivedMasterDataEncryption(
            masterKeyId: masterKey.Id,
            salt: gcmCiphertext.Salt.ToArray(),
            encryptionKey: encryptionKey);
    }

    public IDerivedMasterDataEncryption DeserializeDerived(byte[] serialized)
    {
        return AesGcmDerivedMasterDataEncryption.Deserialize(serialized);
    }
    
    private class AesGcmDerivedMasterDataEncryption(
        byte masterKeyId,
        byte[] salt,
        byte[] encryptionKey) : IDerivedMasterDataEncryption
    {
        public byte[] Encrypt(string plainText)
        {
            return AesGcmMasterDataEncryption.Encrypt(
                plainText,
                masterKeyId,
                salt,
                encryptionKey);
        }

        public string Decrypt(byte[] versionedEncryptedBytes)
        {
            var gcmCiphertext = AesGcmCiphertextStr.FromBytes(
                versionedEncryptedBytes);

            if (!gcmCiphertext.Salt.SequenceEqual(salt) || gcmCiphertext.MasterKeyId != masterKeyId)
            {
                throw new InvalidOperationException(
                    "Cannot decrypt provided bytes with DerivedMasterDataEncryption because they do not belong to the correct family.");
            }

            return AesGcmMasterDataEncryption.Decrypt(
                encryptionKey,
                gcmCiphertext);
        }

        public byte[] Serialize()
        {
            var bytes = new byte[1 + salt.Length + encryptionKey.Length];

            var bytesSpan = bytes.AsSpan();

            bytesSpan[0] = masterKeyId;
            salt.CopyTo(bytesSpan.Slice(1, salt.Length));
            encryptionKey.CopyTo(bytesSpan.Slice(1 + salt.Length, encryptionKey.Length));

            return bytes;
        }

        public static AesGcmDerivedMasterDataEncryption Deserialize(byte[] serialized)
        {
            var span = serialized.AsSpan();

            return new AesGcmDerivedMasterDataEncryption(
                masterKeyId: span[0],
                salt: span.Slice(1, SaltSize).ToArray(),
                encryptionKey: span.Slice(1 + SaltSize).ToArray());
        }
    }

    private readonly ref struct AesGcmCiphertextStr
    {
        public byte MasterKeyId { get; private init; }
        public ReadOnlySpan<byte> IterationsFactor { get; private init; }
        public ReadOnlySpan<byte> Salt { get; private init; }
        public ReadOnlySpan<byte> Nonce { get; private init; }
        public ReadOnlySpan<byte> Tag { get; private init; }
        public ReadOnlySpan<byte> Ciphertext { get; private init; }
        
        public int Iterations => BinaryPrimitives.ReadUInt16LittleEndian(IterationsFactor) * IterationsFactorWeight;

        public static AesGcmCiphertextStr FromBytes(Span<byte> versionedEncryptedBytes)
        {
            var masterKeyIdEnd = 1;
            var masterKeyId = versionedEncryptedBytes[0];

            var iterationsFactorEnd = masterKeyIdEnd + IterationsFactorSize;
            var iterationsFactor = versionedEncryptedBytes[masterKeyIdEnd..iterationsFactorEnd];

            var saltEnd = iterationsFactorEnd + SaltSize;
            var salt = versionedEncryptedBytes[iterationsFactorEnd..saltEnd];

            var nonceEnd = saltEnd + NonceSize;
            var nonce = versionedEncryptedBytes[saltEnd..nonceEnd];

            var tagEnd = nonceEnd + TagSize;
            var tag = versionedEncryptedBytes[nonceEnd..tagEnd];

            var ciphertext = versionedEncryptedBytes[tagEnd..];

            return new AesGcmCiphertextStr
            {
                MasterKeyId = masterKeyId,
                IterationsFactor = iterationsFactor,
                Salt = salt,
                Nonce = nonce,
                Tag = tag,
                Ciphertext = ciphertext
            };
        }
    }
}