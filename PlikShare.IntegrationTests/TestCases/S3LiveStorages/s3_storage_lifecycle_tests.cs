using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.S3;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.S3LiveStorages;

[Collection(IntegrationTestsCollection.Name)]
public class s3_storage_lifecycle_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public s3_storage_lifecycle_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [InlineData(S3StorageProvider.AwsS3)]
    [InlineData(S3StorageProvider.CloudflareR2)]
    [InlineData(S3StorageProvider.BackblazeB2)]
    [InlineData(S3StorageProvider.DigitalOceanSpaces)]
    public async Task s3_storage_can_be_created_and_appears_on_the_list(
        S3StorageProvider provider)
    {
        //when - create endpoint runs S3Client.TestConnection (probe-bucket
        //create+delete) so a successful response proves credentials work
        //and the provider is reachable.
        var storage = await CreateS3Storage(
            user: AppOwner,
            provider: provider,
            encryptionType: StorageEncryptionType.None);

        try
        {
            //then
            var allStorages = await Api.Storages.Get(cookie: AppOwner.Cookie);

            allStorages.Items.Should().Contain(s =>
                s.ExternalId == storage.ExternalId
                && s.Name == storage.Name
                && s.EncryptionType == StorageEncryptionType.None);
        }
        finally
        {
            await Api.Storages.DeleteStorage(
                externalId: storage.ExternalId,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery);
        }
    }
}
