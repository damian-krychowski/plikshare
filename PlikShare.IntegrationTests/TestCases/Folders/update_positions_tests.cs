using FluentAssertions;
using PlikShare.Folders.Id;
using PlikShare.Folders.List;
using PlikShare.Folders.MoveToFolder.Contracts;
using PlikShare.Folders.UpdatePositions.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Folders;

[Collection(IntegrationTestsCollection.Name)]
public class update_positions_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public update_positions_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    [Fact]
    public async Task initial_top_folder_positions_are_materialized_sequentially_when_stored_as_null()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderC = await CreateFolder(workspace: workspace, user: AppOwner);

        // when
        var content = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        // then - positions are sequential multiples of ItemPosition.Step
        content.Subfolders.Should().HaveCount(3);
        content.Subfolders[0].ExternalId.Should().Be(folderA.ExternalId.Value);
        content.Subfolders[0].Position.Should().Be(1 * ItemPosition.Step);
        content.Subfolders[1].ExternalId.Should().Be(folderB.ExternalId.Value);
        content.Subfolders[1].Position.Should().Be(2 * ItemPosition.Step);
        content.Subfolders[2].ExternalId.Should().Be(folderC.ExternalId.Value);
        content.Subfolders[2].Position.Should().Be(3 * ItemPosition.Step);
    }

    [Fact]
    public async Task can_reorder_folders_in_workspace_top()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderC = await CreateFolder(workspace: workspace, user: AppOwner);

        // when - move folderC between folderA (1024) and folderB (2048)
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = null,
                Folders = [
                    new UpdatePositionItemDto
                    {
                        ExternalId = folderC.ExternalId.Value,
                        Position = 1536
                    }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then
        var content = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        content.Subfolders.Should().HaveCount(3);
        content.Subfolders[0].ExternalId.Should().Be(folderA.ExternalId.Value);
        content.Subfolders[1].ExternalId.Should().Be(folderC.ExternalId.Value);
        content.Subfolders[2].ExternalId.Should().Be(folderB.ExternalId.Value);
    }

    [Fact]
    public async Task can_reorder_folders_inside_a_subfolder()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var parent = await CreateFolder(workspace: workspace, user: AppOwner);

        var subA = await CreateFolder(parent: parent, workspace: workspace, user: AppOwner);
        var subB = await CreateFolder(parent: parent, workspace: workspace, user: AppOwner);

        // when - swap subA and subB
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = parent.ExternalId,
                Folders = [
                    new UpdatePositionItemDto { ExternalId = subA.ExternalId.Value, Position = 2 * ItemPosition.Step },
                    new UpdatePositionItemDto { ExternalId = subB.ExternalId.Value, Position = 1 * ItemPosition.Step }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then
        var content = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parent.ExternalId,
            cookie: AppOwner.Cookie);

        content.Subfolders.Should().HaveCount(2);
        content.Subfolders[0].ExternalId.Should().Be(subB.ExternalId.Value);
        content.Subfolders[0].Position.Should().Be(1 * ItemPosition.Step);
        content.Subfolders[1].ExternalId.Should().Be(subA.ExternalId.Value);
        content.Subfolders[1].Position.Should().Be(2 * ItemPosition.Step);
    }

    [Fact]
    public async Task can_swap_two_files_via_position_update()
    {
        // given - 2 files, fileB uploaded last so by id DESC it's first in display
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var fileA = await UploadFile(
            content: "fileA"u8.ToArray(),
            fileName: "a.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var fileB = await UploadFile(
            content: "fileB"u8.ToArray(),
            fileName: "b.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // initial materialized order (id DESC for files): fileB=1024, fileA=2048
        // when - swap so fileA is first
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = folder.ExternalId,
                Folders = [],
                Files = [
                    new UpdatePositionItemDto { ExternalId = fileA.ExternalId.Value, Position = 1 * ItemPosition.Step },
                    new UpdatePositionItemDto { ExternalId = fileB.ExternalId.Value, Position = 2 * ItemPosition.Step }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then
        var content = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        content.Files.Should().HaveCount(2);
        content.Files[0].ExternalId.Should().Be(fileA.ExternalId.Value);
        content.Files[0].Position.Should().Be(1 * ItemPosition.Step);
        content.Files[1].ExternalId.Should().Be(fileB.ExternalId.Value);
        content.Files[1].Position.Should().Be(2 * ItemPosition.Step);
    }

    [Fact]
    public async Task can_reorder_folders_and_files_in_one_call()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var parent = await CreateFolder(workspace: workspace, user: AppOwner);

        var subA = await CreateFolder(parent: parent, workspace: workspace, user: AppOwner);
        var subB = await CreateFolder(parent: parent, workspace: workspace, user: AppOwner);

        var fileA = await UploadFile(
            content: "a"u8.ToArray(),
            fileName: "a.txt",
            contentType: "text/plain",
            folder: parent,
            workspace: workspace,
            user: AppOwner);

        var fileB = await UploadFile(
            content: "b"u8.ToArray(),
            fileName: "b.txt",
            contentType: "text/plain",
            folder: parent,
            workspace: workspace,
            user: AppOwner);

        // when
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = parent.ExternalId,
                Folders = [
                    new UpdatePositionItemDto { ExternalId = subA.ExternalId.Value, Position = 2 * ItemPosition.Step },
                    new UpdatePositionItemDto { ExternalId = subB.ExternalId.Value, Position = 1 * ItemPosition.Step }
                ],
                Files = [
                    new UpdatePositionItemDto { ExternalId = fileA.ExternalId.Value, Position = 5000 },
                    new UpdatePositionItemDto { ExternalId = fileB.ExternalId.Value, Position = 3000 }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then
        var content = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parent.ExternalId,
            cookie: AppOwner.Cookie);

        content.Subfolders.Should().HaveCount(2);
        content.Subfolders[0].ExternalId.Should().Be(subB.ExternalId.Value);
        content.Subfolders[1].ExternalId.Should().Be(subA.ExternalId.Value);

        content.Files.Should().HaveCount(2);
        content.Files[0].ExternalId.Should().Be(fileB.ExternalId.Value);
        content.Files[0].Position.Should().Be(3000);
        content.Files[1].ExternalId.Should().Be(fileA.ExternalId.Value);
        content.Files[1].Position.Should().Be(5000);
    }

    [Fact]
    public async Task collision_triggers_rebalance_to_clean_sequential_positions()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderC = await CreateFolder(workspace: workspace, user: AppOwner);

        // first reorder: assigns explicit positions
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = null,
                Folders = [
                    new UpdatePositionItemDto { ExternalId = folderA.ExternalId.Value, Position = 1 * ItemPosition.Step },
                    new UpdatePositionItemDto { ExternalId = folderB.ExternalId.Value, Position = 2 * ItemPosition.Step },
                    new UpdatePositionItemDto { ExternalId = folderC.ExternalId.Value, Position = 3 * ItemPosition.Step }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // when - submit a position that collides with folderA's stored value
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = null,
                Folders = [
                    new UpdatePositionItemDto { ExternalId = folderC.ExternalId.Value, Position = 1 * ItemPosition.Step }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then - rebalance should produce clean sequential positions
        var content = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        content.Subfolders.Should().HaveCount(3);
        // After rebalance: folderC and folderA both want 1024 — id breaks tie.
        // Sort key: (IS NULL), position, id ASC. Both at 1024 → folderA first (smaller id).
        // Then folderB at 2048.
        // After rebalance: positions reset to 1*1024, 2*1024, 3*1024 in that order.
        content.Subfolders[0].Position.Should().Be(1 * ItemPosition.Step);
        content.Subfolders[1].Position.Should().Be(2 * ItemPosition.Step);
        content.Subfolders[2].Position.Should().Be(3 * ItemPosition.Step);

        // Verify all positions are unique (no collision)
        var positions = content.Subfolders.Select(s => s.Position).ToList();
        positions.Distinct().Should().HaveCount(3);
    }

    [Fact]
    public async Task move_items_to_folder_resets_position_to_null()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(workspace: workspace, user: AppOwner);

        var subInA = await CreateFolder(parent: folderA, workspace: workspace, user: AppOwner);

        // pin subInA to explicit position
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = folderA.ExternalId,
                Folders = [
                    new UpdatePositionItemDto { ExternalId = subInA.ExternalId.Value, Position = 5000 }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // when - move subInA from folderA to folderB
        await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [],
                FolderExternalIds: [subInA.ExternalId],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: folderB.ExternalId),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then - in folderB, subInA should appear with materialized position
        // (its stored value is now NULL — materialization assigns 1024 since it's the only item)
        var folderBContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folderB.ExternalId,
            cookie: AppOwner.Cookie);

        folderBContent.Subfolders.Should().HaveCount(1);
        folderBContent.Subfolders[0].ExternalId.Should().Be(subInA.ExternalId.Value);
        folderBContent.Subfolders[0].Position.Should().Be(1 * ItemPosition.Step);
    }

    [Fact]
    public async Task update_positions_for_non_existing_folder_returns_404()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        // when / then
        var act = async () => await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = null,
                Folders = [
                    new UpdatePositionItemDto
                    {
                        ExternalId = FolderExtId.NewId().Value,
                        Position = 1 * ItemPosition.Step
                    }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await act.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task update_positions_with_wrong_parent_returns_404()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(workspace: workspace, user: AppOwner);
        var subInA = await CreateFolder(parent: folderA, workspace: workspace, user: AppOwner);

        // when - try to reorder subInA but say its parent is folderB (wrong)
        var act = async () => await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = folderB.ExternalId,
                Folders = [
                    new UpdatePositionItemDto
                    {
                        ExternalId = subInA.ExternalId.Value,
                        Position = 1 * ItemPosition.Step
                    }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then
        await act.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task empty_request_succeeds_as_no_op()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        // when / then - should not throw
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = folder.ExternalId,
                Folders = [],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }

    [Fact]
    public async Task explicit_positions_keep_items_pinned_above_null_positions()
    {
        // given - 3 folders with NULL positions
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);

        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderC = await CreateFolder(workspace: workspace, user: AppOwner);

        // when - pin only folderC
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = null,
                Folders = [
                    new UpdatePositionItemDto { ExternalId = folderC.ExternalId.Value, Position = 1 }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then - folderC pinned at 1; folderA/folderB get their materialized positions
        // persisted by the write (1024, 2048) so the user's mental model is preserved.
        var content = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        content.Subfolders.Should().HaveCount(3);
        content.Subfolders[0].ExternalId.Should().Be(folderC.ExternalId.Value);
        content.Subfolders[0].Position.Should().Be(1);
        content.Subfolders[1].ExternalId.Should().Be(folderA.ExternalId.Value);
        content.Subfolders[1].Position.Should().Be(1 * ItemPosition.Step);
        content.Subfolders[2].ExternalId.Should().Be(folderB.ExternalId.Value);
        content.Subfolders[2].Position.Should().Be(2 * ItemPosition.Step);
    }
}
