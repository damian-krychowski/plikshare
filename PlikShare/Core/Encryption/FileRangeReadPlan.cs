using PlikShare.Files.PreSignedLinks.RangeRequests;

namespace PlikShare.Core.Encryption;

public readonly record struct FileRangeReadPlan(
    BytesRange StorageRange,
    EncryptedBytesRange? EncryptedRange);