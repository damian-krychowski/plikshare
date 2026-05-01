using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.S3;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.S3LiveStorages;

/// <summary>
/// Exercises the third upload algorithm — <c>SingleChunkUpload</c> — that
/// neither <see cref="s3_upload_download_tests"/> (DirectUpload) nor
/// <see cref="s3_multipart_upload_tests"/> (MultiStepChunkUpload) reaches.
/// SingleChunkUpload only fires for **unencrypted** files in the size range
/// (MicroFileThreshold, UnencryptedFilePartSize] = (1 MB, 10 MB]. A 5 MB file
/// sits squarely in that band. The test runs only for <c>StorageEncryptionType.None</c>
/// because Managed and Full encryption skip this path entirely.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class s3_single_chunk_upload_tests : TestFixture
{
    private readonly S3LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public s3_single_chunk_upload_tests(
        HostFixture8081 hostFixture,
        S3LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(S3TheoryData.AllProvidersWithNoEncryption),
        MemberType = typeof(S3TheoryData))]
    public async Task single_chunk_upload_and_download_should_return_same_content(
        S3StorageProvider provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

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
