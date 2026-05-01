using PlikShare.Storages.Encryption;
using static PlikShare.IntegrationTests.Infrastructure.TestFixture;

namespace PlikShare.IntegrationTests.Infrastructure.S3;

/// <summary>
/// Long-lived storage + workspace pair shared across S3 live tests for a given
/// (provider, encryption) combination. Created once per test session by
/// <see cref="S3LiveStoragesFixture"/>; tests get isolation by creating their
/// own folder under <see cref="Workspace"/>.
/// </summary>
public sealed record S3LiveSetup(
    S3StorageProvider Provider,
    StorageEncryptionType Encryption,
    AppStorage Storage,
    AppWorkspace Workspace,
    string BucketName,
    AppSignedInUser User);
