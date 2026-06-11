using FluentAssertions;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Folders.Id;
using PlikShare.Folders.MoveToFolder.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Trash.Restore.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Workspaces.UpdateTrashPolicy.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

[Collection(IntegrationTestsCollection.Name)]
public class dependent_files_folder_consistency_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public dependent_files_folder_consistency_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task moving_parent_file_to_another_folder_should_also_move_its_dependent_files()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var sourceFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var destinationFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: sourceFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(512),
            thumbnailFileName: "thumb-small.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var sourceFolderId = GetFolderInternalId(sourceFolder.ExternalId);
        var destinationFolderId = GetFolderInternalId(destinationFolder.ExternalId);

        var dependentFolderIdsBeforeMove = GetDependentFilesFolderIds(parentFile.ExternalId);

        dependentFolderIdsBeforeMove.Should().NotBeEmpty();
        dependentFolderIdsBeforeMove.Should().AllSatisfy(
            folderId => folderId.Should().Be(sourceFolderId));

        //when
        await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [parentFile.ExternalId],
                FolderExternalIds: [],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: destinationFolder.ExternalId,
                DestinationPosition: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var dependentFolderIdsAfterMove = GetDependentFilesFolderIds(parentFile.ExternalId);

        dependentFolderIdsAfterMove.Should().NotBeEmpty();
        dependentFolderIdsAfterMove.Should().AllSatisfy(
            folderId => folderId.Should().Be(destinationFolderId));
    }

    [Fact]
    public async Task restoring_parent_file_from_trash_should_bring_dependent_files_back_to_parent_folder()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        await Api.Workspaces.UpdateTrashPolicy(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceTrashPolicyDto
            {
                Enabled = true,
                RetentionDays = 30
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(512),
            thumbnailFileName: "thumb-small.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var expectedFolderId = GetFolderInternalId(folder.ExternalId);

        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [parentFile.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var restoreResult = await Api.Trash.Restore(
            workspaceExternalId: workspace.ExternalId,
            request: new RestoreFromTrashRequestDto
            {
                Items =
                [
                    new RestoreItemDto
                    {
                        FileExternalId = parentFile.ExternalId,
                        Mode = RestoreMode.OriginalPath,
                        TargetFolderExternalId = null
                    }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        restoreResult.Results.Should().ContainSingle()
            .Which.Status.Should().Be(RestoreStatus.Restored);

        var dependentFolderIdsAfterRestore = GetDependentFilesFolderIds(parentFile.ExternalId);

        dependentFolderIdsAfterRestore.Should().NotBeEmpty();
        dependentFolderIdsAfterRestore.Should().AllSatisfy(
            folderId => folderId.Should().Be(expectedFolderId));
    }

    [Fact]
    public async Task moving_parent_file_should_also_move_its_pending_dependent_uploads()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var sourceFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var destinationFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: sourceFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var dependentUploadContent = RandomBytes(1024);
        var dependentUploadExternalId = FileUploadExtId.NewId();

        var initiateResponse = await Api.Uploads.BulkInitiate(
            workspaceExternalId: workspace.ExternalId,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = dependentUploadExternalId.Value,
                        FolderExternalId = sourceFolder.ExternalId.Value,
                        FileNameWithExtension = "dependent-artifact.bin",
                        FileContentType = "application/octet-stream",
                        FileSizeInBytes = dependentUploadContent.Length
                    }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        initiateResponse.DirectUploads.Should().NotBeNull();

        MarkUploadAsDependentOf(
            uploadExternalId: dependentUploadExternalId,
            parentFileExternalId: parentFile.ExternalId);

        var sourceFolderId = GetFolderInternalId(sourceFolder.ExternalId);
        var destinationFolderId = GetFolderInternalId(destinationFolder.ExternalId);

        GetUploadFolderId(dependentUploadExternalId).Should().Be(sourceFolderId);

        //when
        await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [parentFile.ExternalId],
                FolderExternalIds: [],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: destinationFolder.ExternalId,
                DestinationPosition: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        GetUploadFolderId(dependentUploadExternalId).Should().Be(destinationFolderId);

        var uploadResults = await Api.PreSignedFiles.MultiFileDirectUpload(
            preSignedUrl: initiateResponse.DirectUploads!.PreSignedMultiFileDirectUploadLink,
            files: new Dictionary<FileUploadExtId, byte[]>
            {
                [dependentUploadExternalId] = dependentUploadContent
            },
            cookie: AppOwner.Cookie);

        uploadResults.Should().ContainSingle();

        var dependentFolderIds = GetDependentFilesFolderIds(parentFile.ExternalId);

        dependentFolderIds.Should().NotBeEmpty();
        dependentFolderIds.Should().AllSatisfy(
            folderId => folderId.Should().Be(destinationFolderId));
    }

    [Fact]
    public async Task moving_dependent_file_directly_should_be_rejected()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var sourceFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var destinationFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: sourceFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        await Api.MediaProcessing.UploadThumbnail(
            workspaceExternalId: workspace.ExternalId,
            parentFileExternalId: parentFile.ExternalId,
            thumbnailContent: RandomBytes(512),
            thumbnailFileName: "thumb-small.jpg",
            thumbnailContentType: "image/jpeg",
            variant: ThumbnailVariant.Small,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var sourceFolderId = GetFolderInternalId(sourceFolder.ExternalId);

        var dependentFileExternalId = GetDependentFileExternalIds(parentFile.ExternalId)
            .Should().ContainSingle().Subject;

        //when
        var act = async () => await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [FileExtId.Parse(dependentFileExternalId)],
                FolderExternalIds: [],
                FileUploadExternalIds: [],
                DestinationFolderExternalId: destinationFolder.ExternalId,
                DestinationPosition: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var exception = await act.Should().ThrowAsync<TestApiCallException>();
        exception.Which.StatusCode.Should().Be(404);

        var dependentFolderIds = GetDependentFilesFolderIds(parentFile.ExternalId);

        dependentFolderIds.Should().NotBeEmpty();
        dependentFolderIds.Should().AllSatisfy(
            folderId => folderId.Should().Be(sourceFolderId));
    }

    [Fact]
    public async Task moving_dependent_upload_directly_should_be_rejected()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var sourceFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var destinationFolder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var parentFile = await UploadFile(
            content: RandomBytes(2048),
            fileName: "parent-photo.jpg",
            contentType: "image/jpeg",
            folder: sourceFolder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(parentFile.ExternalId, AppOwner);

        var dependentUploadExternalId = FileUploadExtId.NewId();

        await Api.Uploads.BulkInitiate(
            workspaceExternalId: workspace.ExternalId,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = dependentUploadExternalId.Value,
                        FolderExternalId = sourceFolder.ExternalId.Value,
                        FileNameWithExtension = "dependent-artifact.bin",
                        FileContentType = "application/octet-stream",
                        FileSizeInBytes = 1024
                    }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        MarkUploadAsDependentOf(
            uploadExternalId: dependentUploadExternalId,
            parentFileExternalId: parentFile.ExternalId);

        var sourceFolderId = GetFolderInternalId(sourceFolder.ExternalId);

        //when
        var act = async () => await Api.Folders.MoveItems(
            workspaceExternalId: workspace.ExternalId,
            request: new MoveItemsToFolderRequestDto(
                FileExternalIds: [],
                FolderExternalIds: [],
                FileUploadExternalIds: [dependentUploadExternalId],
                DestinationFolderExternalId: destinationFolder.ExternalId,
                DestinationPosition: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var exception = await act.Should().ThrowAsync<TestApiCallException>();
        exception.Which.StatusCode.Should().Be(404);

        GetUploadFolderId(dependentUploadExternalId).Should().Be(sourceFolderId);
    }

    private int GetFolderInternalId(FolderExtId externalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT fo_id
                     FROM fo_folders
                     WHERE fo_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"Folder '{externalId}' was not found in the database.");

        return rows[0];
    }

    private List<int?> GetDependentFilesFolderIds(FileExtId parentFileExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT child.fi_folder_id
                     FROM fi_files AS child
                     WHERE child.fi_parent_file_id = (
                         SELECT fi_id
                         FROM fi_files
                         WHERE fi_external_id = $parentExternalId
                     )
                     """,
                readRowFunc: reader => reader.GetInt32OrNull(0))
            .WithParameter("$parentExternalId", parentFileExternalId.Value)
            .Execute();
    }

    private List<string> GetDependentFileExternalIds(FileExtId parentFileExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT child.fi_external_id
                     FROM fi_files AS child
                     WHERE child.fi_parent_file_id = (
                         SELECT fi_id
                         FROM fi_files
                         WHERE fi_external_id = $parentExternalId
                     )
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$parentExternalId", parentFileExternalId.Value)
            .Execute();
    }

    private int? GetUploadFolderId(FileUploadExtId uploadExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT fu_folder_id
                     FROM fu_file_uploads
                     WHERE fu_external_id = $uploadExternalId
                     """,
                readRowFunc: reader => reader.GetInt32OrNull(0))
            .WithParameter("$uploadExternalId", uploadExternalId.Value)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"FileUpload '{uploadExternalId}' was not found in the database.");

        return rows[0];
    }

    private void MarkUploadAsDependentOf(
        FileUploadExtId uploadExternalId,
        FileExtId parentFileExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var result = connection
            .NonQueryCmd(
                sql: """
                     UPDATE fu_file_uploads
                     SET fu_parent_file_id = (
                         SELECT fi_id
                         FROM fi_files
                         WHERE fi_external_id = $parentExternalId
                     )
                     WHERE fu_external_id = $uploadExternalId
                     """)
            .WithParameter("$parentExternalId", parentFileExternalId.Value)
            .WithParameter("$uploadExternalId", uploadExternalId.Value)
            .Execute();

        if (result.AffectedRows != 1)
            throw new InvalidOperationException(
                $"Could not mark FileUpload '{uploadExternalId}' as dependent of File '{parentFileExternalId}'.");
    }

    private static byte[] RandomBytes(int length)
    {
        var buffer = new byte[length];
        System.Random.Shared.NextBytes(buffer);
        return buffer;
    }
}
