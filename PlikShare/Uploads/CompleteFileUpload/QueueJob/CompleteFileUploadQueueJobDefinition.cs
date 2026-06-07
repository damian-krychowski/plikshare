using PlikShare.Core.Encryption;

namespace PlikShare.Uploads.CompleteFileUpload.QueueJob;

public record CompleteFileUploadQueueJobDefinition(
    int FileUploadId,
    FullEncryptionSeedEphemeral? EncryptionSeed);
