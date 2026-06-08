namespace PlikShare.Core.Encryption;

public static class EncodedMetadataExtensions
{
    extension(WorkspaceEncryptionSession? workspaceEncryptionSession)
    {
        public string DecodeMetadata(EncodedMetadataValue encoded)
        {
            return workspaceEncryptionSession.DecodeMetadata(
                encoded.Encoded);
        }

        public string DecodeMetadata(string encoded)
        {
            if (workspaceEncryptionSession is null)
                return encoded;

            return AesGcmMetadataV1.Decode(
                encoded,
                workspaceEncryptionSession);
        }
    }
}