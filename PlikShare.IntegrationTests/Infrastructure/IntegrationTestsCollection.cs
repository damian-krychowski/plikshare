namespace PlikShare.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestsCollection: ICollectionFixture<HostFixture8081>
{
    public const string Name = "IntegrationTests";
}
