using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Flurl.Http;
using Microsoft.AspNetCore.Http;
using PlikShare.AuditLog;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.BoxLinks.UpdateIsEnabled.Contracts;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.Utils;
using PlikShare.Files.BulkDownload.Contracts;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Zip;
using PlikShare.Uploads.FilePartUpload.Complete.Contracts;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_link_pre_signed_url_tests : TestFixture
{
    public box_link_pre_signed_url_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task box_link_anonymous_user_can_download_single_file_via_presigned_url()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var fileContent = Encoding.UTF8.GetBytes("anonymous-download payload");
        var file = await UploadFile(
            content: fileContent,
            fileName: "anon-download.txt",
            contentType: "text/plain",
            folder: boxFolder,
            workspace: workspace,
            user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: file.ExternalId,
            contentDisposition: "attachment",
            boxLinkToken: session.Token);

        var downloaded = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.DownloadPreSignedUrl,
            cookie: null);

        downloaded.Should().Equal(fileContent);
    }

    [Fact]
    public async Task box_link_anonymous_user_can_bulk_download_via_presigned_url()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var file1Content = Encoding.UTF8.GetBytes("file one");
        var file2Content = Encoding.UTF8.GetBytes("file two");

        var file1 = await UploadFile(
            content: file1Content, fileName: "one.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var file2 = await UploadFile(
            content: file2Content, fileName: "two.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetBulkDownloadLink(
            accessCode: boxLink.AccessCode,
            request: new GetBulkDownloadLinkRequestDto
            {
                SelectedFiles = [file1.ExternalId, file2.ExternalId],
                SelectedFolders = [],
                ExcludedFiles = [],
                ExcludedFolders = []
            },
            boxLinkToken: session.Token);

        var zipBytes = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.PreSignedUrl,
            cookie: null);

        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(2);
        entries["one.txt"].Should().Equal(file1Content);
        entries["two.txt"].Should().Equal(file2Content);
    }

    [Fact]
    public async Task box_link_anonymous_user_can_download_zip_entry_via_presigned_url()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var entryContent = "zip entry content via box-link"u8.ToArray();

        var zipBytes = CreateZipArchive(new Dictionary<string, byte[]>
        {
            ["nested/inside.txt"] = entryContent
        });

        var zipFile = await UploadFile(
            content: zipBytes,
            fileName: "archive.zip",
            contentType: "application/zip",
            folder: boxFolder,
            workspace: workspace,
            user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true));

        var session = await StartBoxLinkSession();

        var zipDetails = await Api.AccessCodesApi.GetZipFilePreviewDetails(
            accessCode: boxLink.AccessCode,
            fileExternalId: zipFile.ExternalId,
            boxLinkToken: session.Token);

        var entry = zipDetails.Items.Single(i => i.FilePath == "nested/inside.txt");

        var linkResponse = await Api.AccessCodesApi.GetZipContentDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: zipFile.ExternalId,
            request: new GetZipContentDownloadLinkRequestDto(
                Item: new ZipFileDto(
                    FilePath: entry.FilePath,
                    CompressedSizeInBytes: entry.CompressedSizeInBytes,
                    SizeInBytes: entry.SizeInBytes,
                    OffsetToLocalFileHeader: entry.OffsetToLocalFileHeader,
                    FileNameLength: entry.FileNameLength,
                    CompressionMethod: entry.CompressionMethod,
                    IndexInArchive: entry.IndexInArchive),
                ContentDisposition: ContentDispositionType.Attachment),
            boxLinkToken: session.Token);

        var downloaded = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.DownloadPreSignedUrl,
            cookie: null);

        downloaded.Should().Equal(entryContent);
    }

    [Fact]
    public async Task box_link_anonymous_user_can_directly_upload_files_via_presigned_url()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true,
                AllowUpload: true));

        await WaitForBucketReady(workspace, owner);

        var session = await StartBoxLinkSession();

        var fileAContent = Encoding.UTF8.GetBytes("anonymous direct upload A");
        var fileBContent = Encoding.UTF8.GetBytes("anonymous direct upload B");

        var fileAUploadId = FileUploadExtId.NewId();
        var fileBUploadId = FileUploadExtId.NewId();

        var initiate = await Api.AccessCodesApi.BulkInitiateFileUpload(
            accessCode: boxLink.AccessCode,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileAUploadId.Value,
                        FolderExternalId = null, // null → upload to box's main folder
                        FileNameWithExtension = "anon-A.txt",
                        FileContentType = "text/plain",
                        FileSizeInBytes = fileAContent.Length
                    },
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileBUploadId.Value,
                        FolderExternalId = null,
                        FileNameWithExtension = "anon-B.txt",
                        FileContentType = "text/plain",
                        FileSizeInBytes = fileBContent.Length
                    }
                ]
            },
            boxLinkToken: session.Token);

        initiate.DirectUploads.Should().NotBeNull(
            "small files on HardDrive storage must go through the multi-file direct upload path");

        var uploadResults = await Api.PreSignedFiles.MultiFileDirectUpload(
            preSignedUrl: initiate.DirectUploads!.PreSignedMultiFileDirectUploadLink,
            files: new Dictionary<FileUploadExtId, byte[]>
            {
                [fileAUploadId] = fileAContent,
                [fileBUploadId] = fileBContent
            },
            cookie: null);

        uploadResults.Should().HaveCount(2);

        // Verify content via owner: pulled from workspace, must roundtrip.
        var fileAExternalId = uploadResults.Single(r =>
            r.UploadExternalId == fileAUploadId).FileExternalId;

        var fileBExternalId = uploadResults.Single(r =>
            r.UploadExternalId == fileBUploadId).FileExternalId;

        var downloadedA = await DownloadFile(
            fileExternalId: fileAExternalId,
            workspace: workspace,
            user: owner);

        var downloadedB = await DownloadFile(
            fileExternalId: fileBExternalId,
            workspace: workspace,
            user: owner);

        downloadedA.Should().Equal(fileAContent);
        downloadedB.Should().Equal(fileBContent);
    }

    [Fact]
    public async Task box_link_anonymous_user_can_upload_part_via_presigned_url()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true,
                AllowUpload: true));

        await WaitForBucketReady(workspace, owner);

        var session = await StartBoxLinkSession();

        var content = new byte[12 * 1024 * 1024]; // 12 MB → 2 parts on HardDrive
        new Random(4242).NextBytes(content);

        var fileUploadExternalId = FileUploadExtId.NewId();

        var initiate = await Api.AccessCodesApi.BulkInitiateFileUpload(
            accessCode: boxLink.AccessCode,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileUploadExternalId.Value,
                        FolderExternalId = null,
                        FileNameWithExtension = "anon-multistep.bin",
                        FileContentType = "application/octet-stream",
                        FileSizeInBytes = content.Length
                    }
                ]
            },
            boxLinkToken: session.Token);

        initiate.MultiStepChunkUploads.Should().HaveCount(1,
            "a 12 MB file on HardDrive storage must go through MultiStepChunkUpload");

        var multiStep = initiate.MultiStepChunkUploads[0];
        var uploadExtId = FileUploadExtId.Parse(multiStep.FileUploadExternalId);

        for (var partNumber = 1; partNumber <= multiStep.ExpectedPartsCount; partNumber++)
        {
            var partInitiate = await Api.AccessCodesApi.InitiateFilePartUpload(
                accessCode: boxLink.AccessCode,
                fileUploadExternalId: uploadExtId,
                partNumber: partNumber,
                boxLinkToken: session.Token);

            var partContent = content
                .AsSpan()
                .Slice(
                    (int)partInitiate.StartsAtByte,
                    (int)(partInitiate.EndsAtByte - partInitiate.StartsAtByte + 1))
                .ToArray();

            var eTag = await Api.PreSignedFiles.UploadFilePart(
                preSignedUrl: partInitiate.UploadPreSignedUrl,
                content: partContent,
                contentType: "application/octet-stream",
                cookie: null);

            if (partInitiate.IsCompleteFilePartUploadCallbackRequired)
            {
                await Api.AccessCodesApi.CompleteFilePartUpload(
                    accessCode: boxLink.AccessCode,
                    fileUploadExternalId: uploadExtId,
                    partNumber: partNumber,
                    request: new CompleteBoxFilePartUploadRequestDto(ETag: eTag),
                    boxLinkToken: session.Token);
            }
        }

        var completeResult = await Api.AccessCodesApi.CompleteUpload(
            accessCode: boxLink.AccessCode,
            fileUploadExternalId: uploadExtId,
            boxLinkToken: session.Token);

        var downloaded = await DownloadFile(
            fileExternalId: completeResult.FileExternalId,
            workspace: workspace,
            user: owner);

        downloaded.Should().Equal(content);
    }

    [Fact]
    public async Task expired_single_file_download_link_is_rejected()
    {
        var initialTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(initialTime);

        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("expiry-check"),
            fileName: "expiry.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: file.ExternalId,
            contentDisposition: "attachment",
            boxLinkToken: session.Token);

        Clock.CurrentTime(initialTime.AddDays(1).AddSeconds(1));

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.DownloadFile(
                preSignedUrl: linkResponse.DownloadPreSignedUrl,
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError?.Code.Should().Be("invalid-protected-payload");
    }

    [Fact]
    public async Task expired_bulk_download_link_is_rejected()
    {
        // Bulk-download URL TTL = 1 minute.
        var initialTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(initialTime);

        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("bulk expiry"),
            fileName: "bulk-expiry.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetBulkDownloadLink(
            accessCode: boxLink.AccessCode,
            request: new GetBulkDownloadLinkRequestDto
            {
                SelectedFiles = [file.ExternalId],
                SelectedFolders = [],
                ExcludedFiles = [],
                ExcludedFolders = []
            },
            boxLinkToken: session.Token);

        Clock.CurrentTime(initialTime.AddMinutes(1).AddSeconds(1));

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.DownloadFile(
                preSignedUrl: linkResponse.PreSignedUrl,
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError?.Code.Should().Be("invalid-bulk-download-payload");
    }

    [Fact]
    public async Task expired_zip_content_download_link_is_rejected()
    {
        // Zip-content URL TTL = 10 minutes.
        var initialTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(initialTime);

        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var entryContent = "zip expiry"u8.ToArray();
        var zipBytes = CreateZipArchive(new Dictionary<string, byte[]>
        {
            ["entry.txt"] = entryContent
        });

        var zipFile = await UploadFile(
            content: zipBytes, fileName: "expiry.zip", contentType: "application/zip",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var zipDetails = await Api.AccessCodesApi.GetZipFilePreviewDetails(
            accessCode: boxLink.AccessCode,
            fileExternalId: zipFile.ExternalId,
            boxLinkToken: session.Token);

        var entry = zipDetails.Items.Single();

        var linkResponse = await Api.AccessCodesApi.GetZipContentDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: zipFile.ExternalId,
            request: new GetZipContentDownloadLinkRequestDto(
                Item: new ZipFileDto(
                    FilePath: entry.FilePath,
                    CompressedSizeInBytes: entry.CompressedSizeInBytes,
                    SizeInBytes: entry.SizeInBytes,
                    OffsetToLocalFileHeader: entry.OffsetToLocalFileHeader,
                    FileNameLength: entry.FileNameLength,
                    CompressionMethod: entry.CompressionMethod,
                    IndexInArchive: entry.IndexInArchive),
                ContentDisposition: ContentDispositionType.Attachment),
            boxLinkToken: session.Token);

        Clock.CurrentTime(initialTime.AddMinutes(10).AddSeconds(1));

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.DownloadFile(
                preSignedUrl: linkResponse.DownloadPreSignedUrl,
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError?.Code.Should().Be("invalid-protected-payload");
    }

    [Fact]
    public async Task expired_multi_file_direct_upload_link_is_rejected()
    {
        // Multi-file direct upload URL TTL = 15 minutes.
        var initialTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(initialTime);

        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowUpload: true));

        await WaitForBucketReady(workspace, owner);

        var session = await StartBoxLinkSession();

        var fileContent = Encoding.UTF8.GetBytes("expired direct upload");
        var fileUploadId = FileUploadExtId.NewId();

        var initiate = await Api.AccessCodesApi.BulkInitiateFileUpload(
            accessCode: boxLink.AccessCode,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileUploadId.Value,
                        FolderExternalId = null,
                        FileNameWithExtension = "expired-direct.txt",
                        FileContentType = "text/plain",
                        FileSizeInBytes = fileContent.Length
                    }
                ]
            },
            boxLinkToken: session.Token);

        initiate.DirectUploads.Should().NotBeNull();

        Clock.CurrentTime(initialTime.AddMinutes(15).AddSeconds(1));

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.MultiFileDirectUpload(
                preSignedUrl: initiate.DirectUploads!.PreSignedMultiFileDirectUploadLink,
                files: new Dictionary<FileUploadExtId, byte[]>
                {
                    [fileUploadId] = fileContent
                },
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError?.Code.Should().Be("invalid-protected-payload");
    }

    [Fact]
    public async Task expired_file_part_upload_link_is_rejected()
    {
        // Per-part upload URL TTL on HardDrive = 1 minute.
        var initialTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(initialTime);

        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowUpload: true));

        await WaitForBucketReady(workspace, owner);

        var session = await StartBoxLinkSession();

        var content = new byte[12 * 1024 * 1024]; // 12 MB → multi-step
        new Random(7).NextBytes(content);

        var fileUploadId = FileUploadExtId.NewId();

        var initiate = await Api.AccessCodesApi.BulkInitiateFileUpload(
            accessCode: boxLink.AccessCode,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileUploadId.Value,
                        FolderExternalId = null,
                        FileNameWithExtension = "expired-part.bin",
                        FileContentType = "application/octet-stream",
                        FileSizeInBytes = content.Length
                    }
                ]
            },
            boxLinkToken: session.Token);

        var multiStep = initiate.MultiStepChunkUploads.Single();
        var uploadExtId = FileUploadExtId.Parse(multiStep.FileUploadExternalId);

        var partInitiate = await Api.AccessCodesApi.InitiateFilePartUpload(
            accessCode: boxLink.AccessCode,
            fileUploadExternalId: uploadExtId,
            partNumber: 1,
            boxLinkToken: session.Token);

        Clock.CurrentTime(initialTime.AddMinutes(1).AddSeconds(1));

        var partContent = content
            .AsSpan(
                (int)partInitiate.StartsAtByte,
                (int)(partInitiate.EndsAtByte - partInitiate.StartsAtByte + 1))
            .ToArray();

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.UploadFilePart(
                preSignedUrl: partInitiate.UploadPreSignedUrl,
                content: partContent,
                contentType: "application/octet-stream",
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError?.Code.Should().Be("invalid-protected-payload");
    }

    [Theory]
    [InlineData("GET", "api/files/this-is-not-a-real-payload", "invalid-protected-payload")]
    [InlineData("GET", "api/bulk-download/this-is-not-a-real-payload", "invalid-bulk-download-payload")]
    [InlineData("GET", "api/zip-files/this-is-not-a-real-payload", "invalid-protected-payload")]
    [InlineData("POST", "api/files/multi-file/this-is-not-a-real-payload", "invalid-protected-payload")]
    [InlineData("PUT", "api/files/this-is-not-a-real-payload", "invalid-protected-payload")]
    public async Task tampered_protected_payload_is_rejected(
        string method,
        string apiPath,
        string expectedErrorCode)
    {
        // Garbage payload reaches every validator regardless of HTTP method;
        // each must reject with 400 + a stable error code that the client
        // can rely on. PUT/POST need a body so Content-Length passes the
        // pre-validator header checks before the payload extraction runs.

        var request = HostFixture.FlurlClient
            .Request(AppUrl, apiPath)
            .AllowAnyHttpStatus();

        var body = new ByteArrayContent("garbage"u8.ToArray());
        body.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var response = method switch
        {
            "GET" => await request.GetAsync(),
            "PUT" => await request.SendAsync(HttpMethod.Put, body),
            "POST" => await request.PostAsync(body),
            _ => throw new ArgumentException($"Unsupported method '{method}'")
        };

        response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var errorBody = await response.GetStringAsync();
        errorBody.Should().Contain(expectedErrorCode);
    }

    [Fact]
    public async Task audit_log_entry_is_produced_for_anonymous_box_link_download()
    {
        // The actor for an anonymous bearer-URL download is no longer the
        // user/box-link session — it's whatever the (unauthenticated) request
        // looks like to GetAuditLogActorContext, which means AnonymousIdentity
        // and a null email. The PreSignedBy in the payload still records who
        // GENERATED the link if you ever want to reconstruct that, but it is
        // not currently surfaced into the audit row. This test pins the
        // observable behaviour so anyone changing the audit pipeline notices.
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var fileContent = Encoding.UTF8.GetBytes("audit-anonymous-download");
        var file = await UploadFile(
            content: fileContent, fileName: "audit-anon.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: file.ExternalId,
            contentDisposition: "attachment",
            boxLinkToken: session.Token);

        ClearAuditLog();

        var downloaded = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.DownloadPreSignedUrl,
            cookie: null);

        downloaded.Should().Equal(fileContent);

        await AssertAuditLogContains<Audit.File.Downloaded>(
            expectedEventType: AuditLogEventTypes.File.Downloaded,
            assertDetails: details =>
            {
                details.File.ExternalId.Should().Be(file.ExternalId);
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
            },
            // No actor email — the consuming request had no auth.
            expectedActorEmail: null);
    }

    [Fact]
    public async Task box_link_anonymous_user_can_perform_range_download_via_presigned_url()
    {
        // Range request hits the same single-file download endpoint and the
        // same validator, but a different code branch
        // (HandleRangeFileDownload). Without this test, range/inline preview
        // for box-link viewers would silently regress.
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var content = new byte[8 * 1024];
        new Random(99).NextBytes(content);

        var file = await UploadFile(
            content: content,
            fileName: "ranged.bin",
            contentType: "application/octet-stream",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: file.ExternalId,
            contentDisposition: "inline",
            boxLinkToken: session.Token);

        const int rangeStart = 100;
        const int rangeEnd = 1099;

        var rangeResult = await Api.PreSignedFiles.DownloadFileRange(
            preSignedUrl: linkResponse.DownloadPreSignedUrl,
            rangeStart: rangeStart,
            rangeEnd: rangeEnd,
            cookie: null);

        rangeResult.StatusCode.Should().Be(StatusCodes.Status206PartialContent);
        rangeResult.ContentRange.Should().Be($"bytes {rangeStart}-{rangeEnd}/{content.Length}");
        rangeResult.Content.Should().Equal(content[rangeStart..(rangeEnd + 1)]);
    }

    [Fact]
    public async Task disabling_box_link_does_not_invalidate_already_issued_presigned_urls()
    {
        // Documents the deliberate bearer-URL behaviour: once issued, a
        // pre-signed URL stays valid for its TTL window even if the
        // box-link is later disabled. Disabling only stops the box-link
        // API from issuing NEW URLs (via the BoxLinkCookie auth check),
        // it does NOT revoke outstanding ones. If you want immediate
        // revocation, you need a separate mechanism (key rotation,
        // explicit per-link revocation list, or shorter TTL).
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var fileContent = Encoding.UTF8.GetBytes("download-after-disable");
        var file = await UploadFile(
            content: fileContent, fileName: "after-disable.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: file.ExternalId,
            contentDisposition: "attachment",
            boxLinkToken: session.Token);

        // Owner disables the box-link AFTER the URL has been issued.
        await Api.BoxLinks.UpdateIsEnabled(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkIsEnabledRequestDto(IsEnabled: false),
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        // The previously-issued URL still works — that's by design.
        var downloaded = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.DownloadPreSignedUrl,
            cookie: null);

        downloaded.Should().Equal(fileContent);

        // Sanity check: the box-link API itself stops issuing new URLs.
        var newSession = await StartBoxLinkSession();

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.AccessCodesApi.GetFileDownloadLink(
                accessCode: boxLink.AccessCode,
                fileExternalId: file.ExternalId,
                contentDisposition: "attachment",
                boxLinkToken: newSession.Token));

        // Disabled box-link is no longer reachable through the box-link API.
        apiError.StatusCode.Should().BeOneOf(
            StatusCodes.Status404NotFound,
            StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task single_file_download_link_returns_404_when_file_was_deleted_after_url_was_issued()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("delete-after-issuance"),
            fileName: "doomed.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetFileDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: file.ExternalId,
            contentDisposition: "attachment",
            boxLinkToken: session.Token);

        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [file.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.DownloadFile(
                preSignedUrl: linkResponse.DownloadPreSignedUrl,
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        apiError.HttpError?.Code.Should().Be("file-doesnt-exist");
    }

    [Fact]
    public async Task bulk_download_link_silently_skips_files_deleted_after_url_was_issued()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var keepContent = Encoding.UTF8.GetBytes("kept");
        var doomedContent = Encoding.UTF8.GetBytes("doomed");

        var keepFile = await UploadFile(
            content: keepContent, fileName: "keep.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var doomedFile = await UploadFile(
            content: doomedContent, fileName: "doomed.txt", contentType: "text/plain",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var linkResponse = await Api.AccessCodesApi.GetBulkDownloadLink(
            accessCode: boxLink.AccessCode,
            request: new GetBulkDownloadLinkRequestDto
            {
                SelectedFiles = [keepFile.ExternalId, doomedFile.ExternalId],
                SelectedFolders = [],
                ExcludedFiles = [],
                ExcludedFolders = []
            },
            boxLinkToken: session.Token);

        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [doomedFile.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var zipBytes = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.PreSignedUrl,
            cookie: null);

        var entries = ExtractZipEntries(zipBytes);
        entries.Should().HaveCount(1);
        entries["keep.txt"].Should().Equal(keepContent);
        entries.Should().NotContainKey("doomed.txt");
    }

    [Fact]
    public async Task zip_content_download_link_returns_404_when_zip_file_was_deleted_after_url_was_issued()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var boxFolder = await CreateFolder(workspace: workspace, user: owner);

        var entryContent = "inside"u8.ToArray();
        var zipBytes = CreateZipArchive(new Dictionary<string, byte[]>
        {
            ["entry.txt"] = entryContent
        });

        var zipFile = await UploadFile(
            content: zipBytes, fileName: "doomed.zip", contentType: "application/zip",
            folder: boxFolder, workspace: workspace, user: owner);

        var box = await CreateBox(folder: boxFolder, user: owner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: owner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true, AllowDownload: true));

        var session = await StartBoxLinkSession();

        var zipDetails = await Api.AccessCodesApi.GetZipFilePreviewDetails(
            accessCode: boxLink.AccessCode,
            fileExternalId: zipFile.ExternalId,
            boxLinkToken: session.Token);

        var entry = zipDetails.Items.Single();

        var linkResponse = await Api.AccessCodesApi.GetZipContentDownloadLink(
            accessCode: boxLink.AccessCode,
            fileExternalId: zipFile.ExternalId,
            request: new GetZipContentDownloadLinkRequestDto(
                Item: new ZipFileDto(
                    FilePath: entry.FilePath,
                    CompressedSizeInBytes: entry.CompressedSizeInBytes,
                    SizeInBytes: entry.SizeInBytes,
                    OffsetToLocalFileHeader: entry.OffsetToLocalFileHeader,
                    FileNameLength: entry.FileNameLength,
                    CompressionMethod: entry.CompressionMethod,
                    IndexInArchive: entry.IndexInArchive),
                ContentDisposition: ContentDispositionType.Attachment),
            boxLinkToken: session.Token);

        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [zipFile.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            () => Api.PreSignedFiles.DownloadFile(
                preSignedUrl: linkResponse.DownloadPreSignedUrl,
                cookie: null));

        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        apiError.HttpError?.Code.Should().Be("file-doesnt-exist");
    }

    private static byte[] CreateZipArchive(Dictionary<string, byte[]> files)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, contents) in files)
            {
                var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(contents);
            }
        }

        return memoryStream.ToArray();
    }

    private static Dictionary<string, byte[]> ExtractZipEntries(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                continue;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            entries[entry.FullName] = ms.ToArray();
        }

        return entries;
    }
}
