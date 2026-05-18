using System.IO.Compression;
using System.Text;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
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
        content.Files[0].FolderExternalId.Should().BeNull(
            "an individually-shared file has no enclosing share folder");
        content.Folders.Should().BeEmpty();
        content.TotalSizeInBytes.Should().Be(share.FileContent.Length);
    }

    [Fact]
    public async Task get_content_for_nested_share_should_return_folder_structure_with_parent_chain()
    {
        //given
        var share = await CreateNestedShare();

        //when
        var content = await Api.QuickShareExternalAccess.GetContent(slug: share.Slug);

        //then — every download folder is emitted, with parent chain expressed in
        //external ids; the share's top folder has no parent inside the response.
        content
            .Folders
            .Should()
            .BeEquivalentTo(new[]
            {
                new { ExternalId = share.Root.ExternalId,  ParentExternalId = (FolderExtId?)null,                 Name = share.Root.Name },
                new { ExternalId = share.Alpha.ExternalId, ParentExternalId = (FolderExtId?)share.Root.ExternalId, Name = share.Alpha.Name },
                new { ExternalId = share.Beta.ExternalId,  ParentExternalId = (FolderExtId?)share.Alpha.ExternalId, Name = share.Beta.Name },
                new { ExternalId = share.Gamma.ExternalId, ParentExternalId = (FolderExtId?)share.Root.ExternalId, Name = share.Gamma.Name }
            });

        //and — each file points at the folder it belongs to
        content
            .Files
            .Select(f => new { f.ExternalId, f.FolderExternalId, f.Name })
            .Should()
            .BeEquivalentTo(new[]
            {
                new { ExternalId = share.RootTxt.ExternalId, FolderExternalId = (FolderExtId?)share.Root.ExternalId,  Name = "root" },
                new { ExternalId = share.ATxt.ExternalId,    FolderExternalId = (FolderExtId?)share.Alpha.ExternalId, Name = "a" },
                new { ExternalId = share.BTxt.ExternalId,    FolderExternalId = (FolderExtId?)share.Beta.ExternalId,  Name = "b" },
                new { ExternalId = share.GTxt.ExternalId,    FolderExternalId = (FolderExtId?)share.Gamma.ExternalId, Name = "g" }
            });

        content.TotalSizeInBytes.Should().Be(
            share.RootTxtContent.Length +
            share.ATxtContent.Length +
            share.BTxtContent.Length +
            share.GTxtContent.Length);
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

    // --- Selective bulk download ---

    [Fact]
    public async Task bulk_download_with_selected_subfolder_should_return_only_that_subtree()
    {
        //given
        var share = await CreateNestedShare();

        //when — pick only the alpha subtree
        var zipBytes = await SelectiveBulkDownload(
            share: share,
            selectedFolderExternalIds: [share.Alpha.ExternalId]);

        //then — only a.txt (in alpha) and b.txt (in alpha/beta) survive; the
        //surviving subtree lands rooted at the selected folder
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(2);
        entries.Should().ContainKey($"{share.Alpha.Name}/a.txt")
            .WhoseValue.Should().Equal(share.ATxtContent);
        entries.Should().ContainKey($"{share.Alpha.Name}/{share.Beta.Name}/b.txt")
            .WhoseValue.Should().Equal(share.BTxtContent);
    }

    [Fact]
    public async Task bulk_download_with_excluded_file_in_selected_folder_should_skip_it()
    {
        //given
        var share = await CreateNestedShare();

        //when — pick the whole share but exclude b.txt
        var zipBytes = await SelectiveBulkDownload(
            share: share,
            selectedFolderExternalIds: [share.Root.ExternalId],
            excludedFileExternalIds: [share.BTxt.ExternalId]);

        //then — root.txt + a.txt + g.txt; b.txt pruned
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(3);
        entries.Should().ContainKey($"{share.Root.Name}/root.txt");
        entries.Should().ContainKey($"{share.Root.Name}/{share.Alpha.Name}/a.txt");
        entries.Should().ContainKey($"{share.Root.Name}/{share.Gamma.Name}/g.txt");
        entries.Keys.Should().NotContain(k => k.EndsWith("b.txt"));
    }

    [Fact]
    public async Task bulk_download_with_excluded_subfolder_should_skip_its_whole_subtree()
    {
        //given
        var share = await CreateNestedShare();

        //when — pick the whole share but exclude the alpha subtree
        var zipBytes = await SelectiveBulkDownload(
            share: share,
            selectedFolderExternalIds: [share.Root.ExternalId],
            excludedFolderExternalIds: [share.Alpha.ExternalId]);

        //then — root.txt + g.txt; a.txt and b.txt pruned with their subtree
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(2);
        entries.Should().ContainKey($"{share.Root.Name}/root.txt");
        entries.Should().ContainKey($"{share.Root.Name}/{share.Gamma.Name}/g.txt");
        entries.Keys.Should().NotContain(k => k.EndsWith("a.txt"));
        entries.Keys.Should().NotContain(k => k.EndsWith("b.txt"));
    }

    [Fact]
    public async Task bulk_download_with_selected_individual_file_should_return_only_that_file()
    {
        //given
        var share = await CreateNestedShare();

        //when — pick a single nested file with no folder selection
        var zipBytes = await SelectiveBulkDownload(
            share: share,
            selectedFileExternalIds: [share.BTxt.ExternalId]);

        //then — only b.txt in the zip, at the root (no folder context was selected)
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(1);
        entries.Should().ContainKey("b.txt")
            .WhoseValue.Should().Equal(share.BTxtContent);
    }

    [Fact]
    public async Task bulk_download_with_ids_outside_share_should_silently_drop_them()
    {
        //given — a valid file in the share, plus a foreign external id
        var share = await CreateNestedShare();
        var foreign = FileExtId.NewId();

        //when
        var zipBytes = await SelectiveBulkDownload(
            share: share,
            selectedFileExternalIds: [share.ATxt.ExternalId, foreign]);

        //then — only the in-share file makes it, the foreign id is filtered server-side
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(1);
        entries.Should().ContainKey("a.txt")
            .WhoseValue.Should().Equal(share.ATxtContent);
    }

    [Fact]
    public async Task bulk_download_with_only_foreign_ids_returns_400()
    {
        //given
        var share = await CreateNestedShare();
        var antiforgery = await Api.Antiforgery.GetToken();

        //when — everything the client asks for is outside the effective set
        var act = async () => await Api.QuickShareExternalAccess.GetBulkDownloadLink(
            slug: share.Slug,
            antiforgery: antiforgery,
            request: new GetQuickShareBulkDownloadLinkRequestDto(
                SelectedFolderExternalIds: [FolderExtId.NewId()],
                ExcludedFolderExternalIds: null,
                SelectedFileExternalIds: [FileExtId.NewId()],
                ExcludedFileExternalIds: null));

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.StatusCode.Should().Be(400);
        ex.Which.ResponseBody.Should().Contain("quick-share-empty-bulk-selection");
    }

    [Fact]
    public async Task bulk_download_with_empty_selection_should_return_the_whole_share()
    {
        //given
        var share = await CreateNestedShare();

        //when — empty request body falls back to "whole share" behavior
        var zipBytes = await SelectiveBulkDownload(share: share);

        //then — every file in the effective set comes back
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(4);
        entries.Should().ContainKey($"{share.Root.Name}/root.txt");
        entries.Should().ContainKey($"{share.Root.Name}/{share.Alpha.Name}/a.txt");
        entries.Should().ContainKey($"{share.Root.Name}/{share.Alpha.Name}/{share.Beta.Name}/b.txt");
        entries.Should().ContainKey($"{share.Root.Name}/{share.Gamma.Name}/g.txt");
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

    // Layout used by the selective bulk download tests:
    //   <Root>/
    //     root.txt
    //     <Alpha>/
    //       a.txt
    //       <Beta>/
    //         b.txt
    //     <Gamma>/
    //       g.txt
    private async Task<NestedAccessShare> CreateNestedShare()
    {
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        var root = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);
        var alpha = await CreateFolder(parent: root, workspace: workspace, user: AppOwner);
        var beta = await CreateFolder(parent: alpha, workspace: workspace, user: AppOwner);
        var gamma = await CreateFolder(parent: root, workspace: workspace, user: AppOwner);

        var rootTxtContent = Encoding.UTF8.GetBytes("root content");
        var aTxtContent = Encoding.UTF8.GetBytes("alpha content");
        var bTxtContent = Encoding.UTF8.GetBytes("beta content");
        var gTxtContent = Encoding.UTF8.GetBytes("gamma content");

        var rootTxt = await UploadFile(
            content: rootTxtContent, fileName: "root.txt", contentType: "text/plain",
            folder: root, workspace: workspace, user: AppOwner);
        var aTxt = await UploadFile(
            content: aTxtContent, fileName: "a.txt", contentType: "text/plain",
            folder: alpha, workspace: workspace, user: AppOwner);
        var bTxt = await UploadFile(
            content: bTxtContent, fileName: "b.txt", contentType: "text/plain",
            folder: beta, workspace: workspace, user: AppOwner);
        var gTxt = await UploadFile(
            content: gTxtContent, fileName: "g.txt", contentType: "text/plain",
            folder: gamma, workspace: workspace, user: AppOwner);

        var created = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: new CreateQuickShareRequestDto(
                Name: "nested share",
                CustomSlug: null,
                SelectedFiles: [],
                SelectedFolders: [root.ExternalId],
                ExcludedFiles: [],
                ExcludedFolders: [],
                Mode: QuickShareMode.Browser,
                AllowIndividualFileDownload: true,
                ExpiresAt: null,
                Password: null,
                MaxDownloads: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return new NestedAccessShare(
            ExternalId: created.ExternalId,
            Slug: created.Slug,
            WorkspaceExternalId: workspace.ExternalId,
            Root: root,
            Alpha: alpha,
            Beta: beta,
            Gamma: gamma,
            RootTxt: rootTxt, RootTxtContent: rootTxtContent,
            ATxt: aTxt, ATxtContent: aTxtContent,
            BTxt: bTxt, BTxtContent: bTxtContent,
            GTxt: gTxt, GTxtContent: gTxtContent);
    }

    private async Task<byte[]> SelectiveBulkDownload(
        NestedAccessShare share,
        FolderExtId[]? selectedFolderExternalIds = null,
        FolderExtId[]? excludedFolderExternalIds = null,
        FileExtId[]? selectedFileExternalIds = null,
        FileExtId[]? excludedFileExternalIds = null)
    {
        var antiforgery = await Api.Antiforgery.GetToken();

        var linkResponse = await Api.QuickShareExternalAccess.GetBulkDownloadLink(
            slug: share.Slug,
            antiforgery: antiforgery,
            request: new GetQuickShareBulkDownloadLinkRequestDto(
                SelectedFolderExternalIds: selectedFolderExternalIds,
                ExcludedFolderExternalIds: excludedFolderExternalIds,
                SelectedFileExternalIds: selectedFileExternalIds,
                ExcludedFileExternalIds: excludedFileExternalIds));

        return await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.PreSignedUrl,
            cookie: null);
    }

    private record NestedAccessShare(
        QuickShareExtId ExternalId,
        string Slug,
        WorkspaceExtId WorkspaceExternalId,
        AppFolder Root,
        AppFolder Alpha,
        AppFolder Beta,
        AppFolder Gamma,
        AppFile RootTxt, byte[] RootTxtContent,
        AppFile ATxt, byte[] ATxtContent,
        AppFile BTxt, byte[] BTxtContent,
        AppFile GTxt, byte[] GTxtContent);
}
