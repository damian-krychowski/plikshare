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
            ReadOnlySpan<byte> versionedEncryptedBytes)
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
            ReadOnlySpan<byte> versionedEncryptedBytes)
        {
            var plainText = encryption.DecryptString(
                versionedEncryptedBytes);

            return Json.Deserialize<T>(plainText) ?? throw new InvalidOperationException(
                $"Decryption and deserialization of object into {typeof(T)} failed.");
        }

        /// <summary>
        /// Encrypts UTF-8 plaintext and encodes the ciphertext frame as Base64. For payloads that
        /// fit on the stack, both the plaintext UTF-8 buffer and the ciphertext frame are stack-
        /// allocated — the only heap allocation is the final Base64 string.
        /// </summary>
        public string EncryptToBase64(
            string plainText)
        {
            var plaintextByteCount = Encoding.UTF8.GetByteCount(
                plainText);

            var ciphertextLength = encryption.GetEncryptedLength(
                plaintextByteCount);

            if (plaintextByteCount <= StackallocThreshold && ciphertextLength <= StackallocThreshold)
            {
                Span<byte> plaintextBuf = stackalloc byte[plaintextByteCount];
                Span<byte> ciphertextBuf = stackalloc byte[ciphertextLength];

                Encoding.UTF8.GetBytes(
                    plainText,
                    plaintextBuf);

                encryption.EncryptBytes(
                    plaintextBuf,
                    ciphertextBuf);

                return Convert.ToBase64String(
                    ciphertextBuf);
            }

            return Convert.ToBase64String(
                encryption.EncryptString(plainText));
        }

        /// <summary>
        /// Decodes Base64 to the encrypted frame and decrypts it back into a UTF-8 string. For
        /// payloads that fit on the stack, the Base64-decoded buffer is stack-allocated — the
        /// only heap allocation is the final plaintext string.
        /// </summary>
        public string DecryptFromBase64(
            string base64EncryptedText)
        {
            // Upper bound: every 4 Base64 chars decode to ≤ 3 bytes; padding only reduces the
            // actual count. Convert.TryFromBase64Chars writes the real length to bytesWritten.
            var maxDecodedLength = base64EncryptedText.Length / 4 * 3;

            if (maxDecodedLength <= StackallocThreshold)
            {
                Span<byte> decoded = stackalloc byte[maxDecodedLength];

                if (!Convert.TryFromBase64Chars(base64EncryptedText, decoded, out var bytesWritten))
                    throw new FormatException(
                        "Provided text is not a valid Base64-encoded payload.");

                return encryption.DecryptString(
                    decoded[..bytesWritten]);
            }

            return encryption.DecryptString(
                Convert.FromBase64String(base64EncryptedText));
        }
    }
}
