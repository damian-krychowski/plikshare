using System.Text;
using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_upload_download_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_upload_download_tests(
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
    public async Task small_file_upload_and_download_should_return_same_content(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        var originalContent = Encoding.UTF8.GetBytes("Hello, PlikShare S3 integration test!");

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "test-file.txt",
            contentType: "text/plain",
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
