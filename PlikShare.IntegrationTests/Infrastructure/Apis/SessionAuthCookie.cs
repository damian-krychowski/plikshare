using PlikShare.Core.Authorization;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class SessionAuthCookie(string value) : Cookie
{
    public override string Name => CookieName.SessionAuth;
    public override string Value { get; } = value;
}

public class BoxLinkAuthCookie(string value) : Cookie
{
    public override string Name => CookieName.BoxLinkAuth;
    public override string Value { get; } = value;
}

public class TwoFactorUserIdCookie(string value) : Cookie
{
    public override string Name => CookieName.TwoFactorUserId;
    public override string Value { get; } = value;
}

public class TwoFactorRememberMeCookie(string value) : Cookie
{
    public override string Name => CookieName.TwoFactorRememberMe;
    public override string Value { get; } = value;
}