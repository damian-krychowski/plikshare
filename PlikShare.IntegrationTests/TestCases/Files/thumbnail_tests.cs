using FluentAssertions;
using PlikShare.Files.Metadata;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

/// <summary>
/// End-to-end coverage of the manual-thumbnail flow:
/// <list type="bullet">
///   <item>UploadFileThumbnailOperation — storage upload + single-tx insert-completed + replace-old.</item>
///   <item>InsertAndFinalizeThumbnailQuery — atomic insert (as completed) + hard-delete-old + enqueue cleanup.</item>
///   <item>DeleteFileThumbnailOperation — explicit delete of a variant.</item>
///   <item>GetThumbnailsQuery + GetFilePreviewDetailsQuery projection — read with latest-wins dedup.</item>
/// </list>
/// Happy-path scenarios run across the full storage × encryption matrix to catch any
/// backend-specific quirks (presigned URL signing, encrypted blob fi_metadata roundtrip,
/// chunked-vs-direct upload selection). Validation/error paths run a single HardDrive/None
/// combo — the rejection logic is storage-independent.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class thumbnail_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public thumbnail_tests(
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
    public async Task upload_small_thumbnail_should_appear_in_preview_details(
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

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var thumbnailContent = RandomBytes(512);

        //when
        var thumbnailExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: thumbnailContent,
            thumbnailFileName: "thumb-small.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        details.Thumbnails.Should().NotBeNull();
        details.Thumbnails.Should().HaveCount(1);
        details.Thumbnails![0].ExternalId.Should().Be(thumbnailExternalId);
        details.Thumbnails[0].Variant.Should().Be(ThumbnailVariant.Small);
        details.Thumbnails[0].SizeInBytes.Should().Be(thumbnailContent.Length);
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task upload_large_thumbnail_should_appear_in_preview_details(
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

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var thumbnailContent = RandomBytes(4096);

        //when
        var thumbnailExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: thumbnailContent,
            thumbnailFileName: "thumb-large.webp",
            thumbnailContentType: "image/webp",
            variant: ThumbnailVariant.Large,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        details.Thumbnails.Should().NotBeNull();
        details.Thumbnails.Should().HaveCount(1);
        details.Thumbnails![0].ExternalId.Should().Be(thumbnailExternalId);
        details.Thumbnails[0].Variant.Should().Be(ThumbnailVariant.Large);
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task upload_both_variants_should_both_appear_in_preview_details(
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

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        //when
        var smallExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(512),
            thumbnailFileName: "thumb-small.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        var largeExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(4096),
            thumbnailFileName: "thumb-large.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Large,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        details.Thumbnails.Should().NotBeNull();
        details.Thumbnails.Should().HaveCount(2);
        details.Thumbnails!.Should().ContainSingle(t =>
            t.Variant == ThumbnailVariant.Small && t.ExternalId == smallExternalId);
        details.Thumbnails!.Should().ContainSingle(t =>
            t.Variant == ThumbnailVariant.Large && t.ExternalId == largeExternalId);
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task uploading_same_variant_twice_should_replace_previous_thumbnail(
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

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var firstThumbnailContent = RandomBytes(512);
        var secondThumbnailContent = RandomBytes(1024);

        //when
        var firstExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: firstThumbnailContent,
            thumbnailFileName: "thumb-v1.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        var secondExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: secondThumbnailContent,
            thumbnailFileName: "thumb-v2.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //then — preview details shows only the new one
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        details.Thumbnails.Should().NotBeNull();
        details.Thumbnails.Should().HaveCount(1);
        details.Thumbnails![0].ExternalId.Should().Be(secondExternalId);
        details.Thumbnails[0].SizeInBytes.Should().Be(secondThumbnailContent.Length);
        firstExternalId.Should().NotBe(secondExternalId);

        //and — downloading the live thumbnail returns the second content, proving the
        //atomic finalize swapped storage objects as well as DB rows
        await WaitForFileUnlocked(secondExternalId, AppOwner);

        var downloaded = await DownloadFile(
            fileExternalId: secondExternalId,
            workspace: setup.Workspace,
            user: AppOwner);

        downloaded.Should().BeEquivalentTo(secondThumbnailContent);
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task delete_thumbnail_should_remove_it_from_preview_details(
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

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(512),
            thumbnailFileName: "thumb-small.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        var largeExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(4096),
            thumbnailFileName: "thumb-large.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Large,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //when — delete only the Small variant
        await Api.MediaProcessing.DeleteThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //then — Large stays, Small is gone
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        details.Thumbnails.Should().NotBeNull();
        details.Thumbnails.Should().HaveCount(1);
        details.Thumbnails![0].ExternalId.Should().Be(largeExternalId);
        details.Thumbnails[0].Variant.Should().Be(ThumbnailVariant.Large);
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task delete_thumbnail_for_missing_variant_should_be_idempotent(
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

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        //when — delete Small even though no thumbnail exists
        await Api.MediaProcessing.DeleteThumbnail(
            workspaceExternalId: setup.Workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        //then — preview details still empty, no error thrown
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        details.Thumbnails.Should().NotBeNull();
        details.Thumbnails.Should().BeEmpty();
    }

    // --- Error paths — single combo, validation is storage-independent ---

    [Fact]
    public async Task upload_thumbnail_for_non_image_or_video_parent_should_fail()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(256),
            fileName: "document.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        //when + then
        var act = async () => await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(512),
            thumbnailFileName: "thumb.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await act.Should()
            .ThrowAsync<TestApiCallException>()
            .Where(ex => ex.ResponseBody.Contains("parent-not-thumbnailable"));
    }

    [Fact]
    public async Task upload_thumbnail_with_non_image_extension_should_fail()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        //when + then
        var act = async () => await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(256),
            thumbnailFileName: "thumb.txt",
            thumbnailContentType: "text/plain",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await act.Should()
            .ThrowAsync<TestApiCallException>()
            .Where(ex => ex.ResponseBody.Contains("thumbnail-must-be-image"));
    }

    private static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        System.Random.Shared.NextBytes(buffer);
        return buffer;
    }
}
