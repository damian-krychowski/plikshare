namespace PlikShare.Core.UserIdentity;

public record BoxLinkSessionUserIdentity(Guid BoxLinkSessionId) : IUserIdentity
{
    public const string Type = "box_link_session_id";

    public string IdentityType => Type;
    public string Identity { get; } = BoxLinkSessionId.ToString();
}