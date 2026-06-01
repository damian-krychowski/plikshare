using FluentAssertions;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Files.Metadata;
using PlikShare.Integrations.Aws.Textract.TestConfiguration;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.TestAssets;
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
            fileExternalIds: [fileA.ExternalId, fileB.ExternalId, fileC.ExternalId],
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
            fileExternalIds: uploaded.Select(f => f.ExternalId).ToList(),
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
            fileExternalIds: uploaded.Select(f => f.ExternalId).ToList(),
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
            fileExternalIds: uploaded.Select(f => f.ExternalId).ToList(),
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
