using System.Text;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Create.Contracts;
using PlikShare.QuickShares.UpdateExpiration.Contracts;
using PlikShare.QuickShares.UpdateItems.Contracts;
using PlikShare.QuickShares.UpdateMaxDownloads.Contracts;
using PlikShare.QuickShares.UpdateMode.Contracts;
using PlikShare.QuickShares.UpdateName.Contracts;
using PlikShare.QuickShares.UpdatePassword.Contracts;
using PlikShare.QuickShares.UpdateSlug.Contracts;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.QuickShares;

[Collection(IntegrationTestsCollection.Name)]
public class quick_share_management_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public quick_share_management_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    // --- Create ---

    [Fact]
    public async Task creating_quick_share_in_browser_mode_should_succeed()
    {
        //given
        var (workspace, folder, file) = await CreateWorkspaceWithFile();

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "browser share",
                selectedFiles: [file.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.ExternalId.Value.Should().StartWith("qs_");
        response.Slug.Should().NotBeNullOrWhiteSpace();
        response.Url.Should().Contain(response.Slug);

        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: response.ExternalId,
            cookie: AppOwner.Cookie);

        share.Name.Should().Be("browser share");
        share.Mode.Should().Be(QuickShareMode.Browser);
        share.AllowIndividualFileDownload.Should().BeTrue();
        share.HasPassword.Should().BeFalse();
        share.MaxDownloads.Should().BeNull();
        share.ExpiresAt.Should().BeNull();
        share.DownloadsCount.Should().Be(0);
        share.Slug.Should().Be(response.Slug);
        share.HasSecret.Should().BeFalse();
        share.Url.Should().Be(response.Url);
        share.Items.SelectedFiles.Should().Equal(file.ExternalId);
        share.Items.SelectedFolders.Should().BeEmpty();
        share.Items.ExcludedFiles.Should().BeEmpty();
        share.Items.ExcludedFolders.Should().BeEmpty();
    }

    [Fact]
    public async Task creating_quick_share_in_direct_mode_should_succeed()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "direct share",
                selectedFiles: [file.ExternalId],
                mode: QuickShareMode.Direct,
                allowIndividualFileDownload: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: response.ExternalId,
            cookie: AppOwner.Cookie);

        share.Mode.Should().Be(QuickShareMode.Direct);
        share.AllowIndividualFileDownload.Should().BeFalse();
    }

    [Fact]
    public async Task creating_quick_share_with_custom_slug_should_use_it()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();
        var customSlug = $"my-custom-{Guid.NewGuid():N}".Substring(0, 30);

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "custom slug",
                selectedFiles: [file.ExternalId],
                customSlug: customSlug),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.Slug.Should().Be(customSlug);

        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: response.ExternalId,
            cookie: AppOwner.Cookie);

        share.Slug.Should().Be(customSlug);
    }

    [Fact]
    public async Task creating_quick_share_with_password_should_set_has_password()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "with password",
                selectedFiles: [file.ExternalId],
                password: "Secret123!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: response.ExternalId,
            cookie: AppOwner.Cookie);

        share.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task creating_quick_share_with_expiration_should_set_it()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();
        var expiresAt = Clock.UtcNow.AddHours(1);

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "with expiration",
                selectedFiles: [file.ExternalId],
                expiresAt: expiresAt),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: response.ExternalId,
            cookie: AppOwner.Cookie);

        share.ExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task creating_quick_share_with_max_downloads_should_set_it()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "limited",
                selectedFiles: [file.ExternalId],
                maxDownloads: 5),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: response.ExternalId,
            cookie: AppOwner.Cookie);

        share.MaxDownloads.Should().Be(5);
    }

    [Fact]
    public async Task creating_quick_share_with_taken_slug_should_fail()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();
        var customSlug = $"taken-{Guid.NewGuid():N}".Substring(0, 30);

        await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "first",
                selectedFiles: [file.ExternalId],
                customSlug: customSlug),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var act = async () => await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "second",
                selectedFiles: [file.ExternalId],
                customSlug: customSlug),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var ex = await act.Should().ThrowAsync<TestApiCallException>();
        ex.Which.ResponseBody.Should().Contain("slug");
    }

    [Fact]
    public async Task creating_quick_share_with_past_expiration_should_fail()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var act = async () => await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "expired",
                selectedFiles: [file.ExternalId],
                expiresAt: Clock.UtcNow.AddHours(-1)),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task creating_quick_share_with_no_items_should_fail()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        //when
        var act = async () => await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "empty",
                selectedFiles: []),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task creating_quick_share_with_blank_name_should_fail()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var act = async () => await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "   ",
                selectedFiles: [file.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task creating_quick_share_with_invalid_max_downloads_should_fail()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var act = async () => await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "bad",
                selectedFiles: [file.ExternalId],
                maxDownloads: 0),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    // --- List / Get ---

    [Fact]
    public async Task created_quick_share_should_appear_in_list()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        var first = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "first",
                selectedFiles: [file.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var second = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "second",
                selectedFiles: [file.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var list = await Api.QuickShares.GetList(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        list.Items.Should().HaveCount(2);
        list.Items.Should().Contain(item => item.ExternalId == first.ExternalId && item.Name == "first");
        list.Items.Should().Contain(item => item.ExternalId == second.ExternalId && item.Name == "second");
    }

    [Fact]
    public async Task get_should_return_folders_to_expand_for_nested_selection()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var root = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);
        var nested = await CreateFolder(parent: root, workspace: workspace, user: AppOwner);
        var leaf = await CreateFolder(parent: nested, workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("payload"),
            fileName: "deep.txt",
            contentType: "text/plain",
            folder: leaf,
            workspace: workspace,
            user: AppOwner);

        var created = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "nested",
                selectedFiles: [file.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var share = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: created.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        share.Items.SelectedFiles.Should().Equal(file.ExternalId);
        share.Items.FoldersToExpand.Should().NotBeEmpty(
            "the FE needs the ancestor chain to render the tree pre-selected");
        share.Items.FoldersToExpand.Should().Contain(p =>
            p.FolderExternalIds.Contains(root.ExternalId)
            && p.FolderExternalIds.Contains(nested.ExternalId)
            && p.FolderExternalIds.Contains(leaf.ExternalId));
    }

    // --- Update name ---

    [Fact]
    public async Task update_name_should_change_name()
    {
        //given
        var share = await CreateBasicQuickShare(name: "old name");

        //when
        await Api.QuickShares.UpdateName(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareNameRequestDto(Name: "new name"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.Name.Should().Be("new name");
    }

    [Fact]
    public async Task update_name_to_blank_should_fail()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        var act = async () => await Api.QuickShares.UpdateName(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareNameRequestDto(Name: "  "),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    // --- Update slug ---

    [Fact]
    public async Task update_slug_should_change_slug_and_url()
    {
        //given
        var share = await CreateBasicQuickShare();
        var newSlug = $"renamed-{Guid.NewGuid():N}".Substring(0, 30);

        //when
        await Api.QuickShares.UpdateSlug(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareSlugRequestDto(Slug: newSlug),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.Slug.Should().Be(newSlug);
        updated.Url.Should().NotBeNull().And.Subject.Should().Contain(newSlug);
    }

    [Fact]
    public async Task update_slug_to_taken_value_should_fail()
    {
        //given
        var share1 = await CreateBasicQuickShare();
        var share2 = await CreateBasicQuickShare();

        //when
        var act = async () => await Api.QuickShares.UpdateSlug(
            workspaceExternalId: share2.WorkspaceExternalId,
            quickShareExternalId: share2.ExternalId,
            request: new UpdateQuickShareSlugRequestDto(Slug: share1.Slug),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task update_slug_to_invalid_value_should_fail()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        var act = async () => await Api.QuickShares.UpdateSlug(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareSlugRequestDto(Slug: "ab"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    // --- Update expiration ---

    [Fact]
    public async Task update_expiration_should_change_it()
    {
        //given
        var share = await CreateBasicQuickShare();
        var newExpiration = Clock.UtcNow.AddHours(2);

        //when
        await Api.QuickShares.UpdateExpiration(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareExpirationRequestDto(ExpiresAt: newExpiration),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.ExpiresAt.Should().BeCloseTo(newExpiration, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task update_expiration_to_null_should_clear_it()
    {
        //given
        var share = await CreateBasicQuickShare(expiresAt: Clock.UtcNow.AddHours(1));

        //when
        await Api.QuickShares.UpdateExpiration(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareExpirationRequestDto(ExpiresAt: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task update_expiration_to_past_should_fail()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        var act = async () => await Api.QuickShares.UpdateExpiration(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareExpirationRequestDto(ExpiresAt: Clock.UtcNow.AddMinutes(-1)),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    // --- Update password ---

    [Fact]
    public async Task update_password_should_set_password()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdatePassword(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickSharePasswordRequestDto(Password: "Secret!"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task update_password_to_null_should_remove_password()
    {
        //given
        var share = await CreateBasicQuickShare(password: "Secret!");

        //when
        await Api.QuickShares.UpdatePassword(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickSharePasswordRequestDto(Password: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.HasPassword.Should().BeFalse();
    }

    // --- Update max downloads ---

    [Fact]
    public async Task update_max_downloads_should_change_value()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdateMaxDownloads(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareMaxDownloadsRequestDto(MaxDownloads: 7),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.MaxDownloads.Should().Be(7);
    }

    [Fact]
    public async Task update_max_downloads_to_null_should_clear_value()
    {
        //given
        var share = await CreateBasicQuickShare(maxDownloads: 5);

        //when
        await Api.QuickShares.UpdateMaxDownloads(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareMaxDownloadsRequestDto(MaxDownloads: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.MaxDownloads.Should().BeNull();
    }

    [Fact]
    public async Task update_max_downloads_to_zero_should_fail()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        var act = async () => await Api.QuickShares.UpdateMaxDownloads(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareMaxDownloadsRequestDto(MaxDownloads: 0),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    // --- Update mode ---

    [Fact]
    public async Task update_mode_should_change_mode_and_individual_download()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdateMode(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareModeRequestDto(
                Mode: QuickShareMode.Direct,
                AllowIndividualFileDownload: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie);

        updated.Mode.Should().Be(QuickShareMode.Direct);
        updated.AllowIndividualFileDownload.Should().BeFalse();
    }

    // --- Update items ---

    [Fact]
    public async Task update_items_should_replace_selection()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var fileA = await UploadFile(
            content: Encoding.UTF8.GetBytes("A"),
            fileName: "a.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var fileB = await UploadFile(
            content: Encoding.UTF8.GetBytes("B"),
            fileName: "b.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var created = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "items test",
                selectedFiles: [fileA.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.QuickShares.UpdateItems(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: created.ExternalId,
            request: new UpdateQuickShareItemsRequestDto(
                SelectedFiles: [fileB.ExternalId],
                SelectedFolders: [],
                ExcludedFiles: [],
                ExcludedFolders: []),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var updated = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: created.ExternalId,
            cookie: AppOwner.Cookie);

        updated.Items.SelectedFiles.Should().Equal(fileB.ExternalId);
    }

    [Fact]
    public async Task update_items_with_no_items_should_fail()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        var act = async () => await Api.QuickShares.UpdateItems(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareItemsRequestDto(
                SelectedFiles: [],
                SelectedFolders: [],
                ExcludedFiles: [],
                ExcludedFolders: []),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    // --- Delete ---

    [Fact]
    public async Task delete_should_remove_quick_share()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.Delete(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var list = await Api.QuickShares.GetList(
            workspaceExternalId: share.WorkspaceExternalId,
            cookie: AppOwner.Cookie);

        list.Items.Should().NotContain(item => item.ExternalId == share.ExternalId);
    }

    // --- Audit logs ---

    [Fact]
    public async Task creating_quick_share_should_produce_audit_log_entry()
    {
        //given
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        //when
        var response = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "audit-create",
                selectedFiles: [file.ExternalId],
                password: "p"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.Created>(
            expectedEventType: AuditLogEventTypes.QuickShare.Created,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.QuickShare.ExternalId.Should().Be(response.ExternalId);
                details.QuickShare.Name.Should().Be("audit-create");
                details.HasPassword.Should().BeTrue();
                details.Mode.Should().Be(QuickShareMode.Browser);
                details.SelectedFiles.Should().ContainSingle()
                    .Which.ExternalId.Should().Be(file.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_name_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdateName(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareNameRequestDto(Name: "renamed"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.NameUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.NameUpdated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.QuickShare.Name.Should().Be("renamed");
            },
            expectedActorEmail: AppOwner.Email);
    }

    [Fact]
    public async Task updating_slug_should_produce_audit_log_entry_with_old_and_new()
    {
        //given
        var share = await CreateBasicQuickShare();
        var newSlug = $"slug-{Guid.NewGuid():N}".Substring(0, 25);

        //when
        await Api.QuickShares.UpdateSlug(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareSlugRequestDto(Slug: newSlug),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.SlugUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.SlugUpdated,
            assertDetails: details =>
            {
                details.OldSlug.Should().Be(share.Slug);
                details.NewSlug.Should().Be(newSlug);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_expiration_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateBasicQuickShare();
        var newExpiration = Clock.UtcNow.AddHours(3);

        //when
        await Api.QuickShares.UpdateExpiration(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareExpirationRequestDto(ExpiresAt: newExpiration),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.ExpirationUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.ExpirationUpdated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.ExpiresAt.Should().BeCloseTo(newExpiration, TimeSpan.FromSeconds(1));
            },
            expectedActorEmail: AppOwner.Email);
    }

    [Fact]
    public async Task updating_password_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdatePassword(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickSharePasswordRequestDto(Password: "newpass"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.PasswordUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.PasswordUpdated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.IsSet.Should().BeTrue();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_max_downloads_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdateMaxDownloads(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareMaxDownloadsRequestDto(MaxDownloads: 11),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.MaxDownloadsUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.MaxDownloadsUpdated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.MaxDownloads.Should().Be(11);
            },
            expectedActorEmail: AppOwner.Email);
    }

    [Fact]
    public async Task updating_mode_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.UpdateMode(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            request: new UpdateQuickShareModeRequestDto(
                Mode: QuickShareMode.Direct,
                AllowIndividualFileDownload: true),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.ModeUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.ModeUpdated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
                details.Mode.Should().Be(QuickShareMode.Direct);
                details.AllowIndividualFileDownload.Should().BeTrue();
            },
            expectedActorEmail: AppOwner.Email);
    }

    [Fact]
    public async Task updating_items_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var fileA = await UploadFile(
            content: Encoding.UTF8.GetBytes("A"),
            fileName: "a.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var fileB = await UploadFile(
            content: Encoding.UTF8.GetBytes("B"),
            fileName: "b.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var created = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: "items audit",
                selectedFiles: [fileA.ExternalId]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.QuickShares.UpdateItems(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: created.ExternalId,
            request: new UpdateQuickShareItemsRequestDto(
                SelectedFiles: [fileB.ExternalId],
                SelectedFolders: [],
                ExcludedFiles: [],
                ExcludedFolders: []),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.ItemsUpdated>(
            expectedEventType: AuditLogEventTypes.QuickShare.ItemsUpdated,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(created.ExternalId);
                details.SelectedFiles.Should().ContainSingle()
                    .Which.ExternalId.Should().Be(fileB.ExternalId);
            },
            expectedActorEmail: AppOwner.Email);
    }

    [Fact]
    public async Task deleting_should_produce_audit_log_entry()
    {
        //given
        var share = await CreateBasicQuickShare();

        //when
        await Api.QuickShares.Delete(
            workspaceExternalId: share.WorkspaceExternalId,
            quickShareExternalId: share.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.QuickShare.Deleted>(
            expectedEventType: AuditLogEventTypes.QuickShare.Deleted,
            assertDetails: details =>
            {
                details.QuickShare.ExternalId.Should().Be(share.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    // --- Helpers ---

    private static CreateQuickShareRequestDto NewCreateRequest(
        string name,
        List<FileExtId> selectedFiles,
        List<FolderExtId>? selectedFolders = null,
        List<FileExtId>? excludedFiles = null,
        List<FolderExtId>? excludedFolders = null,
        string? customSlug = null,
        QuickShareMode mode = QuickShareMode.Browser,
        bool allowIndividualFileDownload = true,
        DateTimeOffset? expiresAt = null,
        string? password = null,
        int? maxDownloads = null) => new(
            Name: name,
            CustomSlug: customSlug,
            SelectedFiles: selectedFiles,
            SelectedFolders: selectedFolders ?? [],
            ExcludedFiles: excludedFiles ?? [],
            ExcludedFolders: excludedFolders ?? [],
            Mode: mode,
            AllowIndividualFileDownload: allowIndividualFileDownload,
            ExpiresAt: expiresAt,
            Password: password,
            MaxDownloads: maxDownloads);

    private async Task<(AppWorkspace Workspace, AppFolder Folder, AppFile File)> CreateWorkspaceWithFile()
    {
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("hello"),
            fileName: "hello.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        return (workspace, folder, file);
    }

    private async Task<AppQuickShare> CreateBasicQuickShare(
        string name = "test share",
        string? password = null,
        DateTimeOffset? expiresAt = null,
        int? maxDownloads = null)
    {
        var (workspace, _, file) = await CreateWorkspaceWithFile();

        var created = await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            request: NewCreateRequest(
                name: name,
                selectedFiles: [file.ExternalId],
                password: password,
                expiresAt: expiresAt,
                maxDownloads: maxDownloads),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return new AppQuickShare(
            ExternalId: created.ExternalId,
            Slug: created.Slug,
            WorkspaceExternalId: workspace.ExternalId,
            FileExternalId: file.ExternalId);
    }

    private record AppQuickShare(
        PlikShare.QuickShares.Id.QuickShareExtId ExternalId,
        string Slug,
        PlikShare.Workspaces.Id.WorkspaceExtId WorkspaceExternalId,
        FileExtId FileExternalId);
}
