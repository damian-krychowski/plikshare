namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public abstract class Cookie
{
    public abstract string Name { get; }
    public abstract string Value { get; }
};

public abstract class Header
{
    public abstract string Name { get; }
    public abstract string Value { get; }
}