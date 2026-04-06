namespace PlikShare.Core.UserIdentity;

public record AnonymousIdentity : IUserIdentity
{
    public static readonly AnonymousIdentity Instance = new();

    private AnonymousIdentity(){}

    public string IdentityType => "anonymous";
    public string Identity => "anonymous";
}
