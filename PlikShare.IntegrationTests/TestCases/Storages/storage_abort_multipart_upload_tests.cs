using FluentAssertions;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Uploads.FilePartUpload.Complete.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_abort_multipart_upload_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_abort_multipart_upload_tests(
        HostFixture8081 hostFixture,
        LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    // Encryption axis is intentionally pinned to None for abort: the abort code path
    // doesn't read file content or branch on encryption mode, and the on-backend state
    // shape after a partial multipart upload is identical across None / Managed / Full
    // (only the bytes inside parts differ). Multi-encryption coverage of the upload
    // happy path lives in storage_multipart_upload_tests.
    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesWithNoEncryption),
        MemberType = typeof(StorageTheoryData))]
    public async Task aborted_multipart_upload_should_remove_upload_state_from_db_and_storage(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        // 11 MB forces MultiStepChunkUpload across all encryption modes (see notes
        // in storage_multipart_upload_tests). Anything smaller may resolve to
        // SingleChunkUpload or DirectUpload and bypass the abort code path.
        var content = new byte[11 * 1024 * 1024];
        System.Random.Shared.NextBytes(content);

        var (uploadExtId, fileExtId) = await StartPartialMultipartUpload(
            content: content,
            partsToUpload: 1,
            fileName: "abort-me.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        using var rawClient = RawStorageClient.For(setup.Storage, MainVolume);

        //when
        await Api.Workspaces.BulkDelete(
            externalId: setup.Workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [],
                FolderExternalIds = [],
                FileUploadExternalIds = [uploadExtId]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — abort is async (queue job: abort-s3-upload). Wait for the job to
        // land in the completed table before asserting on storage state, otherwise
        // we may race the cleanup on slower providers (B2 in particular).
        await WaitForAbortJobCompleted(
            fileExternalId: fileExtId,
            timeout: TimeSpan.FromSeconds(60));

        // upload row is removed from DB synchronously by BulkDelete itself, before
        // the abort job runs.
        HasFileUploadPersistedRow(uploadExtId).Should().BeFalse(
            "BulkDelete deletes the fu_file_uploads row in the same transaction that " +
            "enqueues the abort job.");

        // download by FileExtId must be impossible — the file was never committed,
        // so the GetDownloadLink endpoint has no fi_files row to resolve against.
        var downloadAttempt = async () => await Api.Files.GetDownloadLink(
            workspaceExternalId: setup.Workspace.ExternalId,
            fileExternalId: fileExtId,
            contentDisposition: "attachment",
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: setup.Workspace.WorkspaceEncryptionSession);

        await downloadAttempt.Should().ThrowAsync<TestApiCallException>();

        // physical storage check: the would-be committed object must not be
        // reachable. For S3 a CompleteMultipartUpload was never called so no object
        // ever existed. For Azure direct-SAS an uncommitted-blocks shell is gone
        // after AbortMultiPartUpload's defensive DeleteIfExists. For HardDrive the
        // partial .part files staged before abort must have been swept by the abort
        // job's per-etag File.Delete loop.
        await rawClient.WaitForFileGone(
            bucketName: setup.BucketName,
            fileExternalId: fileExtId,
            timeout: TimeSpan.FromSeconds(30));
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesWithNoEncryption),
        MemberType = typeof(StorageTheoryData))]
    public async Task abort_should_succeed_when_no_parts_were_uploaded_yet(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        // Initiate only — no parts uploaded. Validates the "S3 multipart upload was
        // started server-side but never received any parts" edge case, where some
        // backends still need cleanup (S3: in-flight multipart upload to drop) and
        // others don't (HD: no .part files yet, Azure: no uncommitted blocks yet).
        var content = new byte[11 * 1024 * 1024];

        var (uploadExtId, fileExtId) = await StartPartialMultipartUpload(
            content: content,
            partsToUpload: 0,
            fileName: "never-uploaded.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: setup.Workspace,
            user: AppOwner);

        //when
        await Api.Workspaces.BulkDelete(
            externalId: setup.Workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [],
                FolderExternalIds = [],
                FileUploadExternalIds = [uploadExtId]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await WaitForAbortJobCompleted(
            fileExternalId: fileExtId,
            timeout: TimeSpan.FromSeconds(60));

        HasFileUploadPersistedRow(uploadExtId).Should().BeFalse();
    }

    private async Task<(FileUploadExtId UploadExtId, FileExtId FileExtId)> StartPartialMultipartUpload(
        byte[] content,
        int partsToUpload,
        string fileName,
        string contentType,
        AppFolder folder,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        await WaitForBucketReady(workspace, user);

        var fileUploadExternalId = FileUploadExtId.NewId();

        var initiateResponse = await Api.Uploads.BulkInitiate(
            workspaceExternalId: workspace.ExternalId,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileUploadExternalId.Value,
                        FolderExternalId = folder.ExternalId.Value,
                        FileNameWithExtension = fileName,
                        FileContentType = contentType,
                        FileSizeInBytes = content.Length
                    }
                ]
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        if (initiateResponse.MultiStepChunkUploads is not { Count: > 0 })
            throw new InvalidOperationException(
                "Expected BulkInitiate to resolve to MultiStepChunkUpload but got a " +
                "different upload algorithm. Increase the file size or check the " +
                "storage configuration to make sure the multipart path is exercised.");

        var multiStep = initiateResponse.MultiStepChunkUploads[0];
        var uploadExtId = FileUploadExtId.Parse(multiStep.FileUploadExternalId);

        if (partsToUpload >= multiStep.ExpectedPartsCount)
            throw new ArgumentException(
                $"partsToUpload ({partsToUpload}) must be less than ExpectedPartsCount " +
                $"({multiStep.ExpectedPartsCount}); the abort path is only meaningful " +
                "when at least one part is missing.",
                nameof(partsToUpload));

        for (var partNumber = 1; partNumber <= partsToUpload; partNumber++)
        {
            var partInitiate = await Api.Uploads.InitiatePartUpload(
                workspaceExternalId: workspace.ExternalId,
                fileUploadExternalId: uploadExtId,
                partNumber: partNumber,
                cookie: user.Cookie,
                antiforgery: user.Antiforgery,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            var partContent = content
                .AsSpan()
                .Slice(
                    (int)partInitiate.StartsAtByte,
                    (int)(partInitiate.EndsAtByte - partInitiate.StartsAtByte + 1))
                .ToArray();

            var eTag = await Api.PreSignedFiles.UploadFilePart(
                preSignedUrl: partInitiate.UploadPreSignedUrl,
                content: partContent,
                contentType: contentType,
                cookie: user.Cookie);

            if (partInitiate.CompleteCallback is not null)
            {
                var etagToSend = partInitiate.CompleteCallback.ETagSourceHeader is null ? null : eTag;

                await Api.Uploads.CompletePartUpload(
                    workspaceExternalId: workspace.ExternalId,
                    fileUploadExternalId: uploadExtId,
                    partNumber: partNumber,
                    request: new CompleteFilePartUploadRequestDto(ETag: etagToSend),
                    cookie: user.Cookie,
                    antiforgery: user.Antiforgery,
                    workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);
            }
        }

        var fileExtId = GetFileExternalIdForUpload(uploadExtId);
        return (uploadExtId, fileExtId);
    }

    private FileExtId GetFileExternalIdForUpload(FileUploadExtId uploadExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT fu_file_external_id
                     FROM fu_file_uploads
                     WHERE fu_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => FileExtId.Parse(reader.GetString(0)))
            .WithParameter("$externalId", uploadExternalId.Value)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"FileUpload '{uploadExternalId}' was not found in the database.");

        return rows[0];
    }

    private async Task WaitForAbortJobCompleted(
        FileExtId fileExternalId,
        TimeSpan timeout)
    {
        // q_definition is JSON serialized with camelCase property names (see
        // PlikShare.Core.Utils.Json), so AbortMultipartUploadQueueJobDefinition.FileExternalId
        // appears as "fileExternalId":"fi_..." in storage. We match on a substring
        // rather than parsing JSON.
        var marker = $"\"fileExternalId\":\"{fileExternalId.Value}\"";
        var pollInterval = TimeSpan.FromMilliseconds(200);
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            using (var connection = HostFixture.Db.OpenConnection())
            {
                var found = connection
                    .Cmd(
                        sql: """
                             SELECT 1
                             FROM qc_queue_completed
                             WHERE qc_job_type = 'abort-multipart-upload'
                               AND qc_definition LIKE $marker
                             LIMIT 1
                             """,
                        readRowFunc: reader => reader.GetInt32(0))
                    .WithParameter("$marker", $"%{marker}%")
                    .Execute();

                if (found.Count > 0)
                    return;
            }

            await Task.Delay(pollInterval);
        }

        throw new InvalidOperationException(
            $"Abort job for FileExternalId '{fileExternalId}' did not complete within {timeout}.");
    }
}
