using System.Text;

namespace PlikShare.Core.Encryption;

public class MasterEncryptionKey(
    byte id,
    string password)
{
    public byte Id { get; } = id;
    public string Password { get; } = password;

    public ReadOnlyMemory<byte> PasswordBytes { get; } = Encoding.UTF8.GetBytes(password);
}