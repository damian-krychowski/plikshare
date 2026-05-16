namespace PlikShare.Core.UserIdentity;

public record QuickShareSessionUserIdentity(Guid QuickShareSessionId) : IUserIdentity
{
    public const string Type = "quick_share_session_id";

    public string IdentityType => Type;
    public string Identity { get; } = QuickShareSessionId.ToString();
}
