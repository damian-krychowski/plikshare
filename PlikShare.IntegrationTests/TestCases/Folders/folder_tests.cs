using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.Folders.MoveToFolder.Contracts;
using PlikShare.Folders.Rename.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Folders;

[Collection(IntegrationTestsCollection.Name)]
public class folder_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public folder_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(
            user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    // --- Functional tests ---

    [Fact]
    public async Task when_folder_name_is_updated_it_should_be_reflected_in_content()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var newName = Random.Name("RenamedFolder");

        //when
        await Api.Folders.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            request: new UpdateFolderNameRequestDto(
                Name: newName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        folderContent.Folder.Name.Should().Be(newName);
    }

    [Fact]
    public async Task when_folder_is_moved_to_another_folder_it_should_appear_in_destination()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folderA = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var folderB = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [],
                FolderExternalIds: [folderA.ExternalId],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: folderB.ExternalId),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var destinationContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folderB.ExternalId,
            cookie: AppOwner.Cookie);

        destinationContent.Subfolders.Should().Contain(s =>
            s.ExternalId == folderA.ExternalId.Value);

        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        (topFolders.Subfolders ?? []).Should().NotContain(s =>
            s.ExternalId == folderA.ExternalId.Value);
    }

    [Fact]
    public async Task when_folder_is_moved_to_top_level_it_should_appear_in_top_folders()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var parentFolder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var childFolder = await CreateFolder(
            parent: parentFolder,
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [],
                FolderExternalIds: [childFolder.ExternalId],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        (topFolders.Subfolders ?? []).Should().Contain(s =>
            s.ExternalId == childFolder.ExternalId.Value);

        var parentContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parentFolder.ExternalId,
            cookie: AppOwner.Cookie);

        (parentContent.Subfolders ?? []).Should().NotContain(s =>
            s.ExternalId == childFolder.ExternalId.Value);
    }

    // --- Audit log tests ---

    [Fact]
    public async Task creating_folder_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folderName = Random.Name("Folder");
        var folderExternalId = FolderExtId.NewId();

        //when
        await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = folderExternalId,
                ParentExternalId = null,
                Name = folderName
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Folder.Created>(
            expectedEventType: AuditLogEventTypes.Folder.Created,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Folder.ExternalId.Should().Be(folderExternalId);
                details.Folder.Name.Encoded.Should().Be(folderName);
                details.Folder.FolderPath.Should().BeNull();
                details.Box.Should().BeNull();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_creating_folders_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Folder A",
                        Subfolders = null
                    },
                    new FolderTreeDto
                    {
                        TemporaryId = 2,
                        Name = "Folder B",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 3,
                                Name = "Folder B_A",
                                Subfolders = null
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Folders.Should().HaveCount(3);
                details.Folders.Should().Contain(f => f.Name.Encoded == "Folder A" && f.FolderPath == null);
                details.Folders.Should().Contain(f => f.Name.Encoded == "Folder B" && f.FolderPath == null);
                details.Folders.Should().Contain(f => f.Name.Encoded == "Folder B_A"
                    && f.FolderPath != null && f.FolderPath.Select(p => p.Encoded).SequenceEqual(new[] { "Folder B" }));
                details.Box.Should().BeNull();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_folder_name_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var newName = Random.Name("RenamedFolder");

        //when
        await Api.Folders.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            request: new UpdateFolderNameRequestDto(
                Name: newName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Folder.NameUpdated>(
            expectedEventType: AuditLogEventTypes.Folder.NameUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Folder.ExternalId.Should().Be(folder.ExternalId);
                details.Folder.Name.Encoded.Should().Be(newName);
                details.Box.Should().BeNull();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task moving_items_to_folder_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folderA = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var folderB = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [],
                FolderExternalIds: [folderA.ExternalId],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: folderB.ExternalId),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Folder.ItemsMoved>(
            expectedEventType: AuditLogEventTypes.Folder.ItemsMoved,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.DestinationFolder.Should().NotBeNull();
                details.DestinationFolder!.ExternalId.Should().Be(folderB.ExternalId);
                details.Folders.Should().ContainSingle(f => f.ExternalId == folderA.ExternalId);
                details.Files.Should().BeEmpty();
                details.FileUploads.Should().BeEmpty();
                details.Box.Should().BeNull();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
