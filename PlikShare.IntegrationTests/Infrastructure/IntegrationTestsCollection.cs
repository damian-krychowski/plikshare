using PlikShare.IntegrationTests.Infrastructure.S3;

namespace PlikShare.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestsCollection :
    ICollectionFixture<HostFixture8081>,
    ICollectionFixture<S3LiveStoragesFixture>
{
    public const string Name = "IntegrationTests";
}
