using PlikShare.IntegrationTests.Infrastructure.Storage;

namespace PlikShare.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestsCollection :
    ICollectionFixture<HostFixture8081>,
    ICollectionFixture<LiveStoragesFixture>
{
    public const string Name = "IntegrationTests";
}
