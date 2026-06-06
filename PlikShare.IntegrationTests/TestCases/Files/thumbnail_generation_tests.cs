using FluentAssertions;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Integrations.Aws.Textract.TestConfiguration;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.TestAssets;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

/// <summary>
/// End-to-end coverage of the QUEUE-DRIVEN thumbnail generation path:
/// <list type="bullet">
///   <item>Single-file enqueue → workspace owner downloads the WebP via the workspace endpoint.</item>
///   <item>Bulk enqueue (Protobuf body) for N files → all complete and download.</item>
///   <item>Video source (.mp4) → executor routes to <c>GenerateThumbnailsFromFile</c> via temp
///         file + <c>DownloadFileRange</c> (covers the &gt;8 MB → ranged path for fast-start mp4).</item>
///   <item>Box anonymous (access-code) download with token in query — <c>&lt;img src&gt;</c> can't
///         carry custom headers, so the auth handler's query fallback is exercised end-to-end.</item>
///   <item>Box anonymous without token → 401 (auth policy).</item>
///   <item>Box anonymous trying a file outside the box's folder subtree → 404
///         (<c>boxFolderId</c> guard in <c>GetThumbnailDownloadDetailsQuery</c>).</item>
///   <item>Team-member (cookie auth, /api/boxes/{boxExt}/) downloads after accepting an invitation
///         with AllowList permission.</item>
/// </list>
/// Manual-upload variants (no queue) are covered in <c>thumbnail_tests.cs</c> across the full
/// storage × encryption matrix; the queue path is storage-independent so we run a single
/// HardDrive/None combo per scenario here.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class thumbnail_generation_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public thumbnail_generation_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task workspace_owner_can_generate_and_download_mini_thumbnail_for_an_image()
    {
        //given — workspace + folder + uploaded real PNG (so ffmpeg actually has bytes to demux)
        var workspace = await CreateWorkspace(user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "sample.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        //when — enqueue Mini thumbnail generation, wait for the queue worker, download bytes
        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var terminalStatus = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            cookie: AppOwner.Cookie);

        //then — batch reports the Mini variant as ready and the bytes are a valid WebP
        terminalStatus.Completed.Should().Be(1);
        terminalStatus.Failed.Should().Be(0);
        terminalStatus.Pending.Should().Be(0);
        terminalStatus.ReadyThumbnails.Should().ContainSingle(r =>
            r.FileExternalId == parentFile.ExternalId.Value
            && r.Variants.Any(v => v.Variant == ThumbnailVariant.Mini));

        statusCode.Should().Be(200);
        body.Should().NotBeEmpty();
        IsWebp(body).Should().BeTrue("Mini thumbnails are always encoded as WebP");
    }

    [Fact]
    public async Task anonymous_box_link_visitor_can_download_thumbnail_via_query_token()
    {
        //given — workspace owner generates a Mini for a file behind a box link
        var workspace = await CreateWorkspace(user: AppOwner);

        var boxFolder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "sample.png",
            contentType: "image/png",
            folder: boxFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        var box = await CreateBox(
            folder: boxFolder,
            user: AppOwner);

        // AllowList lets the visitor see the file in the listing — the thumbnail endpoint reuses
        // that permission (if you see the file, you see its thumb).
        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(AllowList: true));

        var session = await StartBoxLinkSession();

        //when — anonymous visitor downloads via the access-codes endpoint, token in query
        var (statusCode, body) = await Api.AccessCodesApi.GetFileThumbnail(
            accessCode: boxLink.AccessCode,
            fileExternalId: parentFile.ExternalId,
            boxLinkToken: session.Token);

        //then
        statusCode.Should().Be(200);
        body.Should().NotBeEmpty();
        IsWebp(body).Should().BeTrue();
    }

    [Fact]
    public async Task anonymous_box_link_visitor_without_token_should_be_unauthorized()
    {
        //given — same shape as above but no session token at all
        var workspace = await CreateWorkspace(user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "sample.png",
            contentType: "image/png",
            folder: boxFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);

        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(AllowList: true));

        //when — visit the thumbnail URL with neither header nor query token
        var (statusCode, _) = await Api.AccessCodesApi.GetFileThumbnail(
            accessCode: boxLink.AccessCode,
            fileExternalId: parentFile.ExternalId,
            boxLinkToken: null);

        //then — the BoxLinkCookie authorization policy rejects unauthenticated calls
        statusCode.Should().Be(401);
    }

    [Fact]
    public async Task anonymous_box_link_should_not_serve_thumbnail_for_file_outside_box_folder_subtree()
    {
        //given — TWO sibling folders in the workspace; box wraps only the first. Visitor must not
        // be able to download a thumbnail for a file that lives in the OTHER folder, even with a
        // valid session token.
        var workspace = await CreateWorkspace(user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);
        var outsideFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var outsideFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "outside.png",
            contentType: "image/png",
            folder: outsideFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(outsideFile.ExternalId, AppOwner);

        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: outsideFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);

        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(AllowList: true));

        var session = await StartBoxLinkSession();

        //when — visitor requests the thumbnail of the file from the OTHER folder
        var (statusCode, _) = await Api.AccessCodesApi.GetFileThumbnail(
            accessCode: boxLink.AccessCode,
            fileExternalId: outsideFile.ExternalId,
            boxLinkToken: session.Token);

        //then — boxFolderId guard in GetThumbnailDownloadDetailsQuery rejects the lookup → 404
        statusCode.Should().Be(404);
    }

    [Fact]
    public async Task bulk_generation_should_process_every_file_in_batch_and_make_them_downloadable()
    {
        //given — three images in one workspace
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var fileA = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "a.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var fileB = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "b.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var fileC = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "c.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(fileA.ExternalId, AppOwner);
        await WaitForFileUnlocked(fileB.ExternalId, AppOwner);
        await WaitForFileUnlocked(fileC.ExternalId, AppOwner);

        //when — single bulk request (Protobuf), then wait for all 3 to finish
        var (batchId, totalFiles) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            selectedFiles: [fileA.ExternalId, fileB.ExternalId, fileC.ExternalId],
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        totalFiles.Should().Be(3);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        //then — all three completed without failure
        status.Completed.Should().Be(3);
        status.Failed.Should().Be(0);
        status.Pending.Should().Be(0);

        //and — every file's Mini thumbnail can be downloaded as a real WebP
        foreach (var file in new[] { fileA, fileB, fileC })
        {
            var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: file.ExternalId,
                cookie: AppOwner.Cookie);

            statusCode.Should().Be(200, $"file {file.ExternalId} should have a downloadable Mini thumbnail");
            body.Should().NotBeEmpty();
            IsWebp(body).Should().BeTrue();
        }
    }

    [Fact]
    public async Task workspace_owner_can_generate_thumbnail_for_a_video_file()
    {
        //given — a tiny fast-start .mp4 (1s, 64×64 red) so the executor exercises the
        // video-routing branch: DownloadFileRange + temp file + GenerateThumbnailsFromFile.
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var videoFile = await UploadFile(
            content: ThumbnailTestAssets.GetRedVideoBytes(),
            fileName: "clip.mp4",
            contentType: "video/mp4",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(videoFile.ExternalId, AppOwner);

        //when
        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: videoFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: videoFile.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        status.Completed.Should().Be(1);
        status.Failed.Should().Be(0);
        status.ReadyThumbnails.Should().ContainSingle(r =>
            r.FileExternalId == videoFile.ExternalId.Value);

        statusCode.Should().Be(200);
        IsWebp(body).Should().BeTrue();
    }

    [Fact]
    public async Task invited_team_member_can_download_thumbnail_via_box_internal_endpoint()
    {
        //given — workspace owner sets up a file + thumbnail, then invites another user as a
        // box member with AllowList. The team-member auth path goes through /api/boxes/...
        // (cookie + ValidateExternalBoxFilter), distinct from the anonymous /api/access-codes/...
        var workspace = await CreateWorkspace(user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "shared.png",
            contentType: "image/png",
            folder: boxFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);

        var teamMember = await InviteAndRegisterUser(user: AppOwner);

        var invitationResponse = await Api.Boxes.InviteMember(
            workspaceExternalId: workspace.ExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(
                MemberEmails: [teamMember.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.BoxExternalAccess.AcceptInvitation(
            boxExternalId: box.ExternalId,
            cookie: teamMember.Cookie,
            antiforgery: teamMember.Antiforgery);

        // Endpoint requires AllowList — set it explicitly so the test doesn't drift if default
        // permissions ever change.
        var memberExternalId = invitationResponse.Members.First().ExternalId;

        await Api.Boxes.UpdateMemberPermissions(
            workspaceExternalId: workspace.ExternalId,
            boxExternalId: box.ExternalId,
            memberExternalId: memberExternalId,
            request: new UpdateBoxMemberPermissionsRequestDto
            {
                AllowList = true,
                AllowDownload = false,
                AllowUpload = false,
                AllowDeleteFile = false,
                AllowRenameFile = false,
                AllowMoveItems = false,
                AllowCreateFolder = false,
                AllowRenameFolder = false,
                AllowDeleteFolder = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when — invited member fetches the thumbnail via the box-internal endpoint with
        // their own cookie (no BoxLink token involved)
        var (statusCode, body) = await Api.BoxExternalAccess.GetFileThumbnail(
            boxExternalId: box.ExternalId,
            fileExternalId: parentFile.ExternalId,
            cookie: teamMember.Cookie);

        //then
        statusCode.Should().Be(200);
        body.Should().NotBeEmpty();
        IsWebp(body).Should().BeTrue();
    }

    [Fact]
    public async Task bulk_generation_for_three_real_illustrations_should_produce_mini_thumbnails_for_each()
    {
        //given — three real multi-megapixel JPEGs uploaded into one folder. The synthetic
        // TextractTestImage is tiny; this test exercises the encoder against real bytes.
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var illustrations = ThumbnailTestAssets.AllIllustrations();
        var uploaded = new List<AppFile>(illustrations.Count);

        foreach (var (fileName, bytes) in illustrations)
        {
            var file = await UploadFile(
                content: bytes,
                fileName: fileName,
                contentType: "image/jpeg",
                folder: folder,
                workspace: workspace,
                user: AppOwner);

            await WaitForFileUnlocked(file.ExternalId, AppOwner);
            uploaded.Add(file);
        }

        //when — single bulk request for Mini across all three
        var (batchId, totalFiles) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            selectedFiles: uploaded.Select(f => f.ExternalId).ToList(),
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        totalFiles.Should().Be(3);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie,
            timeoutMs: 60_000);

        //then — all three completed and every Mini bytes-stream is a real WebP
        status.Completed.Should().Be(3);
        status.Failed.Should().Be(0);
        status.Pending.Should().Be(0);

        foreach (var file in uploaded)
        {
            var (statusCode, body) = await Api.MediaProcessing.GetFileThumbnail(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: file.ExternalId,
                cookie: AppOwner.Cookie);

            statusCode.Should().Be(200, $"file {file.ExternalId} should have a downloadable Mini thumbnail");
            body.Should().NotBeEmpty();
            IsWebp(body).Should().BeTrue();
        }
    }

    [Fact]
    public async Task bulk_generation_should_produce_all_three_variants_with_increasing_sizes_for_each_file()
    {
        //given — same three illustrations, but this time we ask the queue to produce ALL variants
        // for every file. With Mini=96h, Small=400h, Large=1600h (and these JPEGs much taller than
        // 1600), the resulting WebP sizes must strictly grow: Mini < Small < Large.
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var illustrations = ThumbnailTestAssets.AllIllustrations();
        var uploaded = new List<AppFile>(illustrations.Count);

        foreach (var (fileName, bytes) in illustrations)
        {
            var file = await UploadFile(
                content: bytes,
                fileName: fileName,
                contentType: "image/jpeg",
                folder: folder,
                workspace: workspace,
                user: AppOwner);

            await WaitForFileUnlocked(file.ExternalId, AppOwner);
            uploaded.Add(file);
        }

        //when — one bulk request, three variants
        var (batchId, totalFiles) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            selectedFiles: uploaded.Select(f => f.ExternalId).ToList(),
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small, ThumbnailVariant.Large],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        totalFiles.Should().Be(3);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie,
            timeoutMs: 90_000);

        status.Completed.Should().Be(3);
        status.Failed.Should().Be(0);
        status.Pending.Should().Be(0);

        //then — preview details for every file reports all three variants, and sizes ordered
        // Mini < Small < Large (real photo source, all variants are downscales of the same image)
        foreach (var file in uploaded)
        {
            var details = await Api.Files.GetPreviewDetails(
                workspaceExternalId: workspace.ExternalId,
                fileExternalId: file.ExternalId,
                fields: ["thumbnails"],
                cookie: AppOwner.Cookie);

            details.Thumbnails.Should().NotBeNull();
            details.Thumbnails.Should().HaveCount(3, $"file {file.ExternalId} should have Mini, Small, Large");

            var mini = details.Thumbnails!.Single(t => t.Variant == ThumbnailVariant.Mini);
            var small = details.Thumbnails!.Single(t => t.Variant == ThumbnailVariant.Small);
            var large = details.Thumbnails!.Single(t => t.Variant == ThumbnailVariant.Large);

            mini.SizeInBytes.Should().BeLessThan(small.SizeInBytes,
                $"Mini (96h) must encode smaller than Small (400h) for file {file.ExternalId}");
            small.SizeInBytes.Should().BeLessThan(large.SizeInBytes,
                $"Small (400h) must encode smaller than Large (1600h) for file {file.ExternalId}");
        }
    }

    [Fact]
    public async Task bulk_generation_should_report_every_file_in_ready_thumbnails_for_each_variant()
    {
        //given — three illustrations, asking for two variants. The status endpoint's
        // ReadyThumbnails should list each file once with the variants it produced; we check
        // that the queue worker actually reported per-file completion to the status pipeline
        // (not just the batch-level Completed count).
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var illustrations = ThumbnailTestAssets.AllIllustrations();
        var uploaded = new List<AppFile>(illustrations.Count);

        foreach (var (fileName, bytes) in illustrations)
        {
            var file = await UploadFile(
                content: bytes,
                fileName: fileName,
                contentType: "image/jpeg",
                folder: folder,
                workspace: workspace,
                user: AppOwner);

            await WaitForFileUnlocked(file.ExternalId, AppOwner);
            uploaded.Add(file);
        }

        //when
        var (batchId, _) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            selectedFiles: uploaded.Select(f => f.ExternalId).ToList(),
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie,
            timeoutMs: 60_000);

        //then — every file present in ReadyThumbnails with both variants
        status.ReadyThumbnails.Should().HaveCount(3);

        foreach (var file in uploaded)
        {
            var ready = status.ReadyThumbnails.Should().ContainSingle(r =>
                r.FileExternalId == file.ExternalId.Value).Subject;

            var variants = ready.Variants.Select(v => v.Variant).ToHashSet();
            variants.Should().Contain([ThumbnailVariant.Mini, ThumbnailVariant.Small]);
        }
    }

    [Fact]
    public async Task regenerating_same_variant_should_hard_delete_the_previous_thumbnail()
    {
        //given — a (default None-encryption) workspace whose file already has a queue-generated
        // Mini. Duplicate cleanup is wired only for None/Managed, where the worker can read the
        // thumbnail's variant from plaintext fi_metadata without a WorkspaceEncryptionSession.
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "sample.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var firstBatchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: firstBatchId,
            cookie: AppOwner.Cookie);

        var firstDetails = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        var firstThumbnailExternalId = firstDetails.Thumbnails!.Single().ExternalId;
        CountThumbnailRows(parentFile.ExternalId).Should().Be(1);

        //when — regenerate the SAME variant
        var secondBatchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: secondBatchId,
            cookie: AppOwner.Cookie);

        //then — exactly one Mini row survives in the DB. Asserting against the raw rows (not the
        // preview) matters: GetFilePreviewDetailsQuery dedupes by variant and would report 1 even
        // if the old duplicate were still present. The surviving row is a brand-new file.
        CountThumbnailRows(parentFile.ExternalId).Should().Be(1);

        var secondDetails = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        var secondThumbnail = secondDetails.Thumbnails!.Single();
        secondThumbnail.Variant.Should().Be(ThumbnailVariant.Mini);
        secondThumbnail.ExternalId.Should().NotBe(firstThumbnailExternalId);
    }

    [Fact]
    public async Task regenerating_one_variant_should_not_delete_the_other_variants()
    {
        //given — a file with BOTH Mini and Small generated
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "sample.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var firstBatchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini, ThumbnailVariant.Small],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: firstBatchId,
            cookie: AppOwner.Cookie);

        CountThumbnailRows(parentFile.ExternalId).Should().Be(2);

        var firstDetails = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        var smallExternalId = firstDetails.Thumbnails!
            .Single(t => t.Variant == ThumbnailVariant.Small)
            .ExternalId;

        //when — regenerate ONLY the Mini variant
        var secondBatchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: secondBatchId,
            cookie: AppOwner.Cookie);

        //then — still two rows (per-variant scoping replaced Mini only); Small is the same row,
        // Mini is a new one.
        CountThumbnailRows(parentFile.ExternalId).Should().Be(2);

        var secondDetails = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        secondDetails.Thumbnails.Should().HaveCount(2);
        secondDetails.Thumbnails!
            .Single(t => t.Variant == ThumbnailVariant.Small)
            .ExternalId.Should().Be(smallExternalId, "regenerating Mini must not touch Small");
    }

    [Fact]
    public async Task generating_a_thumbnail_should_recalculate_workspace_size_to_include_it()
    {
        //given — a workspace with a single uploaded image. Once the upload's debounced size-update
        // job settles, the reported workspace size equals the parent file's size and nothing else.
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var parentFileBytes = TextractTestImage.GetBytes();

        var parentFile = await UploadFile(
            content: parentFileBytes,
            fileName: "sample.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var sizeBeforeThumbnails = await PollWorkspaceSize(
            workspace: workspace,
            isSettled: size => size == parentFileBytes.Length,
            eachLoopAction: () => Clock.SetToNow());

        sizeBeforeThumbnails.Should().Be(parentFileBytes.Length);
        
        //when — generate a Mini thumbnail and read back its stored size
        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            fields: ["thumbnails"],
            cookie: AppOwner.Cookie);

        var thumbnailSize = details.Thumbnails!.Single().SizeInBytes;
        thumbnailSize.Should().BeGreaterThan(0);

        //then — the thumbnail bytes are stored as a child file, so the recalculated workspace size
        // must grow by exactly the thumbnail's size.
        var expectedSize = parentFileBytes.Length + thumbnailSize;

        var sizeAfterThumbnail = await PollWorkspaceSize(
            workspace: workspace,
            isSettled: size => size == expectedSize,
            eachLoopAction: () => Clock.SetToNow());

        sizeAfterThumbnail.Should().Be(
            expectedSize,
            "the freshly generated thumbnail's bytes must be reflected in the recalculated workspace size");
    }

    [Fact]
    public async Task bulk_generate_resolves_selected_folder_recursively_and_honors_excluded_file()
    {
        //given — folder tree: folder > subfolder, a thumbnailable PNG in each, plus one excluded PNG
        var workspace = await CreateWorkspace(user: AppOwner);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var subfolder = await CreateFolder(parent: folder, workspace: workspace, user: AppOwner);

        var rootFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "root.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var nestedFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "nested.png",
            contentType: "image/png",
            folder: subfolder,
            workspace: workspace,
            user: AppOwner);

        var excludedFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "skip.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(rootFile.ExternalId, AppOwner);
        await WaitForFileUnlocked(nestedFile.ExternalId, AppOwner);
        await WaitForFileUnlocked(excludedFile.ExternalId, AppOwner);

        //when — select the top folder (resolved recursively), exclude one file
        var (_, totalFiles) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            selectedFolders: [folder.ExternalId],
            excludedFiles: [excludedFile.ExternalId]);

        //then — root + nested resolved recursively, excluded file dropped
        totalFiles.Should().Be(2);
    }

    [Fact]
    public async Task bulk_generate_excludes_subfolder_subtree()
    {
        //given — folder > subfolder, a thumbnailable PNG in each
        var workspace = await CreateWorkspace(user: AppOwner);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var subfolder = await CreateFolder(parent: folder, workspace: workspace, user: AppOwner);

        var rootFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "root.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var nestedFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "nested.png",
            contentType: "image/png",
            folder: subfolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(rootFile.ExternalId, AppOwner);
        await WaitForFileUnlocked(nestedFile.ExternalId, AppOwner);

        //when — select the top folder but exclude the subfolder subtree
        var (_, totalFiles) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            selectedFolders: [folder.ExternalId],
            excludedFolders: [subfolder.ExternalId]);

        //then — only the root-folder file; the subfolder subtree is pruned
        totalFiles.Should().Be(1);
    }

    [Fact]
    public async Task full_encryption_thumbnail_job_redacts_ephemeral_keys_in_completed_queue()
    {
        //given — a FULL-encryption workspace; its thumbnail jobs carry eph:-wrapped keys in
        // q_definition. After completion those must be redacted in qc_queue_completed so the
        // archived JSON never holds the (otherwise dead) ephemeral key ciphertext.
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);
        
        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "sample.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        //when — generate a thumbnail (full encryption requires the workspace encryption session)
        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: parentFile.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then — the archived definition must carry redaction markers, never raw eph: ciphertext
        var completedDefinitions = ReadCompletedQueueDefinitions(batchId);

        completedDefinitions.Should().NotBeEmpty(
            "the finished thumbnail job must be archived in qc_queue_completed");

        var joined = string.Join("\n", completedDefinitions);

        joined.Should().Contain(
            "eph:[redacted]",
            "full-encryption thumbnail jobs wrap keys as eph: values which must be redacted on completion");

        joined.Should().NotMatchRegex(
            "eph:[A-Za-z0-9+/=]",
            "no raw eph: ciphertext may survive in the completed queue — only eph:[redacted]");
    }

    [Fact]
    public async Task count_thumbnailable_files_resolves_selection_filters_non_media_and_honors_exclusion()
    {
        //given — a folder with two thumbnailable PNGs and one non-thumbnailable .txt
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var png1 = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "a.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var png2 = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "b.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var txt = await UploadFile(
            content: "not an image"u8.ToArray(),
            fileName: "note.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(png1.ExternalId, AppOwner);
        await WaitForFileUnlocked(png2.ExternalId, AppOwner);
        await WaitForFileUnlocked(txt.ExternalId, AppOwner);

        //when — count the whole folder, then count again excluding one PNG
        var countAll = await Api.MediaProcessing.CountThumbnailableFiles(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            selectedFolders: [folder.ExternalId]);

        var countExcluded = await Api.MediaProcessing.CountThumbnailableFiles(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            selectedFolders: [folder.ExternalId],
            excludedFiles: [png1.ExternalId]);

        //then — only the two PNGs are thumbnailable (the .txt is filtered); exclusion drops one
        countAll.FileCount.Should().Be(2, "only the two PNGs are thumbnailable — the .txt is filtered out");
        countAll.TotalSizeInBytes.Should().BeGreaterThan(0);

        countExcluded.FileCount.Should().Be(1, "excluding one PNG leaves a single thumbnailable file");
    }

    [Fact]
    public async Task bulk_generate_on_folder_with_existing_thumbnail_skips_the_derived_file_and_does_not_crash()
    {
        //given — a folder with an image that already has a Mini thumbnail. The thumbnail is a
        // derived fi_files row (fi_parent_file_id set) that inherits the image's fi_folder_id, so a
        // naive folder expansion would pull it back in as a source — and re-generating would then
        // hit a FOREIGN KEY failure when the old thumbnail is hard-deleted. This guards that path.
        var workspace = await CreateWorkspace(user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var image = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "pic.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(image.ExternalId, AppOwner);

        var firstBatch = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: image.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: firstBatch,
            cookie: AppOwner.Cookie);

        //when — bulk generate on the FOLDER, which now also contains the thumbnail as a derived file
        var (batchId, totalFiles) = await Api.MediaProcessing.GenerateFileThumbnailsBulk(
            workspaceExternalId: workspace.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            selectedFolders: [folder.ExternalId]);

        var status = await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        //then — only the source image is a candidate (the derived thumbnail is skipped) and the
        // batch finishes without the FK crash that hit when thumbnails were treated as sources
        totalFiles.Should().Be(1, "the existing thumbnail (a derived file) must not be a generation source");
        status.Completed.Should().Be(1);
        status.Failed.Should().Be(0);
    }

    // Reads every qc_queue_completed.qc_definition archived for a batch straight from the DB,
    // so the assertion sees exactly what persisted after the job left q_queue.
    private List<string> ReadCompletedQueueDefinitions(Guid batchId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT qc_definition
                     FROM qc_queue_completed
                     WHERE qc_batch_id = $batchId
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$batchId", batchId)
            .Execute();
    }

    // Polls the workspace's reported CurrentSizeInBytes until it satisfies the predicate (or a
    // ~10s timeout elapses), returning the last observed value. The cached size is recalculated
    // asynchronously via a debounced queue job, so a direct read right after a mutation is racy.
    private async Task<long> PollWorkspaceSize(
        AppWorkspace workspace,
        Func<long, bool> isSettled,
        Action eachLoopAction)
    {
        var lastObserved = -1L;

        for (var i = 0; i < 100; i++)
        {
            lastObserved = (await Api.Workspaces.GetDetails(
                workspace.ExternalId,
                AppOwner.Cookie)).CurrentSizeInBytes;

            if (isSettled(lastObserved))
                return lastObserved;
            
            await Task.Delay(100);

            eachLoopAction();
        }

        return lastObserved;
    }

    // Counts the live thumbnail child rows of a parent straight from the DB — bypassing the
    // preview's by-variant dedup so a leaked duplicate is actually visible to the assertion.
    private int CountThumbnailRows(FileExtId parentFileExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var counts = connection
            .Cmd(
                sql: """
                     SELECT COUNT(*)
                     FROM fi_files AS child_fi
                     INNER JOIN fi_files AS parent_fi
                         ON parent_fi.fi_id = child_fi.fi_parent_file_id
                     WHERE
                         parent_fi.fi_external_id = $parentExternalId
                         AND child_fi.fi_deleted_at IS NULL
                         AND child_fi.fi_is_upload_completed = TRUE
                         AND child_fi.fi_metadata IS NOT NULL
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$parentExternalId", parentFileExternalId.Value)
            .Execute();

        return counts[0];
    }

    // WebP file signature: bytes 0..3 = "RIFF", bytes 8..11 = "WEBP". Detects whether the queue
    // produced an actual WebP image (not, say, an HTML error page or empty body).
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
