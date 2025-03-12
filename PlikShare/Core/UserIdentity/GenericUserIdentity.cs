namespace PlikShare.Core.UserIdentity;

public record GenericUserIdentity(
    string IdentityType,
    string Identity) : IUserIdentity;