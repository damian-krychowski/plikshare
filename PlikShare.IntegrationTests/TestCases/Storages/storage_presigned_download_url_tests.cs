using System.Text;
using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

/// <summary>
/// Verifies the contract of <c>GetDownloadLink</c>: the API hands out a
/// pre-signed URL, and a direct HTTP GET against that URL returns the file
/// bytes. For S3-backed unencrypted / Managed storage the URL points straight
/// at the cloud endpoint; for S3 Full encryption and for hard-drive / Azure
/// it points at PlikShare's internal passthrough endpoint — all paths are
/// exercised here.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class storage_presigned_download_url_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_presigned_download_url_tests(
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
    public async Task download_via_presigned_url_should_return_uploaded_content(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        var originalContent = Encoding.UTF8.GetBytes("Direct fetch via pre-signed URL");

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "presigned-download.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(uploadedFile.ExternalId, AppOwner);

        //when
        var downloadLinkResponse = await Api.Files.GetDownloadLink(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            contentDisposition: "attachment",
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        var presignedUrl = downloadLinkResponse.DownloadPreSignedUrl;
        presignedUrl.Should().NotBeNullOrEmpty();

        var downloadedContent = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: presignedUrl,
            cookie: AppOwner.Cookie);

        //then
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }
}
