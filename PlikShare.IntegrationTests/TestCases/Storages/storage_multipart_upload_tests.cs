using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_multipart_upload_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_multipart_upload_tests(
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
    public async Task multipart_upload_and_download_should_return_same_content(
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

        // 11 MB triggers multipart path: unencrypted parts are 10 MB each, so any
        // size > 10 MB requires ≥ 2 parts and forces MultiStepChunkUpload. Encrypted
        // streaming formats (V1/V2) reach multipart at smaller sizes, so this size
        // also exercises multipart for Managed and Full encryption.
        var originalContent = new byte[11 * 1024 * 1024];
        System.Random.Shared.NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "multipart-test.bin",
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
