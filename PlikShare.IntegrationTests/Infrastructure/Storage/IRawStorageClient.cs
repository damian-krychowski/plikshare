using PlikShare.Files.Id;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Test-only abstraction over a storage backend's raw object layer (S3, hard-drive,
/// Azure Blob). Used by tests to verify properties of the at-rest bytes — e.g. that a
/// file uploaded with Full encryption does not contain plaintext markers, or that a
/// file is physically gone after bulk-delete. Each backend addresses files differently
/// (S3 key with secret-part suffix vs. hard-drive filename equal to the external id),
/// so callers should not have to think about the key format — they pass the
/// <see cref="FileExtId"/> and the implementation resolves the storage-specific path.
/// </summary>
public interface IRawStorageClient : IDisposable
{
    Task<byte[]> ReadFileBytes(
        string bucketName,
        FileExtId fileExternalId,
        CancellationToken cancellationToken = default);

    Task WaitForFileGone(
        string bucketName,
        FileExtId fileExternalId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
