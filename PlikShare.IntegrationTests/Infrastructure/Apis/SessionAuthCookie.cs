using PlikShare.Core.Authorization;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class SessionAuthCookie(string value) : Cookie
{
    public override string Name => CookieName.SessionAuth;
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

public class GenericCookie(string name, string value): Cookie
{
    public override string Name { get; } = name;
    public override string Value { get; } = value;
}