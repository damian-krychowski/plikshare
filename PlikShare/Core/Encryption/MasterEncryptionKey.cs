using System.Text;

namespace PlikShare.Core.Encryption;

public class MasterEncryptionKey : IDisposable
{
    public byte Id { get; }
    public SecureBytes PasswordBytes { get; }

    public MasterEncryptionKey(byte id, string password)
    {
        Id = id;

        var passwordBytes = Encoding.UTF8.GetBytes(password);

        try
        {
            PasswordBytes = SecureBytes.CopyFrom(passwordBytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public void Dispose() => PasswordBytes.Dispose();

    public override string ToString() => $"MasterEncryptionKey#{Id}";
}