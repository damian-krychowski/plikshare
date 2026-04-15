using System.Text;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Core.Encryption;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Files;

[Collection(IntegrationTestsCollection.Name)]
public class upload_and_download_tests : TestFixture
{
    public upload_and_download_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task small_file_upload_and_download_should_return_same_content(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = Encoding.UTF8.GetBytes("Hello, PlikShare integration test!");

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "test-file.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task binary_file_upload_and_download_should_return_same_content(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = new byte[1024];
        new Random(42).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "binary-test.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task multiple_files_upload_and_download_should_return_same_content(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var file1Content = Encoding.UTF8.GetBytes("First file content");
        var file2Content = Encoding.UTF8.GetBytes("Second file content - a bit longer");
        var file3Content = new byte[512];
        new Random(123).NextBytes(file3Content);

        //when
        var uploadedFiles = await UploadFiles(
            files:
            [
                (file1Content, "file1.txt", "text/plain"),
                (file2Content, "file2.txt", "text/plain"),
                (file3Content, "file3.bin", "application/octet-stream")
            ],
            folder: folder,
            workspace: workspace,
            user: user);

        //then
        uploadedFiles.Should().HaveCount(3);

        var downloaded1 = await DownloadFile(uploadedFiles[0].ExternalId, workspace, user);
        var downloaded2 = await DownloadFile(uploadedFiles[1].ExternalId, workspace, user);
        var downloaded3 = await DownloadFile(uploadedFiles[2].ExternalId, workspace, user);

        downloaded1.Should().BeEquivalentTo(file1Content);
        downloaded2.Should().BeEquivalentTo(file2Content);
        downloaded3.Should().BeEquivalentTo(file3Content);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task large_file_multistep_upload_and_download_should_return_same_content(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        // Use a size that aligns with encrypted segment boundaries:
        // FirstFilePartSizeInBytes (10,485,559) + SegmentsCiphertextSize (1,048,560) = 11,534,119
        // This ensures part 2 fills exactly one segment, avoiding a Kestrel PipeReader
        // edge case where ReadAtLeastAsync returns IsCompleted on a partially-consumed buffer.
        var originalContent = new byte[11_534_119];
        new Random(789).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "large-file.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task large_file_multistep_upload_unaligned_part_should_work(
        StorageEncryptionType encryptionType)
    {
        // This test reproduces a bug in Aes256GcmStreaming.CopyIntoBufferReadyForInPlaceEncryption:
        // When the HTTP client sends the entire body at once (not streamed in chunks),
        // Kestrel's PipeReader returns IsCompleted=true on the first ReadAtLeastAsync call.
        // The method copies only one segment's worth of data (1,048,560 bytes) but then
        // checks readResult.IsCompleted && bytesToCopyLeft > 0 — which throws, even though
        // the remaining 217 bytes ARE available in the PipeReader's buffer.
        //
        // File size: FirstFilePartSizeInBytes + SegmentsCiphertextSize + 217
        //   Part 1: 10,485,559 bytes (full first part)
        //   Part 2: 1,048,777 bytes (one full segment + 217 bytes leftover)
        //
        // Part 2 triggers the bug because ReadAtLeastAsync(1,048,560) returns
        // the full 1,048,777 buffer with IsCompleted=true. After copying 1,048,560,
        // bytesToCopyLeft=217 > 0 and IsCompleted=true → false throw.

        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = new byte[10_485_559 + 1_048_560 + 217]; // 11,534,336 bytes
        new Random(101).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "unaligned-file.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task one_byte_file_should_upload_and_download(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = new byte[] { 0x42 };

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "one-byte.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_exactly_one_segment_should_upload_and_download(
        StorageEncryptionType encryptionType)
    {
        // FirstSegmentCiphertextSize = SegmentSize - TagSize - HeaderSize
        // This fills exactly the first segment's plaintext capacity — no spill.

        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var firstSegmentCiphertextSize = Aes256GcmStreamingV2.GetFirstSegmentCiphertextSize(chainStepsCount: 0); // 1,048,518
        var originalContent = new byte[firstSegmentCiphertextSize];
        new Random(200).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "exact-one-segment.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_one_segment_plus_one_byte_should_upload_and_download(
        StorageEncryptionType encryptionType)
    {
        // FirstSegmentCiphertextSize + 1 → spills 1 byte into segment 2.
        // Tests minimum second-segment plaintext.

        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var firstSegmentCiphertextSize = Aes256GcmStreamingV2.GetFirstSegmentCiphertextSize(chainStepsCount: 0);
        var originalContent = new byte[firstSegmentCiphertextSize + 1];
        new Random(201).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "one-segment-plus-one.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_exactly_max_single_part_should_upload_and_download(
        StorageEncryptionType encryptionType)
    {
        // FirstFilePartSizeInBytes = 10,485,559 → exactly 10 segments, max single-part file.
        // Tests the boundary: one more byte would trigger multi-step.

        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = new byte[Aes256GcmStreamingV2.GetFirstFilePartSizeInBytes(chainStepsCount: 0)];
        new Random(202).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "exact-max-single-part.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_one_byte_over_max_single_part_should_upload_and_download(
        StorageEncryptionType encryptionType)
    {
        // FirstFilePartSizeInBytes + 1 → minimum 2-part file.
        // Part 2 has exactly 1 byte of plaintext.

        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = new byte[Aes256GcmStreamingV2.GetFirstFilePartSizeInBytes(chainStepsCount: 0) + 1];
        new Random(203).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "one-byte-over-max-part.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_spanning_three_parts_should_upload_and_download(
        StorageEncryptionType encryptionType)
    {
        // FirstFilePartSizeInBytes + FilePartSizeInBytes + 1000
        // 3 parts: full part 1, full part 2, small part 3.
        // Tests middle part as fully-filled non-first, non-last part.

        //given
        var user = await SignIn(
            Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(
            storage,
            user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var originalContent = new byte[
            Aes256GcmStreamingV2.GetFirstFilePartSizeInBytes(chainStepsCount: 0) +
            Aes256GcmStreamingV2.FilePartSizeInBytes +
            1000];
        new Random(204).NextBytes(originalContent);

        //when
        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "three-parts.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        var downloadedContent = await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        downloadedContent.Should().HaveCount(originalContent.Length);
        downloadedContent.Should().BeEquivalentTo(originalContent);
    }

    // --- Audit log tests ---

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task uploading_file_should_produce_upload_bulk_initiated_audit_log_entry(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(storage, user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        //when
        await UploadFile(
            content: Encoding.UTF8.GetBytes("audit test content"),
            fileName: "audit-upload.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: user);

        //then
        await AssertAuditLogContains<Audit.File.UploadInitiated>(
            expectedEventType: AuditLogEventTypes.File.UploadInitiated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.FileUploads.Should().Contain(f => f.Name == "audit-upload.txt");
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task completing_multistep_upload_should_produce_upload_completed_audit_log_entry(
        StorageEncryptionType encryptionType)
    {
        //given - file >10MB triggers MultiStepChunkUpload which calls CompleteUpload
        var user = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(storage, user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        // Size aligned for encrypted segments: FirstFilePartSizeInBytes + SegmentsCiphertextSize.
        var content = new byte[11_534_119];
        new Random(42).NextBytes(content);

        //when
        var uploadedFile = await UploadFile(
            content: content,
            fileName: "audit-multistep.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //then
        await AssertAuditLogContains<Audit.File.UploadCompleted>(
            expectedEventType: AuditLogEventTypes.File.UploadCompleted,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.FileUpload.FileExternalId.Should().Be(uploadedFile.ExternalId);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task direct_upload_should_produce_multi_file_direct_uploaded_audit_log_entry(
        StorageEncryptionType encryptionType)
    {
        //given — on HardDrive storage, small files always go through the DirectUpload path
        // (HardDriveStorageClient.ResolveUploadAlgorithm returns DirectUpload whenever
        // filePartsCount == 1), regardless of encryption mode.
        var user = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(storage, user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        //when
        var uploadedFile = await UploadFile(
            content: Encoding.UTF8.GetBytes("direct upload audit test"),
            fileName: "audit-direct.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: user);

        //then
        await AssertAuditLogContains<Audit.File.MultiUploadCompleted>(
            expectedEventType: AuditLogEventTypes.File.MultiUploadCompleted,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.FileUploads.Should().Contain(f => f.FileExternalId == uploadedFile.ExternalId);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task downloading_file_should_produce_download_link_generated_audit_log_entry(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(storage, user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var uploadedFile = await UploadFile(
            content: Encoding.UTF8.GetBytes("download audit test"),
            fileName: "audit-download.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        await Api.Files.GetDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            contentDisposition: "attachment",
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        await AssertAuditLogContains<Audit.File.DownloadLinkGenerated>(
            expectedEventType: AuditLogEventTypes.File.DownloadLinkGenerated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(uploadedFile.ExternalId);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task downloading_file_should_produce_file_downloaded_audit_log_entry(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);

        var storage = await CreateHardDriveStorage(
            user,
            encryptionType);

        var workspace = await CreateWorkspace(storage, user);

        var folder = await CreateFolder(
            parent: null,
            workspace,
            user);

        var uploadedFile = await UploadFile(
            content: Encoding.UTF8.GetBytes("actual download audit test"),
            fileName: "audit-actual-download.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        await DownloadFile(
            fileExternalId: uploadedFile.ExternalId,
            workspace: workspace,
            user: user);

        //then
        await AssertAuditLogContains<Audit.File.Downloaded>(
            expectedEventType: AuditLogEventTypes.File.Downloaded,
            assertDetails: details =>
            {
                details.File.ExternalId.Should().Be(uploadedFile.ExternalId);
            },
            expectedActorEmail: user.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
