using System.Text;
using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.S3;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.S3LiveStorages;

/// <summary>
/// Verifies the contract of <c>GetDownloadLink</c>: the API hands out a
/// pre-signed URL, and a direct HTTP GET against that URL returns the file
/// bytes. For unencrypted / Managed storage the URL points straight at the
/// S3 endpoint; for Full encryption it points at PlikShare's internal
/// passthrough decryptor — both are exercised here.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class s3_presigned_download_url_tests : TestFixture
{
    private readonly S3LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public s3_presigned_download_url_tests(
        HostFixture8081 hostFixture,
        S3LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(S3TheoryData.AllProvidersAndEncryptionTypes),
        MemberType = typeof(S3TheoryData))]
    public async Task download_via_presigned_url_should_return_uploaded_content(
        S3StorageProvider provider,
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
