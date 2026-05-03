using System.Text;
using FluentAssertions;
using PlikShare.BulkDelete.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_file_delete_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_file_delete_tests(
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
    public async Task uploaded_file_should_be_inaccessible_after_delete(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        var content = Encoding.UTF8.GetBytes("this file will be deleted");

        var file = await UploadFile(
            content: content,
            fileName: "delete-me.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        await WaitForFileUnlocked(file.ExternalId, AppOwner);

        // sanity: file shows up in the folder listing
        var beforeDelete = await Api.Folders.Get(
            workspaceExternalId: setup.Workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        beforeDelete.Files.Should().Contain(f => f.ExternalId == file.ExternalId.Value);

        // capture a raw client now so we can verify physical removal afterwards.
        using var rawClient = RawStorageClient.For(setup.Storage, MainVolume);

        //when
        await Api.Workspaces.BulkDelete(
            externalId: setup.Workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [file.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — bulk-delete is async (queue job: bulk-delete-s3-files). Poll the
        // folder listing until the file is gone, with a budget large enough for
        // slow S3 providers (B2 in particular).
        await WaitForFileToDisappear(
            workspace: setup.Workspace,
            folder: folder,
            file: file,
            timeout: TimeSpan.FromSeconds(30));

        // download must now be impossible — the DB row is gone, so the
        // GetDownloadLink endpoint rejects the request.
        var downloadAttempt = async () => await Api.Files.GetDownloadLink(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: file.ExternalId,
            contentDisposition: "attachment",
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        await downloadAttempt.Should().ThrowAsync<TestApiCallException>();

        // physical storage-level check: object must be unreachable on the underlying
        // storage too. The DB row going away is not enough — the bulk-delete queue
        // job has to actually run and call the backend's delete primitive.
        await rawClient.WaitForFileGone(
            bucketName: setup.BucketName,
            fileExternalId: file.ExternalId,
            timeout: TimeSpan.FromSeconds(30));
    }

    private async Task WaitForFileToDisappear(
        AppWorkspace workspace,
        AppFolder folder,
        AppFile file,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var current = await Api.Folders.Get(
                workspaceExternalId: workspace.ExternalId,
                folderExternalId: folder.ExternalId,
                cookie: AppOwner.Cookie,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            var files = current.Files ?? [];

            if (files.All(f => f.ExternalId != file.ExternalId.Value))
                return;

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            $"File '{file.ExternalId}' was not removed from folder listing within {timeout}.");
    }
}
