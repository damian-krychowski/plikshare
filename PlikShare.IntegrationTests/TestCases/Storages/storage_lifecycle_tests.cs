using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Storages;

[Collection(IntegrationTestsCollection.Name)]
public class storage_lifecycle_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public storage_lifecycle_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Theory]
    [InlineData(StorageType.AwsS3)]
    [InlineData(StorageType.CloudflareR2)]
    [InlineData(StorageType.BackblazeB2)]
    [InlineData(StorageType.DigitalOceanSpaces)]
    [InlineData(StorageType.AzureBlob)]
    [InlineData(StorageType.GoogleCloudStorage)]
    [InlineData(StorageType.HardDrive)]
    public async Task storage_can_be_created_and_appears_on_the_list(
        StorageType provider)
    {
        //when - create endpoint runs S3Client.TestConnection (probe-bucket
        //create+delete) so a successful response proves credentials work
        //and the provider is reachable.
        var storage = await CreateStorage(
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
