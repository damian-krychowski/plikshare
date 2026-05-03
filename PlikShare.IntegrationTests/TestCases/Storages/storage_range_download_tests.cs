using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_range_download_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_range_download_tests(
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
    public async Task range_request_should_return_exact_byte_slice(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        var originalContent = new byte[5 * 1024 * 1024];
        System.Random.Shared.NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-source.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(uploadedFile.ExternalId, AppOwner);

        var downloadLinkResponse = await Api.Files.GetDownloadLink(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            contentDisposition: "attachment",
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        const long rangeStart = 1024;
        const long rangeEnd = 2047;
        var expectedSlice = originalContent[(int)rangeStart..((int)rangeEnd + 1)];

        //when
        var rangeResponse = await Api.PreSignedFiles.DownloadFileRange(
            preSignedUrl: downloadLinkResponse.DownloadPreSignedUrl,
            rangeStart: rangeStart,
            rangeEnd: rangeEnd,
            cookie: AppOwner.Cookie);

        //then
        rangeResponse.StatusCode.Should().Be(206);
        rangeResponse.Content.Should().BeEquivalentTo(expectedSlice);
        rangeResponse.Content.Length.Should().Be((int)(rangeEnd - rangeStart + 1));
    }
}
