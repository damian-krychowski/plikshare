using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Storages.List.Contracts;
using PlikShare.Trash.DeleteForever.Contracts;
using PlikShare.Trash.List.Contracts;
using PlikShare.Trash.Restore.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.UpdateTrashPolicy.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Trash;

[Collection(IntegrationTestsCollection.Name)]
public class trash_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public trash_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    // ── Policy ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task new_workspace_has_trash_disabled_by_default()
    {
        //given & when
        var workspace = await CreateWorkspace(AppOwner);

        //then
        var details = await Api.Workspaces.GetDetails(workspace.ExternalId, AppOwner.Cookie);

        details.TrashPolicy.Enabled.Should().BeFalse();
        details.TrashPolicy.RetentionDays.Should().BeNull();
    }

    [Fact]
    public async Task trash_policy_can_be_enabled_with_a_retention_window()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);

        //when
        await EnableTrash(workspace, retentionDays: 30);

        //then
        var details = await Api.Workspaces.GetDetails(workspace.ExternalId, AppOwner.Cookie);

        details.TrashPolicy.Enabled.Should().BeTrue();
        details.TrashPolicy.RetentionDays.Should().Be(30);
    }

    [Fact]
    public async Task trash_policy_can_be_enabled_with_no_retention_limit()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);

        //when — enabled + null retentionDays means "keep forever"
        await EnableTrash(workspace, retentionDays: null);

        //then
        var details = await Api.Workspaces.GetDetails(workspace.ExternalId, AppOwner.Cookie);

        details.TrashPolicy.Enabled.Should().BeTrue();
        details.TrashPolicy.RetentionDays.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_000)]
    public async Task enabling_trash_with_out_of_range_retention_days_fails(int retentionDays)
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Workspaces.UpdateTrashPolicy(
                externalId: workspace.ExternalId,
                request: new UpdateWorkspaceTrashPolicyDto { Enabled = true, RetentionDays = retentionDays },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("invalid-trash-policy");
    }

    [Fact]
    public async Task workspace_inherits_the_storage_default_trash_policy_at_creation_time()
    {
        //given — a storage whose default trash policy is enabled
        var storage = await CreateHardDriveStorageWithDefaultTrashPolicy(
            new TrashPolicyDto { Enabled = true, RetentionDays = 14 });

        //when
        var workspace = await CreateWorkspaceOnStorage(storage);

        //then
        var details = await Api.Workspaces.GetDetails(workspace, AppOwner.Cookie);

        details.TrashPolicy.Enabled.Should().BeTrue();
        details.TrashPolicy.RetentionDays.Should().Be(14);
    }

    [Fact]
    public async Task updating_storage_default_trash_policy_does_not_affect_existing_workspaces()
    {
        //given — a storage with trash disabled and a workspace already created on it
        var storage = await CreateHardDriveStorageWithDefaultTrashPolicy(
            new TrashPolicyDto { Enabled = false, RetentionDays = null });

        var existingWorkspace = await CreateWorkspaceOnStorage(storage);

        //when — the storage default is flipped to enabled
        await Api.Storages.UpdateDefaultTrashPolicy(
            externalId: storage,
            request: new TrashPolicyDto { Enabled = true, RetentionDays = 20 },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var newWorkspace = await CreateWorkspaceOnStorage(storage);

        //then — the snapshot taken at creation time stands: only the new workspace inherits it
        var existingDetails = await Api.Workspaces.GetDetails(existingWorkspace, AppOwner.Cookie);
        existingDetails.TrashPolicy.Enabled.Should().BeFalse();

        var newDetails = await Api.Workspaces.GetDetails(newWorkspace, AppOwner.Cookie);
        newDetails.TrashPolicy.Enabled.Should().BeTrue();
        newDetails.TrashPolicy.RetentionDays.Should().Be(20);
    }

    // ── Soft-delete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task bulk_delete_with_trash_enabled_moves_the_file_to_trash()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadTextFile("report.txt", folder, workspace);

        //when
        await BulkDeleteFiles(workspace, file);

        //then — gone from the folder listing
        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        (folderContent.Files ?? []).Should().NotContain(f => f.ExternalId == file.ExternalId.Value);

        //and — present in the trash
        var trash = await GetTrash(workspace);
        trash.Items.Should().ContainSingle(i => i.ExternalId == file.ExternalId);
    }

    [Fact]
    public async Task bulk_delete_with_trash_disabled_hard_deletes_the_file()
    {
        //given — trash is disabled by default
        var workspace = await CreateWorkspace(AppOwner);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadTextFile("report.txt", folder, workspace);

        //when
        await BulkDeleteFiles(workspace, file);

        //then — nothing landed in the trash
        var trash = await GetTrash(workspace);
        trash.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task trashed_file_keeps_its_original_folder_path_when_the_folder_is_deleted()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(name: "Quarterly", workspace: workspace, user: AppOwner);
        var file = await UploadTextFile("report.txt", folder, workspace);

        //when — the whole folder is bulk-deleted
        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [],
                FolderExternalIds = [folder.ExternalId],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — the file inside the subtree is in the trash, snapshotting its path
        var trash = await GetTrash(workspace);

        var item = trash.Items.Should().ContainSingle(i => i.ExternalId == file.ExternalId).Subject;
        item.OriginalFolderPath.Should().Equal("Quarterly");
    }

    [Fact]
    public async Task bulk_deleting_an_already_trashed_file_is_idempotent()
    {
        //given — a file already sitting in the trash
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when — it is bulk-deleted a second time
        await BulkDeleteFiles(workspace, file);

        //then — it is still a single trash entry, not duplicated or errored
        var trash = await GetTrash(workspace);
        trash.Items.Should().ContainSingle(i => i.ExternalId == file.ExternalId);
    }

    [Fact]
    public async Task trash_total_size_sums_the_trashed_files()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        await TrashTextFile("a.txt", folder, workspace);
        await TrashTextFile("b.txt", folder, workspace);

        //when
        var trash = await GetTrash(workspace);

        //then
        trash.Items.Should().HaveCount(2);
        trash.TotalSizeInBytes.Should().Be(trash.Items.Sum(i => i.SizeInBytes));
        trash.TotalSizeInBytes.Should().BeGreaterThan(0);
    }

    // ── Policy changes while items already sit in the trash ──────────────────

    [Fact]
    public async Task disabling_trash_keeps_already_trashed_items_listed_for_imminent_purge()
    {
        //given — a file is trashed while the policy is enabled
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace, retentionDays: 30);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when — trash is disabled afterwards
        await DisableTrash(workspace);

        //then — the leftover item is still listed, but due for purge at the next sweep
        var trash = await GetTrash(workspace);

        var item = trash.Items.Should().ContainSingle(i => i.ExternalId == file.ExternalId).Subject;
        item.AutoDeletesAt.Should().Be(item.DeletedAt);
    }

    [Fact]
    public async Task items_can_still_be_restored_after_trash_is_disabled()
    {
        //given — a file is trashed, then the policy is disabled
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace, retentionDays: 30);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        await DisableTrash(workspace);

        //when
        var result = await Restore(workspace, OriginalPath(file.ExternalId));

        //then — disabling trash does not block recovery of what is already in it
        result.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.Restored);

        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        folderContent.Files!.Should().ContainSingle(f => f.ExternalId == file.ExternalId.Value);
    }

    [Fact]
    public async Task auto_deletes_at_tracks_the_current_retention_window()
    {
        //given — a file trashed under a 30-day retention
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace, retentionDays: 30);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        var before = (await GetTrash(workspace)).Items.Single();
        before.AutoDeletesAt.Should().Be(before.DeletedAt.AddDays(30));

        //when — the retention window is shortened
        await EnableTrash(workspace, retentionDays: 7);

        //then — AutoDeletesAt is recomputed live from the current policy, not snapshotted
        var after = (await GetTrash(workspace)).Items.Single();
        after.AutoDeletesAt.Should().Be(after.DeletedAt.AddDays(7));
    }

    [Fact]
    public async Task auto_deletes_at_is_null_when_retention_is_unlimited()
    {
        //given — trash kept forever (enabled, no retention limit)
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace, retentionDays: null);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        await TrashTextFile("report.txt", folder, workspace);

        //when
        var trash = await GetTrash(workspace);

        //then
        trash.Items.Single().AutoDeletesAt.Should().BeNull();
    }

    // ── Restore ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task restore_original_path_returns_the_file_to_its_folder()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when
        var result = await Restore(workspace, OriginalPath(file.ExternalId));

        //then
        result.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.Restored);

        (await GetTrash(workspace)).Items.Should().BeEmpty();

        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        folderContent.Files!.Should().ContainSingle(f => f.ExternalId == file.ExternalId.Value);
    }

    [Fact]
    public async Task restore_to_a_chosen_folder_places_the_file_there()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var sourceFolder = await CreateFolder(workspace: workspace, user: AppOwner);
        var targetFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await TrashTextFile("report.txt", sourceFolder, workspace);

        //when
        var result = await Restore(workspace, ChosenFolder(file.ExternalId, targetFolder.ExternalId));

        //then
        result.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.Restored);

        var targetContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: targetFolder.ExternalId,
            cookie: AppOwner.Cookie);

        targetContent.Files!.Should().ContainSingle(f => f.ExternalId == file.ExternalId.Value);
    }

    [Fact]
    public async Task restore_to_a_null_target_places_the_file_at_the_workspace_root()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when — chosen-folder restore with a null target means the workspace root
        var result = await Restore(workspace, ChosenFolder(file.ExternalId, target: null));

        //then
        result.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.Restored);

        var root = await Api.Folders.GetTop(workspace.ExternalId, AppOwner.Cookie);
        root.Files.Should().ContainSingle(f => f.ExternalId == file.ExternalId.Value);
    }

    [Fact]
    public async Task restore_resolves_a_name_collision_by_suffixing_the_restored_file()
    {
        //given — trash a file, then upload a live file with the same name into the same folder
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var trashedFile = await TrashTextFile("report.txt", folder, workspace);
        await UploadTextFile("report.txt", folder, workspace);

        //when
        var result = await Restore(workspace, OriginalPath(trashedFile.ExternalId));

        //then — the restored file is suffixed so it doesn't clobber the live one
        var restored = result.Results.Should().ContainSingle().Subject;
        restored.Status.Should().Be(RestoreStatus.Restored);

        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        folderContent.Files!.Select(f => f.Name).Should()
            .BeEquivalentTo("report", "report (restored)");
    }

    [Fact]
    public async Task restore_suffixes_incrementally_on_repeated_collisions()
    {
        //given — a live "report.txt" plus two trashed files of the same name
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadTextFile("report.txt", folder, workspace);
        var firstTrashed = await TrashTextFile("report.txt", folder, workspace);
        var secondTrashed = await TrashTextFile("report.txt", folder, workspace);

        //when — both are restored in one batch
        var result = await Restore(
            workspace,
            OriginalPath(firstTrashed.ExternalId),
            OriginalPath(secondTrashed.ExternalId));

        //then — each collision bumps the suffix
        result.Results.Should().HaveCount(2);
        result.Results.Should().AllSatisfy(r => r.Status.Should().Be(RestoreStatus.Restored));

        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        folderContent.Files!.Select(f => f.Name).Should()
            .BeEquivalentTo("report", "report (restored)", "report (restored 2)");
    }

    [Fact]
    public async Task restoring_a_file_that_is_not_in_trash_reports_not_found()
    {
        //given — a live file that was never trashed
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var liveFile = await UploadTextFile("report.txt", folder, workspace);

        //when
        var result = await Restore(workspace, OriginalPath(liveFile.ExternalId));

        //then
        result.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.NotFound);
    }

    [Fact]
    public async Task restore_to_an_unknown_target_folder_reports_destination_invalid()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when — restore into a folder id that does not exist
        var result = await Restore(workspace, ChosenFolder(file.ExternalId, FolderExtId.NewId()));

        //then
        result.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.DestinationInvalid);

        //and — the failed restore left the item in the trash
        (await GetTrash(workspace)).Items.Should().ContainSingle(i => i.ExternalId == file.ExternalId);
    }

    [Fact]
    public async Task restoring_an_already_restored_item_reports_not_found()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        var first = await Restore(workspace, OriginalPath(file.ExternalId));
        first.Results.Single().Status.Should().Be(RestoreStatus.Restored);

        //when — the same item is restored again
        var second = await Restore(workspace, OriginalPath(file.ExternalId));

        //then — it is no longer in the trash
        second.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.NotFound);
    }

    [Fact]
    public async Task restore_processes_a_mixed_batch_per_item()
    {
        //given — one genuinely trashed file and one id that is not in the trash
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var trashedFile = await TrashTextFile("report.txt", folder, workspace);
        var unknownId = FileExtId.NewId();

        //when
        var result = await Restore(
            workspace,
            OriginalPath(trashedFile.ExternalId),
            OriginalPath(unknownId));

        //then — each item gets its own outcome
        result.Results.Should().HaveCount(2);
        result.Results.Single(r => r.FileExternalId == trashedFile.ExternalId)
            .Status.Should().Be(RestoreStatus.Restored);
        result.Results.Single(r => r.FileExternalId == unknownId)
            .Status.Should().Be(RestoreStatus.NotFound);
    }

    [Fact]
    public async Task restoring_an_item_produces_an_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when
        await Restore(workspace, OriginalPath(file.ExternalId));

        //then
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Trash.ItemsRestored,
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    // ── Delete forever / empty ───────────────────────────────────────────────

    [Fact]
    public async Task delete_forever_permanently_removes_a_file_from_the_trash()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await TrashTextFile("report.txt", folder, workspace);

        //when
        var result = await Api.Trash.DeleteForever(
            workspaceExternalId: workspace.ExternalId,
            request: new DeleteForeverRequestDto { FileExternalIds = [file.ExternalId] },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        result.DeletedCount.Should().Be(1);
        (await GetTrash(workspace)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task delete_forever_ignores_files_that_are_not_in_trash()
    {
        //given — a live file that was never trashed
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var liveFile = await UploadTextFile("report.txt", folder, workspace);

        //when — delete-forever is a trash-only operation; a live id is silently dropped
        var result = await Api.Trash.DeleteForever(
            workspaceExternalId: workspace.ExternalId,
            request: new DeleteForeverRequestDto { FileExternalIds = [liveFile.ExternalId] },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — nothing was deleted, and the live file is untouched
        result.DeletedCount.Should().Be(0);

        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie);

        folderContent.Files!.Should().ContainSingle(f => f.ExternalId == liveFile.ExternalId.Value);
    }

    [Fact]
    public async Task empty_trash_clears_every_trashed_file()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        for (var i = 0; i < 3; i++)
            await TrashTextFile($"report-{i}.txt", folder, workspace);

        //when
        var result = await Api.Trash.EmptyTrash(workspace.ExternalId, AppOwner.Cookie, AppOwner.Antiforgery);

        //then
        result.DeletedCount.Should().Be(3);
        (await GetTrash(workspace)).Items.Should().BeEmpty();
    }

    [Fact]
    public async Task empty_trash_on_an_empty_trash_is_a_no_op()
    {
        //given — trash enabled but nothing in it
        var workspace = await CreateWorkspace(AppOwner);
        await EnableTrash(workspace);

        //when
        var result = await Api.Trash.EmptyTrash(workspace.ExternalId, AppOwner.Cookie, AppOwner.Antiforgery);

        //then
        result.DeletedCount.Should().Be(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task EnableTrash(AppWorkspace workspace, int? retentionDays = 30) =>
        Api.Workspaces.UpdateTrashPolicy(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceTrashPolicyDto { Enabled = true, RetentionDays = retentionDays },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

    private Task DisableTrash(AppWorkspace workspace) =>
        Api.Workspaces.UpdateTrashPolicy(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceTrashPolicyDto { Enabled = false, RetentionDays = null },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

    private Task<GetTrashItemsResponseDto> GetTrash(AppWorkspace workspace) =>
        Api.Trash.GetItems(workspace.ExternalId, AppOwner.Cookie);

    private async Task<AppFile> UploadTextFile(
        string fileName,
        AppFolder folder,
        AppWorkspace workspace)
    {
        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes($"content of {fileName} {Guid.NewGuid()}"),
            fileName: fileName,
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFilesUnlocked(
            fileExternalIds: [file.ExternalId],
            user: AppOwner);

        return file;
    }

    private async Task<AppFile> TrashTextFile(
        string fileName,
        AppFolder folder,
        AppWorkspace workspace)
    {
        var file = await UploadTextFile(fileName, folder, workspace);
        await BulkDeleteFiles(workspace, file);
        return file;
    }

    private Task BulkDeleteFiles(AppWorkspace workspace, params AppFile[] files) =>
        Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = files.Select(f => f.ExternalId).ToList(),
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

    private Task<RestoreFromTrashResponseDto> Restore(
        AppWorkspace workspace,
        params RestoreItemDto[] items) =>
        Api.Trash.Restore(
            workspaceExternalId: workspace.ExternalId,
            request: new RestoreFromTrashRequestDto { Items = items.ToList() },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

    private static RestoreItemDto OriginalPath(FileExtId fileExternalId) =>
        new()
        {
            FileExternalId = fileExternalId,
            Mode = RestoreMode.OriginalPath,
            TargetFolderExternalId = null
        };

    private static RestoreItemDto ChosenFolder(FileExtId fileExternalId, FolderExtId? target) =>
        new()
        {
            FileExternalId = fileExternalId,
            Mode = RestoreMode.ChosenFolder,
            TargetFolderExternalId = target
        };

    private async Task<StorageExtId> CreateHardDriveStorageWithDefaultTrashPolicy(
        TrashPolicyDto defaultTrashPolicy)
    {
        var storageName = Random.Name("hard-drive");

        var storage = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: storageName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{storageName}",
                EncryptionType: StorageEncryptionType.None,
                DefaultTrashPolicy: defaultTrashPolicy),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return storage.ExternalId;
    }

    private async Task<WorkspaceExtId> CreateWorkspaceOnStorage(StorageExtId storage)
    {
        var workspace = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: storage,
                Name: Random.Name("workspace")),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        return workspace.ExternalId;
    }
}
