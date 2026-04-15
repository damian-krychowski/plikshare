using System.Text;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.Encryption;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class full_encryption_session_filter_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public full_encryption_session_filter_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task request_on_full_encryption_storage_without_session_cookie_returns_423()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        await WaitForBucketReady(workspace, AppOwner);

        // Strip the session cookie to simulate a request from a locked state.
        var lockedWorkspace = workspace with { FullEncryptionSession = null };

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: lockedWorkspace.ExternalId,
                fileExternalId: PlikShare.Files.Id.FileExtId.NewId(),
                contentDisposition: "attachment",
                cookie: AppOwner.Cookie,
                fullEncryptionSession: lockedWorkspace.FullEncryptionSession));

        //then
        apiError.StatusCode.Should().Be(423);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("full-encryption-session-required");
    }

    [Fact]
    public async Task request_on_full_encryption_storage_with_valid_session_cookie_succeeds()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        //when
        var uploaded = await UploadFile(
            content: Encoding.UTF8.GetBytes("session filter test"),
            fileName: "ok.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var downloaded = await DownloadFile(
            fileExternalId: uploaded.ExternalId,
            workspace: workspace,
            user: AppOwner);

        //then
        downloaded.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("session filter test"));
    }

    [Fact]
    public async Task request_on_full_encryption_storage_with_tampered_session_cookie_returns_423()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        await WaitForBucketReady(workspace, AppOwner);

        // Replace the protected cookie value with garbage — DataProtection should reject it.
        var tamperedCookie = new GenericCookie(
            name: workspace.FullEncryptionSession!.Name,
            value: "tampered-garbage-value-that-cannot-be-unprotected");

        var tamperedWorkspace = workspace with { FullEncryptionSession = tamperedCookie };

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: tamperedWorkspace.ExternalId,
                fileExternalId: PlikShare.Files.Id.FileExtId.NewId(),
                contentDisposition: "attachment",
                cookie: AppOwner.Cookie,
                fullEncryptionSession: tamperedWorkspace.FullEncryptionSession));

        //then
        apiError.StatusCode.Should().Be(423);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("full-encryption-session-required");
    }

    [Fact]
    public async Task request_with_session_cookie_for_wrong_storage_returns_423()
    {
        //given
        var storageA = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var storageB = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspaceA = await CreateWorkspace(storage: storageA, user: AppOwner);
        var workspaceB = await CreateWorkspace(storage: storageB, user: AppOwner);

        await WaitForBucketReady(workspaceA, AppOwner);
        await WaitForBucketReady(workspaceB, AppOwner);

        // Build a workspace B request carrying A's session cookie — wrong cookie name,
        // so the server does not see a matching FullEncryptionSession_{B} cookie.
        var mismatchedWorkspaceB = workspaceB with { FullEncryptionSession = workspaceA.FullEncryptionSession };

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: mismatchedWorkspaceB.ExternalId,
                fileExternalId: PlikShare.Files.Id.FileExtId.NewId(),
                contentDisposition: "attachment",
                cookie: AppOwner.Cookie,
                fullEncryptionSession: mismatchedWorkspaceB.FullEncryptionSession));

        //then
        apiError.StatusCode.Should().Be(423);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("full-encryption-session-required");
    }

    [Fact]
    public async Task multistep_upload_in_progress_cannot_continue_after_session_is_dropped()
    {
        //given — file sized for a 2-part multistep upload on a Full encryption storage
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        await WaitForBucketReady(workspace, AppOwner);

        // FirstFilePartSizeInBytes + 1 forces a 2-part multistep upload.
        var content = new byte[Aes256GcmStreamingV2.GetFirstFilePartSizeInBytes(chainStepsCount: 0) + 1];
        new Random(42).NextBytes(content);

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
                        FileNameWithExtension = "midflight.bin",
                        FileContentType = "application/octet-stream",
                        FileSizeInBytes = content.Length
                    }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            fullEncryptionSession: workspace.FullEncryptionSession);

        initiateResponse.MultiStepChunkUploads.Should().NotBeNull();
        initiateResponse.MultiStepChunkUploads!.Should().HaveCount(1);

        var multiStep = initiateResponse.MultiStepChunkUploads[0];
        multiStep.ExpectedPartsCount.Should().BeGreaterThan(1,
            "test requires a multi-part upload to meaningfully exercise the filter mid-flight");

        var uploadExtId = FileUploadExtId.Parse(multiStep.FileUploadExternalId);

        // Upload part 1 (with session)
        var part1Initiate = await Api.Uploads.InitiatePartUpload(
            workspaceExternalId: workspace.ExternalId,
            fileUploadExternalId: uploadExtId,
            partNumber: 1,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            fullEncryptionSession: workspace.FullEncryptionSession);

        var part1Bytes = content
            .AsSpan()
            .Slice(
                (int)part1Initiate.StartsAtByte,
                (int)(part1Initiate.EndsAtByte - part1Initiate.StartsAtByte + 1))
            .ToArray();

        var part1ETag = await Api.PreSignedFiles.UploadFilePart(
            preSignedUrl: part1Initiate.UploadPreSignedUrl,
            content: part1Bytes,
            contentType: "application/octet-stream",
            cookie: AppOwner.Cookie);

        if (part1Initiate.IsCompleteFilePartUploadCallbackRequired)
        {
            await Api.Uploads.CompletePartUpload(
                workspaceExternalId: workspace.ExternalId,
                fileUploadExternalId: uploadExtId,
                partNumber: 1,
                request: new Uploads.FilePartUpload.Complete.Contracts.CompleteFilePartUploadRequestDto(
                    ETag: part1ETag),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                fullEncryptionSession: workspace.FullEncryptionSession);
        }

        //when — session is dropped mid-flight; part 2 initiate must be rejected by filter
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Uploads.InitiatePartUpload(
                workspaceExternalId: workspace.ExternalId,
                fileUploadExternalId: uploadExtId,
                partNumber: 2,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery,
                fullEncryptionSession: null));

        //then
        apiError.StatusCode.Should().Be(423);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("full-encryption-session-required");
    }

    [Fact]
    public async Task file_uploaded_to_storage_b_cannot_be_downloaded_when_only_storage_a_is_unlocked()
    {
        //given — two Full encryption storages, each with a file uploaded while unlocked
        var storageA = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var storageB = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspaceA = await CreateWorkspace(storage: storageA, user: AppOwner);
        var workspaceB = await CreateWorkspace(storage: storageB, user: AppOwner);

        var folderB = await CreateFolder(parent: null, workspace: workspaceB, user: AppOwner);

        var contentInB = Encoding.UTF8.GetBytes("file content stored inside storage B");
        var fileInB = await UploadFile(
            content: contentInB,
            fileName: "file-in-b.txt",
            contentType: "text/plain",
            folder: folderB,
            workspace: workspaceB,
            user: AppOwner);

        // Attempt to access B while presenting ONLY A's session cookie (B is effectively locked)
        var workspaceBWithOnlyACookie = workspaceB with
        {
            FullEncryptionSession = workspaceA.FullEncryptionSession
        };

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Files.GetDownloadLink(
                workspaceExternalId: workspaceBWithOnlyACookie.ExternalId,
                fileExternalId: fileInB.ExternalId,
                contentDisposition: "attachment",
                cookie: AppOwner.Cookie,
                fullEncryptionSession: workspaceBWithOnlyACookie.FullEncryptionSession));

        //then — B's session cookie is missing, filter blocks before any file logic runs
        apiError.StatusCode.Should().Be(423);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("full-encryption-session-required");
    }

    [Fact]
    public async Task request_on_none_encryption_storage_without_session_cookie_succeeds()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        //when — upload+download without any session cookie (None storage doesn't need it)
        var uploaded = await UploadFile(
            content: Encoding.UTF8.GetBytes("no-encryption content"),
            fileName: "plain.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var downloaded = await DownloadFile(
            fileExternalId: uploaded.ExternalId,
            workspace: workspace,
            user: AppOwner);

        //then
        downloaded.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("no-encryption content"));
    }

    [Fact]
    public async Task bulk_delete_on_full_encryption_storage_removes_files_and_produces_audit_log()
    {
        // Bulk delete is on the Workspaces endpoint, not Files, so ValidateFullEncryptionSessionFilter
        // does NOT apply — the operation does not need the session cookie to run. This reflects that
        // deletion only needs the ciphertext file paths, never the KEK. This test documents the
        // current behavior AND verifies bulk delete is end-to-end functional on Full storage.

        //given — upload multiple files with the Full storage session
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var file1 = await UploadFile(
            content: Encoding.UTF8.GetBytes("first file to delete"),
            fileName: "delete-1.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var file2 = await UploadFile(
            content: Encoding.UTF8.GetBytes("second file to delete"),
            fileName: "delete-2.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var file3 = await UploadFile(
            content: Encoding.UTF8.GetBytes("third file to delete"),
            fileName: "delete-3.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //when — bulk delete all three without passing session cookie (filter not applied)
        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [file1.ExternalId, file2.ExternalId, file3.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then — audit log confirms the delete request was accepted
        await AssertAuditLogContains<Audit.Workspace.BulkDeleteRequested>(
            expectedEventType: AuditLogEventTypes.Workspace.BulkDeleteRequested,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Files.Should().Contain(f => f.ExternalId == file1.ExternalId);
                details.Files.Should().Contain(f => f.ExternalId == file2.ExternalId);
                details.Files.Should().Contain(f => f.ExternalId == file3.ExternalId);
                details.Folders.Should().BeEmpty();
                details.FileUploads.Should().BeEmpty();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Critical);

        // and — files are no longer reachable via the download-link endpoint
        // (which IS behind the session filter — so we pass the cookie here)
        var downloadAttempt = async () => await Api.Files.GetDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file1.ExternalId,
            contentDisposition: "attachment",
            cookie: AppOwner.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        // Deletion is processed via a queue job so it is not instantaneous — retry briefly.
        await WaitUntilFileIsDeleted(downloadAttempt);
    }

    private static async Task WaitUntilFileIsDeleted(Func<Task> getDownloadLinkAttempt)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                await getDownloadLinkAttempt();
            }
            catch (TestApiCallException ex) when (ex.StatusCode == 404)
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException(
            "File was still reachable via download link after bulk delete request.");
    }

    [Fact]
    public async Task request_on_managed_encryption_storage_without_session_cookie_succeeds()
    {
        //given
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Managed);

        var workspace = await CreateWorkspace(
            storage: storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        //when — Managed storage's keys are held server-side, no user session needed
        var uploaded = await UploadFile(
            content: Encoding.UTF8.GetBytes("managed content"),
            fileName: "managed.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var downloaded = await DownloadFile(
            fileExternalId: uploaded.ExternalId,
            workspace: workspace,
            user: AppOwner);

        //then
        downloaded.Should().BeEquivalentTo(Encoding.UTF8.GetBytes("managed content"));
    }
}
