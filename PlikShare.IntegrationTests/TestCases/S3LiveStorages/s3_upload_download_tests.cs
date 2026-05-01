using System.Text;
using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.S3;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.S3LiveStorages;

[Collection(IntegrationTestsCollection.Name)]
public class s3_upload_download_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public s3_upload_download_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        HostFixture.ResetUserEncryption().AsTask().Wait();
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [MemberData(nameof(S3TheoryData.AllProvidersAndEncryptionTypes),
        MemberType = typeof(S3TheoryData))]
    public async Task small_file_upload_and_download_should_return_same_content(
        S3StorageProvider provider,
        StorageEncryptionType encryptionType)
    {
        //given
        var storage = await CreateS3Storage(
            user: AppOwner,
            provider: provider,
            encryptionType: encryptionType);

        AppWorkspace? workspace = null;

        try
        {
            workspace = await CreateWorkspace(storage, AppOwner);

            var folder = await CreateFolder(
                parent: null,
                workspace: workspace,
                user: AppOwner);

            var originalContent = Encoding.UTF8.GetBytes("Hello, PlikShare S3 integration test!");

            //when
            var uploadedFile = await UploadFile(
                content: originalContent,
                fileName: "test-file.txt",
                contentType: "text/plain",
                folder: folder,
                workspace: workspace,
                user: AppOwner);

            var downloadedContent = await DownloadFile(
                fileExternalId: uploadedFile.ExternalId,
                workspace: workspace,
                user: AppOwner);

            //then
            downloadedContent.Should().BeEquivalentTo(originalContent);
        }
        finally
        {
            if (workspace is not null)
            {
                await CleanupS3WorkspaceAndStorage(workspace, storage, provider, AppOwner);
            }
            else
            {
                // Workspace creation failed before we got a handle — just drop the storage.
                await Api.Storages.DeleteStorage(
                    externalId: storage.ExternalId,
                    cookie: AppOwner.Cookie,
                    antiforgery: AppOwner.Antiforgery);
            }
        }
    }
}
