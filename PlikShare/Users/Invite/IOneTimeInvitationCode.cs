using PlikShare.Core.Utils;

namespace PlikShare.Users.Invite;

public interface IOneTimeInvitationCode
{
    string Generate();
}

public class OneTimeInvitationCode : IOneTimeInvitationCode
{
    public string Generate() => Guid.NewGuid().ToBase62();
}