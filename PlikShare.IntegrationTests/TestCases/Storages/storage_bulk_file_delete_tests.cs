using System.Text;
using FluentAssertions;
using PlikShare.BulkDelete.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_bulk_file_delete_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_bulk_file_delete_tests(
        HostFixture8081 hostFixture,
        LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesAndEncryptionTypes),
        MemberType = typeof(StorageTheoryData))]
    public async Task bulk_delete_should_remove_all_files_in_single_request(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(
            this, 
            provider, 
            encryptionType, 
            AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        const int fileCount = 7;
        var uploadedFiles = new List<AppFile>();

        for (var i = 0; i < fileCount; i++)
        {
            var content = Encoding.UTF8.GetBytes($"file #{i} content payload");
            var file = await UploadFile(
                content: content,
                fileName: $"bulk-delete-{i}.txt",
                contentType: "text/plain",
                folder: folder,
                workspace: setup.Workspace,
                user: AppOwner);
            uploadedFiles.Add(file);
        }

        await WaitForFilesUnlocked(
            fileExternalIds: uploadedFiles.Select(f => f.ExternalId).ToList(),
            user: AppOwner);

        var beforeDelete = await Api.Folders.Get(
            workspaceExternalId: setup.Workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        beforeDelete.Files
            .Where(f => uploadedFiles.Any(u => u.ExternalId.Value == f.ExternalId))
            .Should().HaveCount(fileCount);

        // capture a raw client now so we can verify physical removal afterwards.
        using var rawClient = RawStorageClient.For(setup.Storage, MainVolume);

        //when
        await Api.Workspaces.BulkDelete(
            externalId: setup.Workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = uploadedFiles.Select(f => f.ExternalId).ToList(),
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await WaitForAllFilesToDisappear(
            workspace: setup.Workspace,
            folder: folder,
            files: uploadedFiles,
            timeout: TimeSpan.FromSeconds(30));

        // physical storage-level check: every object must be unreachable on the
        // underlying storage too. The DB rows going away is not enough — the
        // bulk-delete queue job has to actually run the backend's delete primitive
        // for all 7 files.
        foreach (var file in uploadedFiles)
        {
            await rawClient.WaitForFileGone(
                bucketName: setup.BucketName,
                fileExternalId: file.ExternalId,
                timeout: TimeSpan.FromSeconds(30));
        }
    }

    private async Task WaitForAllFilesToDisappear(
        AppWorkspace workspace,
        AppFolder folder,
        List<AppFile> files,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        var fileExternalIds = files.Select(f => f.ExternalId.Value).ToHashSet();

        while (DateTime.UtcNow < deadline)
        {
            var current = await Api.Folders.Get(
                workspaceExternalId: workspace.ExternalId,
                folderExternalId: folder.ExternalId,
                cookie: AppOwner.Cookie,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            var stillThere = current
                .Files
                ?.Count(f => fileExternalIds.Contains(f.ExternalId)) ?? 0;

            if (stillThere == 0)
                return;

            await Task.Delay(50);
        }

        throw new InvalidOperationException(
            $"Not all {files.Count} files were removed from folder listing within {timeout}.");
    }
}
