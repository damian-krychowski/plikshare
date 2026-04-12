namespace PlikShare.Core.Encryption;

public class FullEncryptionSession
{
    public const string HttpContextName = "FullEncryptionSession";

    public required byte[] Kek { get; init; }
}