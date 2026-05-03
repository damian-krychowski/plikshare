using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

/// <summary>
/// Exercises the third upload algorithm — <c>SingleChunkUpload</c> — that
/// neither <see cref="storage_upload_download_tests"/> (DirectUpload) nor
/// <see cref="storage_multipart_upload_tests"/> (MultiStepChunkUpload) reaches.
/// SingleChunkUpload only fires for **unencrypted** files in the size range
/// (MicroFileThreshold, UnencryptedFilePartSize] = (1 MB, 10 MB]. A 5 MB file
/// sits squarely in that band. The test runs only for <c>StorageEncryptionType.None</c>
/// because Managed and Full encryption skip this path entirely.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class storage_single_chunk_upload_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_single_chunk_upload_tests(
        HostFixture8081 hostFixture,
        LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesWithNoEncryption),
        MemberType = typeof(StorageTheoryData))]
    public async Task single_chunk_upload_and_download_should_return_same_content(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(
            this, 
            provider, 
            encryptionType, 
            AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        // 5 MB unencrypted → filePartsCount == 1, size > MicroFileThreshold (1 MB),
        // so ResolveUploadAlgorithm returns SingleChunkUpload (initiate → PUT to
        // pre-signed URL → complete part → complete upload).
        var originalContent = new byte[5 * 1024 * 1024];
        System.Random.Shared.NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "single-chunk-test.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: setup.Workspace,
            user: AppOwner);

        //then
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }
}
