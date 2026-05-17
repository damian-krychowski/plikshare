using System.IO.Compression;
using System.Text;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.QuickShareExternalAccess.Contracts;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Create.Contracts;
using PlikShare.QuickShares.Id;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.Id;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.QuickShares;

[Collection(IntegrationTestsCollection.Name)]
public class quick_share_external_access_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public quick_share_external_access_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    // --- GetInfo ---

    [Fact]
    public async Task get_info_should_return_basic_info_for_anonymous_visitor()
    {
        //given
        var share = await CreateShare(name: "public");

        //when
        var result = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);

        //then
        result.Info.Should().NotBeNull();
        result.Info!.Name.Should().Be("public");
        result.Info.Mode.Should().Be(QuickShareMode.Browser);
        result.Info.AllowIndividualFileDownload.Should().BeTrue();
        result.Info.RequiresPassword.Should().BeFalse();
        result.Info.IsUnlocked.Should().BeTrue();
        result.Info.IsExpired.Should().BeFalse();
        result.Info.IsExhausted.Should().BeFalse();
        result.Info.IsOwnerPreview.Should().BeFalse();
        result.Info.DownloadsCount.Should().Be(0);
    }

    [Fact]
    public async Task get_info_for_password_protected_share_should_require_password()
    {
        //given
        var share = await CreateShare(password: "Secret123");

        //when
        var result = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);

        //then
        result.Info!.RequiresPassword.Should().BeTrue();
        result.Info.IsUnlocked.Should().BeFalse();
    }

    [Fact]
    public async Task get_info_for_expired_share_should_set_is_expired()
    {
        //given
        Clock.SetToNow();
        var share = await CreateShare(expiresAt: Clock.UtcNow.AddMinutes(10));
        Clock.CurrentTime(Clock.UtcNow.AddHours(1));

        //when
        var result = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);

        //then
        result.Info!.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task get_info_for_exhausted_share_should_set_is_exhausted()
    {
        //given
        var share = await CreateShare(maxDownloads: 1);

        await ExternalBulkDownload(share);

        //when
        var result = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);

        //then
        result.Info!.IsExhausted.Should().BeTrue();
        result.Info.DownloadsCount.Should().Be(1);
    }

    [Fact]
    public async Task get_info_with_invalid_slug_should_fail()
    {
        //when
        var result = await Api.QuickShareExternalAccess.GetInfo(
            slug: "does-not-exist",
            allowErrors: true);

        //then
        result.Info.Should().BeNull();
        result.StatusCode.Should().Be(400);
        result.ResponseBody.Should().Contain("invalid-quick-share-slug");
    }

    [Fact]
    public async Task get_info_for_owner_should_set_is_owner_preview()
    {
        //given
        var share = await CreateShare();

        //when
        var result = await Api.QuickShareExternalAccess.GetInfo(
            slug: share.Slug,
            authCookie: AppOwner.Cookie);

        //then
        result.Info!.IsOwnerPreview.Should().BeTrue();
    }

    // --- Unlock ---

    [Fact]
    public async Task unlock_with_correct_password_should_succeed()
    {
        //given
        var share = await CreateShare(password: "right-pass");
        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var result = await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "right-pass"),
            antiforgery: antiforgery);

        //then
        result.StatusCode.Should().Be(200);

        var info = await Api.QuickShareExternalAccess.GetInfo(
            slug: share.Slug,
            sessionCookie: result.SessionCookie);

        info.Info!.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task unlock_with_wrong_password_should_fail()
    {
        //given
        var share = await CreateShare(password: "right-pass");
        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var result = await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "wrong-pass"),
            antiforgery: antiforgery,
            allowErrors: true);

        //then
        result.StatusCode.Should().Be(401);
        result.ResponseBody.Should().Contain("quick-share-wrong-password");
    }

    [Fact]
    public async Task unlock_for_share_without_password_should_fail()
    {
        //given
        var share = await CreateShare();
        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var result = await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "any"),
            antiforgery: antiforgery,
            allowErrors: true);

        //then
        result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task unlock_for_expired_share_should_fail()
    {
        //given
        Clock.SetToNow();
        var share = await CreateShare(password: "p", expiresAt: Clock.UtcNow.AddMinutes(5));
        Clock.CurrentTime(Clock.UtcNow.AddHours(1));

        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var result = await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "p"),
            antiforgery: antiforgery,
            allowErrors: true);

        //then
        result.StatusCode.Should().Be(410);
        result.ResponseBody.Should().Contain("quick-share-expired");
    }

    [Fact]
    public async Task unlock_for_exhausted_share_should_fail()
    {
        //given
        var share = await CreateShare(password: "p", maxDownloads: 1);
        await UnlockAndBulkDownload(share, password: "p");

        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var result = await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "p"),
            antiforgery: antiforgery,
            allowErrors: true);

        //then
        result.StatusCode.Should().Be(410);
        result.ResponseBody.Should().Contain("quick-share-download-limit-reached");
    }

    [Fact]
    public async Task successful_unlock_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateShare(password: "right");
        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "right"),
            antiforgery: antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.Unlocked>(
            expectedEventType: AuditLogEventTypes.QuickShare.Unlocked,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
            });
    }

    [Fact]
    public async Task failed_unlock_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateShare(password: "right");
        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: "wrong"),
            antiforgery: antiforgery,
            allowErrors: true);

        //then
        await AssertAuditLogContains<Audit.QuickShare.UnlockFailed>(
            expectedEventType: AuditLogEventTypes.QuickShare.UnlockFailed,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
            },
            expectedSeverity: AuditLogSeverities.Warning);
    }

    // --- GetContent ---

    [Fact]
    public async Task get_content_should_return_files_in_share()
    {
        //given
        var share = await CreateShare();

        //when
        var content = await Api.QuickShareExternalAccess.GetContent(slug: share.Slug);

        //then
        content.Files.Should().HaveCount(1);
        content.Files[0].Name.Should().Be("hello");
        content.Files[0].Extension.Should().Be(".txt");
        content.TotalSizeInBytes.Should().Be(share.FileContent.Length);
    }

    [Fact]
    public async Task get_content_without_password_unlock_should_fail()
    {
        //given
        var share = await CreateShare(password: "p");

        //when
        var act = async () => await Api.QuickShareExternalAccess.GetContent(slug: share.Slug);

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.ResponseBody.Should().Contain("quick-share-requires-password");
    }

    // --- Bulk download ---

    [Fact]
    public async Task bulk_download_should_return_zip_with_all_files()
    {
        //given
        var share = await CreateShare();

        //when
        var zipBytes = await ExternalBulkDownload(share);

        //then
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(1);
        entries.Should().ContainKey("hello.txt")
            .WhoseValue.Should().Equal(share.FileContent);
    }

    [Fact]
    public async Task bulk_download_should_increment_download_count()
    {
        //given
        var share = await CreateShare(maxDownloads: 5);

        //when
        await ExternalBulkDownload(share);

        //then
        var info = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);
        info.Info!.DownloadsCount.Should().Be(1);
    }

    [Fact]
    public async Task bulk_download_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateShare();

        //when
        await ExternalBulkDownload(share);

        //then
        await AssertAuditLogContains<Audit.QuickShare.BulkDownloadLinkGenerated>(
            expectedEventType: AuditLogEventTypes.QuickShare.BulkDownloadLinkGenerated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.DownloadsCountAfter.Should().Be(1);
            });
    }

    [Fact]
    public async Task bulk_download_when_exhausted_should_fail()
    {
        //given
        var share = await CreateShare(maxDownloads: 1);
        await ExternalBulkDownload(share);

        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var act = async () => await Api.QuickShareExternalAccess.GetBulkDownloadLink(
            slug: share.Slug,
            antiforgery: antiforgery);

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.StatusCode.Should().Be(410);
        ex.Which.ResponseBody.Should().Contain("quick-share-download-limit-reached");
    }

    [Fact]
    public async Task bulk_download_for_expired_share_should_fail()
    {
        //given
        Clock.SetToNow();
        var share = await CreateShare(expiresAt: Clock.UtcNow.AddMinutes(5));
        Clock.CurrentTime(Clock.UtcNow.AddHours(1));

        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var act = async () => await Api.QuickShareExternalAccess.GetBulkDownloadLink(
            slug: share.Slug,
            antiforgery: antiforgery);

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.ResponseBody.Should().Contain("quick-share-expired");
    }

    [Fact]
    public async Task password_protected_bulk_download_requires_unlock()
    {
        //given
        var share = await CreateShare(password: "p");
        var antiforgery = await Api.Antiforgery.GetToken();

        //when
        var act = async () => await Api.QuickShareExternalAccess.GetBulkDownloadLink(
            slug: share.Slug,
            antiforgery: antiforgery);

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.ResponseBody.Should().Contain("quick-share-requires-password");
    }

    [Fact]
    public async Task unlock_then_bulk_download_should_work()
    {
        //given
        var share = await CreateShare(password: "p");

        //when
        var zipBytes = await UnlockAndBulkDownload(share, password: "p");

        //then
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().ContainKey("hello.txt")
            .WhoseValue.Should().Equal(share.FileContent);
    }

    // --- File download ---

    [Fact]
    public async Task file_download_should_return_file_content()
    {
        //given
        var share = await CreateShare();

        //when
        var link = await Api.QuickShareExternalAccess.GetFileDownloadLink(
            slug: share.Slug,
            fileExternalId: share.FileExternalId,
            contentDisposition: "attachment");

        var content = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: link.DownloadPreSignedUrl,
            cookie: null);

        //then
        content.Should().Equal(share.FileContent);
    }

    [Fact]
    public async Task file_download_should_increment_download_count()
    {
        //given
        var share = await CreateShare(maxDownloads: 5);

        //when
        await Api.QuickShareExternalAccess.GetFileDownloadLink(
            slug: share.Slug,
            fileExternalId: share.FileExternalId,
            contentDisposition: "attachment");

        //then
        var info = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);
        info.Info!.DownloadsCount.Should().Be(1);
    }

    [Fact]
    public async Task file_download_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateShare();

        //when
        await Api.QuickShareExternalAccess.GetFileDownloadLink(
            slug: share.Slug,
            fileExternalId: share.FileExternalId,
            contentDisposition: "attachment");

        //then
        await AssertAuditLogContains<Audit.QuickShare.FileDownloadLinkGenerated>(
            expectedEventType: AuditLogEventTypes.QuickShare.FileDownloadLinkGenerated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.File.ExternalId.Should().Be(share.FileExternalId);
                details.DownloadsCountAfter.Should().Be(1);
            });
    }

    [Fact]
    public async Task file_download_when_individual_disabled_should_fail()
    {
        //given
        var share = await CreateShare(allowIndividualFileDownload: false);

        //when
        var act = async () => await Api.QuickShareExternalAccess.GetFileDownloadLink(
            slug: share.Slug,
            fileExternalId: share.FileExternalId,
            contentDisposition: "attachment");

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.ResponseBody.Should().Contain("individual-file-download-disabled");
    }

    // --- Owner preview ---

    [Fact]
    public async Task owner_preview_bulk_download_should_not_count()
    {
        //given
        var share = await CreateShare(maxDownloads: 5);

        //when
        await ExternalBulkDownload(share, authCookie: AppOwner.Cookie, antiforgery: AppOwner.Antiforgery);

        //then
        var info = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);
        info.Info!.DownloadsCount.Should().Be(0,
            "owner preview downloads must not be counted toward the share's limit");
    }

    [Fact]
    public async Task owner_preview_bulk_download_should_not_produce_audit_log()
    {
        //given
        var share = await CreateShare();

        //when
        await ExternalBulkDownload(share, authCookie: AppOwner.Cookie, antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogDoesNotContain(
            expectedEventType: AuditLogEventTypes.QuickShare.BulkDownloadLinkGenerated);
    }

    [Fact]
    public async Task owner_preview_file_download_should_not_count_and_no_audit()
    {
        //given
        var share = await CreateShare(maxDownloads: 5);

        //when
        await Api.QuickShareExternalAccess.GetFileDownloadLink(
            slug: share.Slug,
            fileExternalId: share.FileExternalId,
            contentDisposition: "attachment",
            authCookie: AppOwner.Cookie);

        //then
        var info = await Api.QuickShareExternalAccess.GetInfo(slug: share.Slug);
        info.Info!.DownloadsCount.Should().Be(0);

        await AssertAuditLogDoesNotContain(
            expectedEventType: AuditLogEventTypes.QuickShare.FileDownloadLinkGenerated);
    }

    [Fact]
    public async Task owner_preview_should_ignore_expired_and_exhausted_gates()
    {
        //given
        Clock.SetToNow();
        var share = await CreateShare(
            expiresAt: Clock.UtcNow.AddMinutes(5),
            maxDownloads: 1);

        // exhaust + expire
        await ExternalBulkDownload(share);
        Clock.CurrentTime(Clock.UtcNow.AddHours(1));

        //when
        var zipBytes = await ExternalBulkDownload(share,
            authCookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().ContainKey("hello.txt");

        var info = await Api.QuickShareExternalAccess.GetInfo(
            slug: share.Slug,
            authCookie: AppOwner.Cookie);
        info.Info!.IsOwnerPreview.Should().BeTrue();
        info.Info.IsExpired.Should().BeFalse("owner preview bypasses the expired gate");
        info.Info.IsExhausted.Should().BeFalse("owner preview bypasses the exhausted gate");
    }

    [Fact]
    public async Task owner_preview_should_still_require_password()
    {
        //given
        var share = await CreateShare(password: "p");

        //when
        var info = await Api.QuickShareExternalAccess.GetInfo(
            slug: share.Slug,
            authCookie: AppOwner.Cookie);

        //then
        info.Info!.IsOwnerPreview.Should().BeTrue();
        info.Info.RequiresPassword.Should().BeTrue();
        info.Info.IsUnlocked.Should().BeFalse(
            "the owner must still enter the password to access the content");

        var contentAct = async () => await Api.QuickShareExternalAccess.GetContent(
            slug: share.Slug,
            authCookie: AppOwner.Cookie);
        await contentAct.Should().ThrowAsync<TestApiCallException>();
    }

    // --- Helpers ---

    private async Task<ExternalAccessShare> CreateShare(
        string name = "test share",
        string? password = null,
        DateTimeOffset? expiresAt = null,
        int? maxDownloads = null,
        bool allowIndividualFileDownload = true)
    {
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var fileContent = Encoding.UTF8.GetBytes("hello quick share");
        var file = await UploadFile(
            content: fileContent,
            fileName: "hello.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var created = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: new CreateQuickShareRequestDto(
                Name: name,
                CustomSlug: null,
                SelectedFiles: [file.ExternalId],
                SelectedFolders: [],
                ExcludedFiles: [],
                ExcludedFolders: [],
                Mode: QuickShareMode.Browser,
                AllowIndividualFileDownload: allowIndividualFileDownload,
                ExpiresAt: expiresAt,
                Password: password,
                MaxDownloads: maxDownloads),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return new ExternalAccessShare(
            ExternalId: created.ExternalId,
            Slug: created.Slug,
            WorkspaceExternalId: workspace.ExternalId,
            FileExternalId: file.ExternalId,
            FileContent: fileContent);
    }

    private async Task<byte[]> ExternalBulkDownload(
        ExternalAccessShare share,
        Cookie? authCookie = null,
        AntiforgeryCookies? antiforgery = null,
        Cookie? sessionCookie = null)
    {
        antiforgery ??= await Api.Antiforgery.GetToken();

        var linkResponse = await Api.QuickShareExternalAccess.GetBulkDownloadLink(
            slug: share.Slug,
            antiforgery: antiforgery,
            sessionCookie: sessionCookie,
            authCookie: authCookie);

        return await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.PreSignedUrl,
            cookie: null);
    }

    private async Task<byte[]> UnlockAndBulkDownload(
        ExternalAccessShare share,
        string password)
    {
        var antiforgery = await Api.Antiforgery.GetToken();

        var unlockResult = await Api.QuickShareExternalAccess.Unlock(
            slug: share.Slug,
            request: new UnlockQuickShareRequestDto(Password: password),
            antiforgery: antiforgery);

        return await ExternalBulkDownload(
            share: share,
            antiforgery: antiforgery,
            sessionCookie: unlockResult.SessionCookie);
    }

    private static Dictionary<string, byte[]> ExtractZipEntries(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                continue;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            entries[entry.FullName] = ms.ToArray();
        }

        return entries;
    }

    private record ExternalAccessShare(
        QuickShareExtId ExternalId,
        string Slug,
        WorkspaceExtId WorkspaceExternalId,
        FileExtId FileExternalId,
        byte[] FileContent);
}
