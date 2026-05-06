using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Encryption;

public static class MasterDataEncryptionExtensions
{
    private const int StackallocThreshold = 1024;

    extension(IMasterDataEncryption encryption)
    {
        public byte[] EncryptString(
            string plainText)
        {
            var byteCount = Encoding.UTF8.GetByteCount(
                plainText);

            if (byteCount <= StackallocThreshold)
            {
                Span<byte> buffer = stackalloc byte[byteCount];

                Encoding.UTF8.GetBytes(
                    plainText, 
                    buffer);

                return encryption.EncryptBytes(
                    buffer);
            }

            var rented = ArrayPool<byte>.Shared.Rent(
                byteCount);

            try
            {
                var span = rented.AsSpan(
                    0, 
                    byteCount);

                Encoding.UTF8.GetBytes(
                    plainText, 
                    span);

                return encryption.EncryptBytes(
                    span);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    rented.AsSpan(0, byteCount));

                ArrayPool<byte>.Shared.Return(
                    rented);
            }
        }

        public string DecryptString(
            byte[] versionedEncryptedBytes)
        {
            var plaintextLength = encryption.GetDecryptedLength(
                versionedEncryptedBytes);

            if (plaintextLength == 0)
                return string.Empty;

            if (plaintextLength <= StackallocThreshold)
            {
                Span<byte> buffer = stackalloc byte[plaintextLength];

                encryption.DecryptBytes(
                    versionedEncryptedBytes, 
                    buffer);

                return Encoding.UTF8.GetString(
                    buffer);
            }

            var rented = ArrayPool<byte>.Shared.Rent(
                plaintextLength);

            try
            {
                var span = rented.AsSpan(
                    0, 
                    plaintextLength);

                encryption.DecryptBytes(
                    versionedEncryptedBytes, 
                    span);

                return Encoding.UTF8.GetString(
                    span);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(
                    rented.AsSpan(0, plaintextLength));

                ArrayPool<byte>.Shared.Return(
                    rented);
            }
        }

        public byte[] EncryptJson<T>(
            T item)
        {
            return encryption.EncryptString(Json.Serialize(item));
        }

        public T DecryptJson<T>(
            byte[] versionedEncryptedBytes)
        {
            var plainText = encryption.DecryptString(
                versionedEncryptedBytes);

            return Json.Deserialize<T>(plainText) ?? throw new InvalidOperationException(
                $"Decryption and deserialization of object into {typeof(T)} failed.");
        }

        public string EncryptToBase64(
            string plainText)
        {
            return Convert.ToBase64String(encryption.EncryptString(plainText));
        }

        public string DecryptFromBase64(
            string base64EncryptedText)
        {
            return encryption.DecryptString(Convert.FromBase64String(base64EncryptedText));
        }
    }
}
