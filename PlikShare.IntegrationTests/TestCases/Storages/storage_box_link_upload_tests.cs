using FluentAssertions;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Storage;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

/// <summary>
/// Anonymous-user upload via a box share link, exercised against every storage backend.
/// Counterpart to <c>storage_box_share_link_tests</c>: where that one validates the
/// download path, this one validates the upload path. Full encryption is excluded —
/// that path requires the workspace owner's encryption session to wrap the per-file
/// DEK, which an anonymous box-link visitor doesn't have.
///
/// File size is chosen to land on <c>MultiStepChunkUpload</c> in
/// <see cref="PlikShare.Storages.S3.S3StorageClient.ResolveUploadAlgorithm"/>, which
/// runs the full multipart cycle (initiate, per-part presigned URLs, complete-part
/// callbacks, finalising complete) against the real bucket — the most likely path
/// to break across providers.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class storage_box_link_upload_tests : TestFixture
{
    private readonly LiveStoragesFixture _liveFixture;
    private AppSignedInUser AppOwner { get; }

    public storage_box_link_upload_tests(
        HostFixture8081 hostFixture,
        LiveStoragesFixture liveFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        _liveFixture = liveFixture;
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(StorageTheoryData.AllStoragesWithoutFullEncryption),
        MemberType = typeof(StorageTheoryData))]
    public async Task anonymous_visitor_should_upload_multipart_file_via_box_link(
        StorageType provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var setup = await _liveFixture.GetOrCreate(this, provider, encryptionType, AppOwner);

        var boxFolder = await CreateFolder(
            parent: null,
            workspace: setup.Workspace,
            user: AppOwner);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);

        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowDownload: true,
                AllowUpload: true));

        var content = new byte[12 * 1024 * 1024];
        new System.Random(Seed: 1234).NextBytes(content);

        var fileUploadId = FileUploadExtId.NewId();

        //when
        var session = await StartBoxLinkSession();

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
                        FileNameWithExtension = "anon-multipart.bin",
                        FileContentType = "application/octet-stream",
                        FileSizeInBytes = content.Length
                    }
                ]
            },
            boxLinkToken: session.Token);

        initiate.MultiStepChunkUploads.Should().HaveCount(1,
            "a 12 MB file must resolve to MultiStepChunkUpload regardless of None/Managed encryption");

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
                .AsSpan(
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

        await WaitForFileUnlocked(completeResult.FileExternalId, AppOwner);

        var downloaded = await DownloadFile(
            fileExternalId: completeResult.FileExternalId,
            workspace: setup.Workspace,
            user: AppOwner);

        //then
        downloaded.Should().Equal(content);
    }
}
