using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

/// <summary>
/// Coverage of the image-dimensions backfill counting/status surface:
/// <list type="bullet">
///   <item>GET …/media/image-dimensions/backfill/count — images-without-dimensions count.</item>
///   <item>PATCH …/media-processing-policy/image-dimensions — toggling returns the backfill batch
///   it kicks off (or none).</item>
///   <item>GET …/media/image-dimensions/backfill — server-discovered active-backfill snapshot.</item>
/// </list>
/// Each test runs against its OWN freshly-created workspace because the count is workspace-wide —
/// reusing a shared workspace would let other tests' uploads pollute it. The counting logic is a
/// plain DB query (storage-independent), so HardDrive/None is sufficient; one Full-encryption case
/// covers the encrypted fi_content_type decode path. Assertions only touch the deterministic,
/// in-request responses (the backfill itself runs asynchronously on the queue).
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class image_dimensions_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public image_dimensions_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task backfill_count_returns_number_of_images_without_dimensions()
    {
        //given
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        await UploadJpegImages(3, folder, workspace);

        //when
        var count = await Api.MediaProcessing.CountImageDimensionsBackfill(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        count.FileCount.Should().Be(3);
    }

    [Fact]
    public async Task backfill_count_excludes_non_image_files()
    {
        //given
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        await UploadJpegImages(2, folder, workspace);

        var textFile = await UploadFile(
            content: RandomBytes(256),
            fileName: "notes.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(textFile.ExternalId, AppOwner);

        //when
        var count = await Api.MediaProcessing.CountImageDimensionsBackfill(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        //then — only the two images count
        count.FileCount.Should().Be(2);
    }

    [Fact]
    public async Task backfill_count_is_zero_for_workspace_without_images()
    {
        //given
        var (workspace, _) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        //when
        var count = await Api.MediaProcessing.CountImageDimensionsBackfill(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        count.FileCount.Should().Be(0);
    }

    [Fact]
    public async Task backfill_count_works_on_full_encrypted_workspace()
    {
        //given — content type is stored encrypted, so the count has to decode it with the session
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.Full);

        await UploadJpegImages(2, folder, workspace);

        //when
        var count = await Api.MediaProcessing.CountImageDimensionsBackfill(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        count.FileCount.Should().Be(2);
    }

    [Fact]
    public async Task enabling_policy_returns_backfill_batch_for_existing_images()
    {
        //given
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        await UploadJpegImages(3, folder, workspace);

        //when
        var response = await Api.Workspaces.UpdateImageDimensionsPolicy(
            externalId: workspace.ExternalId,
            extractOnUpload: true,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — a backfill of the three existing images was kicked off
        response.BatchId.Should().NotBeNull();
        Guid.TryParse(response.BatchId, out _).Should().BeTrue();
        response.TotalFiles.Should().Be(3);
    }

    [Fact]
    public async Task enabling_policy_without_existing_images_returns_no_batch()
    {
        //given
        var (workspace, _) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        //when
        var response = await Api.Workspaces.UpdateImageDimensionsPolicy(
            externalId: workspace.ExternalId,
            extractOnUpload: true,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — nothing to backfill
        response.BatchId.Should().BeNull();
        response.TotalFiles.Should().Be(0);
    }

    [Fact]
    public async Task backfill_status_is_empty_when_no_backfill_running()
    {
        //given — a workspace whose policy was never enabled
        var (workspace, _) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        //when
        var status = await Api.MediaProcessing.GetImageDimensionsBackfillStatus(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        status.BatchId.Should().BeNull();
        status.Total.Should().Be(0);
        status.Completed.Should().Be(0);
        status.Failed.Should().Be(0);
        status.Pending.Should().Be(0);
    }

    [Fact]
    public async Task disabling_policy_returns_no_batch()
    {
        //given — policy enabled (with a backfill in flight)
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        await UploadJpegImages(2, folder, workspace);

        await Api.Workspaces.UpdateImageDimensionsPolicy(
            externalId: workspace.ExternalId,
            extractOnUpload: true,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — disabling cancels the remaining work and reports no new batch
        var response = await Api.Workspaces.UpdateImageDimensionsPolicy(
            externalId: workspace.ExternalId,
            extractOnUpload: false,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.BatchId.Should().BeNull();
        response.TotalFiles.Should().Be(0);
    }

    private async Task<(AppWorkspace Workspace, AppFolder Folder)> CreateWorkspaceWithFolder(
        StorageEncryptionType encryptionType)
    {
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: encryptionType);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        return (workspace, folder);
    }

    private async Task<List<AppFile>> UploadJpegImages(
        int count,
        AppFolder folder,
        AppWorkspace workspace)
    {
        var uploaded = new List<AppFile>(count);

        for (var i = 0; i < count; i++)
        {
            var file = await UploadFile(
                content: RandomBytes(2048),
                fileName: $"image-{i}.jpg",
                contentType: "image/jpeg",
                folder: folder,
                workspace: workspace,
                user: AppOwner);

            uploaded.Add(file);
        }

        await WaitForFilesUnlocked(
            uploaded.Select(f => f.ExternalId).ToList(),
            AppOwner);

        return uploaded;
    }

    private static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        System.Random.Shared.NextBytes(buffer);
        return buffer;
    }
}
