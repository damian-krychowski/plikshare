using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using static PlikShare.IntegrationTests.Infrastructure.TestFixture;

namespace PlikShare.IntegrationTests.Infrastructure.Storage;

/// <summary>
/// Long-lived storage + workspace pair shared across live storage tests for a given
/// (provider, encryption) combination. Created once per test session by
/// <see cref="LiveStoragesFixture"/>; tests get isolation by creating their
/// own folder under <see cref="Workspace"/>.
/// </summary>
public sealed record LiveStorageSetup(
    StorageType Provider,
    StorageEncryptionType Encryption,
    AppStorage Storage,
    AppWorkspace Workspace,
    string BucketName,
    AppSignedInUser User);
