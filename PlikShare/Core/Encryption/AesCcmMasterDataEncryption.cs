using System.Security.Cryptography;
using System.Text;

namespace PlikShare.Core.Encryption;

[Obsolete(message: $"Use {nameof(AesGcmMasterDataEncryption)} service instead.")]
public class AesCcmMasterDataEncryption(MasterEncryptionKeyProvider masterEncryptionKeyProvider): IMasterDataEncryption
{
    public byte[] Encrypt(string plainText)
    {
        var encryptionKey = masterEncryptionKeyProvider.GetCurrentEncryptionKey();
        
        var base64EncryptedText = AesCcmEncryptionService.Encrypt(
            plainText, 
            encryptionKey.Password);
        
        return VersionedAesCcmCiphertext.ToBytes(
            encryptionKey.Id, 
            base64EncryptedText);
    }
    
    public string Decrypt(byte[] versionedEncryptedBytes)
    {
        var versionedCiphertext = VersionedAesCcmCiphertext.FromBytes(
            versionedEncryptedBytes);
        
        var encryptionKey = masterEncryptionKeyProvider.GetEncryptionKeyById(
            versionedCiphertext.KeyId);
        
        return AesCcmEncryptionService.Decrypt(
            versionedCiphertext.Ciphertext,
            encryptionKey.Password);
    }

    public IDerivedMasterDataEncryption NewDerived()
    {
        throw new NotImplementedException();
    }

    public IDerivedMasterDataEncryption DerivedFrom(byte[] versionedEncryptedBytes)
    {
        throw new NotImplementedException();
    }

    public IDerivedMasterDataEncryption DeserializeDerived(byte[] serialized)
    {
        throw new NotImplementedException();
    }

    private static class AesCcmEncryptionService
    {
        public static byte[] Encrypt(string plainText, string password)
        {
            var salt = new byte[16];
            RandomNumberGenerator.Fill(
                salt);
            
            var encryptionKey = DeriveKey(
                password, 
                salt);
            
            using var aes = new AesCcm(encryptionKey);
        
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            RandomNumberGenerator.Fill(nonce);
            
            var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            var ciphertextBytes = new byte[plaintextBytes.Length];
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        
            aes.Encrypt(nonce, plaintextBytes, ciphertextBytes, tag);
            
            return AesCcmCiphertext.ToBytes(
                salt,
                nonce, 
                tag, 
                ciphertextBytes);
        }

        public static string Decrypt(byte[] encryptedBytes, string password)
        {
            var gcmCiphertext = AesCcmCiphertext.FromBytes(
                encryptedBytes);
            
            var encryptionKey = DeriveKey(
                password, 
                gcmCiphertext.Salt);
        
            using var aes = new AesCcm(encryptionKey);
        
            var plaintextBytes = new byte[gcmCiphertext.CiphertextBytes.Length];
        
            aes.Decrypt(gcmCiphertext.Nonce, gcmCiphertext.CiphertextBytes, gcmCiphertext.Tag, plaintextBytes);
        
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        
        private static byte[] DeriveKey(string password, byte[] salt)
        {
            using var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations: 10000, HashAlgorithmName.SHA256);
            return deriveBytes.GetBytes(32); // 256-bit key
        }
    }
    
    private class AesCcmCiphertext
    {
        public byte[] Salt { get; }
        public byte[] Nonce { get; }
        public byte[] Tag { get; }
        public byte[] CiphertextBytes { get; }

        private const int SaltLength = 16;

        private AesCcmCiphertext(byte[] salt, byte[] nonce, byte[] tag, byte[] ciphertextBytes)
        {
            Salt = salt;
            Nonce = nonce;
            Tag = tag;
            CiphertextBytes = ciphertextBytes;
        }
    
        public static AesCcmCiphertext FromBytes(byte[] encryptedBytes)
        {
            return new AesCcmCiphertext(
                encryptedBytes[..SaltLength], // Salt
                encryptedBytes[SaltLength..(SaltLength + AesGcm.NonceByteSizes.MaxSize)], // Nonce
                encryptedBytes[(SaltLength + AesGcm.NonceByteSizes.MaxSize)..(SaltLength + AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize)], // Tag
                encryptedBytes[(SaltLength + AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize)..] // Ciphertext
            );
        }

        public static byte[] ToBytes(byte[] salt, byte[] nonce, byte[] tag, byte[] ciphertextBytes)
        {
            return salt.Concat(nonce).Concat(tag).Concat(ciphertextBytes).ToArray();
        }
    }
    
    private class VersionedAesCcmCiphertext
    {
        public byte KeyId { get; }
        public byte[] Ciphertext { get; }

        private VersionedAesCcmCiphertext(byte keyId, byte[] ciphertext)
        {
            KeyId = keyId;
            Ciphertext = ciphertext;
        }
    
        public static VersionedAesCcmCiphertext FromBytes(byte[] versionedEncryptedBytes)
        {
            return new VersionedAesCcmCiphertext(
                versionedEncryptedBytes[0], 
                versionedEncryptedBytes[1..]);
        }
    
        public static byte[] ToBytes(byte keyId, byte[] encryptedBytes)
        {
            return new[] { keyId }.Concat(encryptedBytes).ToArray();
        }
    }
}