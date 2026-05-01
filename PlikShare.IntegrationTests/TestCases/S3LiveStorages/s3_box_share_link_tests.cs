using System.Text;
using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.S3;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.S3LiveStorages;

[Collection(IntegrationTestsCollection.Name)]
public class s3_box_share_link_tests : TestFixture
{
    private readonly S3LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public s3_box_share_link_tests(
        HostFixture8081 hostFixture,
        S3LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(S3TheoryData.AllProvidersWithoutFullEncryption),
        MemberType = typeof(S3TheoryData))]
    public async Task anonymous_visitor_should_download_file_via_box_link(
        S3StorageProvider provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(
            this, 
            provider, 
            encryptionType, 
            AppOwner);

        var boxFolder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        var originalContent = Encoding.UTF8.GetBytes("public file via box share link");

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "shared.txt",
            contentType: "text/plain",
            folder: boxFolder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(uploadedFile.ExternalId, AppOwner);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);

        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true));

        //when
        var session = await StartBoxLinkSession();

        var downloadLinkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: uploadedFile.ExternalId,
            contentDisposition: "attachment",
            boxLinkToken: session.Token);

        var downloadedContent = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: downloadLinkResponse.DownloadPreSignedUrl,
            cookie: null);

        //then
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }
}
