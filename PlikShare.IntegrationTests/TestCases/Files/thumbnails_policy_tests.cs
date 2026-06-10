using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.Preview.GetDetails.Contracts;
using PlikShare.Integrations.Aws.Textract.TestConfiguration;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.MediaProcessing.Generation;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Files;

/// <summary>
/// End-to-end coverage of the workspace THUMBNAILS POLICY (generate on upload + backfill):
/// <list type="bullet">
///   <item>PATCH …/media-processing-policy/thumbnails — round-trip through workspace details,
///   validation (enabled requires variants), owner-only access, audit-log entry.</item>
///   <item>Enabling the policy backfills existing images, but ONLY the variants each image is
///   missing — manually uploaded thumbnails must never be regenerated or replaced.</item>
///   <item>GET …/media/thumbnails/backfill/count — per-variant missing-thumbnails count.</item>
///   <item>Widening the variant set while enabled backfills just the new variant; re-saving an
///   unchanged policy is a no-op (no batch).</item>
///   <item>Uploading an image with the policy enabled generates the selected variants
///   automatically (ThumbnailsFileCreatedHandler); non-images and disabled policy enqueue
///   nothing — asserted against the queue tables, since the handler runs transactionally with
///   upload completion.</item>
///   <item>One Full-encryption case covers both the backfill and the on-upload seed paths.</item>
/// </list>
/// Generation itself is storage-independent (covered across the matrix in thumbnail_tests.cs),
/// so HardDrive storage is sufficient here. Real PNG bytes are used wherever ffmpeg actually
/// runs; count-only tests get away with random ".jpg" bytes because selection is content-type
/// based.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class thumbnails_policy_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public thumbnails_policy_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task policy_round_trips_through_workspace_details()
    {
        //given
        var workspace = await CreateWorkspace(user: AppOwner);

        //when — enable with two variants (empty workspace, so no backfill is started)
        var enableResponse = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Large],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        enableResponse.BatchId.Should().BeNull();
        enableResponse.TotalFiles.Should().Be(0);

        var details = await Api.Workspaces.GetDetails(workspace.ExternalId, AppOwner.Cookie);

        details.MediaProcessingPolicy.Thumbnails.GenerateOnUpload.Should().BeTrue();
        details.MediaProcessingPolicy.Thumbnails.Variants.Should().BeEquivalentTo(
            [ThumbnailVariant.Mini, ThumbnailVariant.Large]);

        //when — disable; the variant selection is kept so re-enabling restores it
        await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: false,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Large],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var detailsAfterDisable = await Api.Workspaces.GetDetails(workspace.ExternalId, AppOwner.Cookie);

        detailsAfterDisable.MediaProcessingPolicy.Thumbnails.GenerateOnUpload.Should().BeFalse();
        detailsAfterDisable.MediaProcessingPolicy.Thumbnails.Variants.Should().BeEquivalentTo(
            [ThumbnailVariant.Mini, ThumbnailVariant.Large]);
    }

    [Fact]
    public async Task enabling_policy_without_variants_is_rejected()
    {
        //given
        var workspace = await CreateWorkspace(user: AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(() =>
            Api.Workspaces.UpdateThumbnailsPolicy(
                externalId: workspace.ExternalId,
                generateOnUpload: true,
                variants: [],
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(400);
        apiError.ResponseBody.Should().Contain("thumbnail-variants-required");
    }

    [Fact]
    public async Task workspace_member_who_is_not_owner_cannot_update_policy()
    {
        //given — a workspace with an invited (non-owner) member
        var workspace = await CreateWorkspace(user: AppOwner);

        var member = await InviteAndRegisterUser(user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        //when — the member (who can use the workspace) tries to change the workspace-wide policy
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(() =>
            Api.Workspaces.UpdateThumbnailsPolicy(
                externalId: workspace.ExternalId,
                generateOnUpload: true,
                variants: [ThumbnailVariant.Mini],
                cookie: member.Cookie,
                antiforgery: member.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(400);
        apiError.ResponseBody.Should().Contain("not-workspace-owner");
    }

    [Fact]
    public async Task updating_policy_writes_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(user: AppOwner);

        //when
        await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.ThumbnailsPolicyUpdated>(
            expectedEventType: AuditLogEventTypes.Workspace.ThumbnailsPolicyUpdated,
            assertDetails: details =>
            {
                details.GenerateOnUpload.Should().BeTrue();
                details.Variants.Should().BeEquivalentTo(
                    [ThumbnailVariant.Mini, ThumbnailVariant.Small]);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task enabling_policy_backfills_existing_images()
    {
        //given — two real PNGs uploaded before the policy exists
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        var fileA = await UploadPngImage("a.png", folder, workspace);
        var fileB = await UploadPngImage("b.png", folder, workspace);

        //when
        var response = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — a backfill batch covering both images was kicked off
        response.BatchId.Should().NotBeNull();
        response.TotalFiles.Should().Be(2);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: Guid.Parse(response.BatchId!),
            cookie: AppOwner.Cookie);

        status.Completed.Should().Be(2);
        status.Failed.Should().Be(0);

        //and — both Mini thumbnails are downloadable WebPs
        foreach (var file in new[] { fileA, fileB })
        {
            var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: file.ExternalId,
                cookie: AppOwner.Cookie);

            statusCode.Should().Be(200, $"file {file.ExternalId} should have a backfilled Mini thumbnail");
            IsWebp(body).Should().BeTrue();
        }
    }

    [Fact]
    public async Task backfill_count_returns_images_missing_selected_variants_and_skips_non_images()
    {
        //given — three "images" (count is content-type based, so random bytes suffice) + one txt
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        for (var i = 0; i < 3; i++)
        {
            var image = await UploadFile(
                content: RandomBytes(2048),
                fileName: $"image-{i}.jpg",
                contentType: "image/jpeg",
                folder: folder,
                workspace: workspace,
                user: AppOwner);

            await WaitForFileUnlocked(image.ExternalId, AppOwner);
        }

        var textFile = await UploadFile(
            content: "not an image"u8.ToArray(),
            fileName: "notes.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(textFile.ExternalId, AppOwner);

        //when
        var count = await Api.MediaProcessing.CountThumbnailsBackfill(
            workspaceExternalId: workspace.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie);

        //then — only the three images are missing the Mini variant
        count.FileCount.Should().Be(3);
    }

    [Fact]
    public async Task backfill_generates_only_missing_variants_and_never_touches_manual_thumbnails()
    {
        //given — image A gets a MANUAL Mini thumbnail; image B has none
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        var fileA = await UploadPngImage("a.png", folder, workspace);
        var fileB = await UploadPngImage("b.png", folder, workspace);

        var manualMiniExternalId = await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: workspace.ExternalId,
            parentFileExternalId: fileA.ExternalId,
            thumbnailContent: TextractTestImage.GetBytes(),
            thumbnailFileName: "manual-mini.png",
            thumbnailContentType: "image/png",
            variant: ThumbnailVariant.Mini,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //and — the count already respects the manual thumbnail
        var miniOnlyCount = await Api.MediaProcessing.CountThumbnailsBackfill(
            workspaceExternalId: workspace.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie);

        miniOnlyCount.FileCount.Should().Be(1, "only file B is missing a Mini thumbnail");

        //when — enable with Mini + Small: A is missing only Small, B is missing both
        var response = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        response.TotalFiles.Should().Be(2);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: Guid.Parse(response.BatchId!),
            cookie: AppOwner.Cookie);

        //then — both files end up with Mini + Small …
        var detailsA = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: fileA.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        var detailsB = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: fileB.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        detailsA.Thumbnails.Should().HaveCount(2);
        detailsB.Thumbnails.Should().HaveCount(2);

        //… but A's Mini is STILL the manually uploaded row — the backfill generated Small only
        detailsA.Thumbnails!
            .Single(t => t.Variant == ThumbnailVariant.Mini)
            .ExternalId.Should().Be(
                manualMiniExternalId,
                "the backfill must never regenerate or replace an existing thumbnail");
    }

    [Fact]
    public async Task widening_variants_backfills_only_the_new_variant_and_resaving_is_a_noop()
    {
        //given — policy enabled with Mini, backfill of the single image done
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        var file = await UploadPngImage("a.png", folder, workspace);

        var enableResponse = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: Guid.Parse(enableResponse.BatchId!),
            cookie: AppOwner.Cookie);

        var miniExternalId = (await Api.Files.GetPreviewDetails(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: file.ExternalId,
                fields: ["thumbnails"],
                cookie: AppOwner.Cookie))
            .Thumbnails!
            .Single()
            .ExternalId;

        //when — widen the variant set to Mini + Small
        var widenResponse = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — a batch was started for the missing Small only
        widenResponse.BatchId.Should().NotBeNull();
        widenResponse.TotalFiles.Should().Be(1);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: Guid.Parse(widenResponse.BatchId!),
            cookie: AppOwner.Cookie);

        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        details.Thumbnails.Should().HaveCount(2);
        details.Thumbnails!
            .Single(t => t.Variant == ThumbnailVariant.Mini)
            .ExternalId.Should().Be(miniExternalId, "the already-generated Mini must stay untouched");

        //and — re-saving the unchanged policy starts no batch
        var resaveResponse = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        resaveResponse.BatchId.Should().BeNull();
        resaveResponse.TotalFiles.Should().Be(0);
    }

    [Fact]
    public async Task image_uploaded_with_policy_enabled_gets_thumbnails_automatically()
    {
        //given — empty workspace with the policy enabled for Mini + Small
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — a real PNG is uploaded (the handler enqueues generation on upload completion)
        var file = await UploadPngImage("uploaded.png", folder, workspace);

        //then — both variants appear without any explicit generate call
        var details = await WaitForThumbnails(
            workspace: workspace,
            fileExternalId: file.ExternalId,
            expectedCount: 2);

        details.Thumbnails!
            .Select(t => t.Variant)
            .Should().BeEquivalentTo([ThumbnailVariant.Mini, ThumbnailVariant.Small]);

        var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            cookie: AppOwner.Cookie);

        statusCode.Should().Be(200);
        IsWebp(body).Should().BeTrue();
    }

    [Fact]
    public async Task upload_enqueues_no_generation_for_non_images_or_when_policy_disabled()
    {
        //given — workspace A with the policy enabled, workspace B without it
        var (workspaceWithPolicy, folderWithPolicy) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);
        var (workspaceWithoutPolicy, folderWithoutPolicy) = await CreateWorkspaceWithFolder(StorageEncryptionType.None);

        await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspaceWithPolicy.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — a non-image lands in A, an image lands in B
        var textFile = await UploadFile(
            content: "not an image"u8.ToArray(),
            fileName: "notes.txt",
            contentType: "text/plain",
            folder: folderWithPolicy,
            workspace: workspaceWithPolicy,
            user: AppOwner);

        await WaitForFileUnlocked(textFile.ExternalId, AppOwner);

        var image = await UploadPngImage("pic.png", folderWithoutPolicy, workspaceWithoutPolicy);

        //then — the handler runs transactionally with upload completion, so right after the files
        //unlock the queue verdict is final: no generation job was ever enqueued in either workspace
        CountThumbnailGenerationJobs(workspaceWithPolicy.ExternalId).Should().Be(0);
        CountThumbnailGenerationJobs(workspaceWithoutPolicy.ExternalId).Should().Be(0);
    }

    [Fact]
    public async Task full_encryption_workspace_backfills_and_generates_on_upload()
    {
        //given — a Full-encryption workspace with one pre-existing image
        var (workspace, folder) = await CreateWorkspaceWithFolder(StorageEncryptionType.Full);

        var existingFile = await UploadPngImage("existing.png", folder, workspace);

        //when — enabling the policy (with the live encryption session) backfills it
        var response = await Api.Workspaces.UpdateThumbnailsPolicy(
            externalId: workspace.ExternalId,
            generateOnUpload: true,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        response.TotalFiles.Should().Be(1);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: Guid.Parse(response.BatchId!),
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        status.Completed.Should().Be(1);
        status.Failed.Should().Be(0);

        //and — a fresh upload generates its thumbnail on the fly (per-variant ephemeral seeds path)
        var uploadedFile = await UploadPngImage("uploaded.png", folder, workspace);

        await WaitForThumbnails(
            workspace: workspace,
            fileExternalId: uploadedFile.ExternalId,
            expectedCount: 1);

        //then — both thumbnails decrypt and download as WebPs
        foreach (var file in new[] { existingFile, uploadedFile })
        {
            var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: file.ExternalId,
                cookie: AppOwner.Cookie,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            statusCode.Should().Be(200, $"file {file.ExternalId} should have a downloadable Mini thumbnail");
            IsWebp(body).Should().BeTrue();
        }
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

    private async Task<AppFile> UploadPngImage(
        string fileName,
        AppFolder folder,
        AppWorkspace workspace)
    {
        var file = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: fileName,
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(file.ExternalId, AppOwner);

        return file;
    }

    // On-upload generation jobs carry no batchId (nothing to track batch-wise), so completion is
    // observed through the file's preview details instead of the batch-status endpoint.
    private async Task<GetFilePreviewDetailsResponseDto> WaitForThumbnails(
        AppWorkspace workspace,
        FileExtId fileExternalId,
        int expectedCount,
        int timeoutMs = 30_000,
        int pollIntervalMs = 100)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (true)
        {
            var details = await Api.Files.GetPreviewDetails(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: fileExternalId,
                fields: ["thumbnails"],
                cookie: AppOwner.Cookie,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            if ((details.Thumbnails?.Count ?? 0) >= expectedCount)
                return details;

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException(
                    $"File {fileExternalId} did not get {expectedCount} thumbnail(s) within {timeoutMs} ms — " +
                    $"last seen: {details.Thumbnails?.Count ?? 0}");

            await Task.Delay(pollIntervalMs);
        }
    }

    // Counts every generate-image-thumbnails job the workspace has ever had — still queued or
    // already completed. The on-upload handler enqueues in the same transaction that completes
    // the upload, so once the file is unlocked a zero here proves nothing was ever scheduled.
    private int CountThumbnailGenerationJobs(WorkspaceExtId workspaceExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var counts = connection
            .Cmd(
                sql: """
                     SELECT
                         (SELECT COUNT(*)
                          FROM q_queue
                          WHERE q_job_type = $jobType
                            AND q_workspace_id = (SELECT w_id FROM w_workspaces WHERE w_external_id = $workspaceExternalId))
                         +
                         (SELECT COUNT(*)
                          FROM qc_queue_completed
                          WHERE qc_job_type = $jobType
                            AND qc_workspace_id = (SELECT w_id FROM w_workspaces WHERE w_external_id = $workspaceExternalId))
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$jobType", GenerateImageThumbnailsJobType.Value)
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .Execute();

        return counts[0];
    }

    private static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        System.Random.Shared.NextBytes(buffer);
        return buffer;
    }

    private static bool IsWebp(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
            return false;

        return bytes[0] == (byte)'R'
            && bytes[1] == (byte)'I'
            && bytes[2] == (byte)'F'
            && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W'
            && bytes[9] == (byte)'E'
            && bytes[10] == (byte)'B'
            && bytes[11] == (byte)'P';
    }
}
