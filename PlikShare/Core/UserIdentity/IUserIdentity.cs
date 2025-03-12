namespace PlikShare.Core.UserIdentity;

public interface IUserIdentity
{
    string IdentityType { get; }
    string Identity { get; }

    public bool IsEqual(IUserIdentity other)
    {
        return IdentityType == other.IdentityType 
               && Identity == other.Identity;
    }

    public bool IsEqual(string identity, string identityType)
    {
        return Identity == identity && IdentityType == identityType;
    }
}

public static class UserIdentityExtensions
{
    public static bool ContainsIdentity(this IEnumerable<IUserIdentity> identities, IUserIdentity searchIdentity)
    {
        return identities.Any(x =>
            x.Identity == searchIdentity.Identity &&
            x.IdentityType == searchIdentity.IdentityType);
    }
}