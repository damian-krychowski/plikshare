using System.Text;
using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

/// <summary>
/// End-to-end correctness check that <see cref="StorageEncryptionType"/> actually
/// affects what lands on the underlying storage. Round-trip tests prove that bytes
/// coming back match bytes going in — they do NOT prove that anything was encrypted
/// in transit. This fixture uploads a recognisable plaintext marker, then reads the
/// raw object directly (bypassing PlikShare) via <see cref="IRawStorageClient"/>.
/// For unencrypted storage the marker must be present in the raw bytes; for Managed
/// and Full encryption it must be absent — catching regressions where the encryption
/// pipeline silently passes plaintext through.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class storage_encryption_at_rest_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_encryption_at_rest_tests(
        HostFixture8081 hostFixture,
        LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task raw_storage_object_should_match_storage_encryption_mode(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        const string Marker = "PLIKSHARE-ENCRYPTION-MARKER-XYZ-9D4E2F";
        var setup = await _liveFixture.GetOrCreate(
            this, 
            provider, 
            encryptionType, 
            AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        // Repeat the marker so it survives through any block-level processing without
        // being split awkwardly across boundaries — and to make the unencrypted
        // positive assertion trivially observable.
        var plaintext = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(Marker + "\n", 256)));
        var markerBytes = Encoding.UTF8.GetBytes(Marker);

        var uploadedFile = await UploadFile(
            content: plaintext,
            fileName: "encryption-at-rest.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(uploadedFile.ExternalId, AppOwner);

        //when
        using var rawClient = RawStorageClient.For(setup.Storage, MainVolume);

        var rawBytes = await rawClient.ReadFileBytes(
            bucketName: setup.BucketName,
            fileExternalId: uploadedFile.ExternalId);

        //then
        if (encryptionType == StorageEncryptionType.None)
        {
            ContainsSequence(rawBytes, markerBytes).Should().BeTrue(
                "raw storage object on unencrypted storage must contain the plaintext marker");
        }
        else
        {
            ContainsSequence(rawBytes, markerBytes).Should().BeFalse(
                $"raw storage object on {encryptionType} storage must NOT contain the plaintext marker");
        }
    }

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        if (needle.Length == 0) return true;
        if (haystack.Length < needle.Length) return false;

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
                return true;
        }

        return false;
    }
}
