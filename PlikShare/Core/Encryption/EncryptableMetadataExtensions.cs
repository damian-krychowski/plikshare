namespace PlikShare.Core.Encryption;

public static class EncryptableMetadataExtensions
{
    extension(WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        public string DecodeEncryptableMetadata(EncodedMetadataValue encoded)
        {
            return workspaceEncryptionSession.DecodeEncryptableMetadata(
                encoded.Encoded);
        }

        public string DecodeEncryptableMetadata(string encoded)
        {
            if (workspaceEncryptionSession is null)
                return encoded;

            return AesGcmMetadataV1.Decode(
                encoded,
                workspaceEncryptionSession);
        }
    }
}