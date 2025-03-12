using PlikShare.Users.Id;

namespace PlikShare.Core.UserIdentity;

public record UserIdentity(UserExtId UserExternalId) : IUserIdentity
{
    public const string Type = "user_external_id";
    public string IdentityType => Type;
    public string Identity => UserExternalId.Value;
}