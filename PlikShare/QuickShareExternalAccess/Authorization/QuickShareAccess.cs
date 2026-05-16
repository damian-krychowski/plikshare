using PlikShare.Core.UserIdentity;
using PlikShare.QuickShares.Cache;

namespace PlikShare.QuickShareExternalAccess.Authorization;

public record QuickShareAccess(
    QuickShareContext QuickShare,
    IUserIdentity UserIdentity,
    string? UserIp)
{
    public const string HttpContextName = "QuickShareAccess";
}
