using FluentAssertions;
using PlikShare.Auth.Contracts;
using PlikShare.AuthProviders.PasswordLogin.Contracts;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.AuthProviders;

[Collection(IntegrationTestsCollection.Name)]
public class password_login_tests : TestFixture, IDisposable
{
    private readonly HostFixture8081 _hostFixture;
    private AppSignedInUser AppOwner { get; }

    public password_login_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        _hostFixture = hostFixture;

        MockOidcServer.Reset();

        AppOwner = SignIn(user: Users.AppOwner).Result;

        Api.GeneralSettings
            .SetApplicationSignUp(
                value: AppSettings.SignUpSetting.Everyone,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery)
            .Wait();
    }

    [Fact]
    public async Task cannot_disable_password_login_when_user_has_no_sso_linked()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        //when
        var act = () => Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>()
            .Where(e => e.StatusCode == 400);
    }

    [Fact]
    public async Task can_disable_password_login_when_user_has_sso_linked()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        await SignInViaSso(
            authProvider: authProvider,
            email: Users.AppOwner.Email,
            sub: Random.Sub());

        //when
        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var settings = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        settings.IsPasswordLoginEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task can_re_enable_password_login()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        await SignInViaSso(
            authProvider: authProvider,
            email: Users.AppOwner.Email,
            sub: Random.Sub());

        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = true },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var settings = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        settings.IsPasswordLoginEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task sign_in_with_password_returns_password_login_disabled_when_disabled()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        await SignInViaSso(
            authProvider: authProvider,
            email: Users.AppOwner.Email,
            sub: Random.Sub());

        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var anonymousAntiforgery = await Api.Antiforgery.GetToken();

        var (result, sessionAuthCookie, _) = await Api.Auth.SignIn(
            email: Users.AppOwner.Email,
            password: Users.AppOwner.Password,
            antiforgeryCookies: anonymousAntiforgery);

        //then
        result.Should().BeEquivalentTo(
            SignInUserResponseDto.PasswordLoginDisabled);

        sessionAuthCookie.Should().BeNull();
    }

    [Fact]
    public async Task sso_login_still_works_when_password_login_is_disabled()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);
        var ssoSub = Random.Sub();

        await SignInViaSso(
            authProvider: authProvider,
            email: Users.AppOwner.Email,
            sub: ssoSub);

        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var ssoEmail = Random.Email("sso");
        var newSub = Random.Sub();
        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);

        initiateResult.StatusCode.Should().Be(302);

        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(authCode, ssoEmail, newSub, nonce!, clientId!, codeChallenge!);

        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().NotContain("error=");
        callbackResult.LocationHeader.Should().BeEquivalentUrl(AppUrl);
    }

    [Fact]
    public async Task sign_up_returns_password_login_disabled_when_disabled()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        await SignInViaSso(
            authProvider: authProvider,
            email: Users.AppOwner.Email,
            sub: Random.Sub());

        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var anonymousAntiforgery = await Api.Antiforgery.GetToken();

        var (result, _) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = Random.Email("newuser"),
                Password = "Test1234!@#$",
                InvitationCode = null,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgery);

        //then
        result.Should().BeEquivalentTo(
            SignUpUserResponseDto.PasswordLoginDisabled);
    }

    [Fact]
    public async Task get_auth_settings_returns_password_login_status()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        //when
        var settings = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        //then
        settings.IsPasswordLoginEnabled.Should().BeTrue();
        settings.CurrentUserHasSsoLinked.Should().BeFalse();
    }

    [Fact]
    public async Task entry_page_returns_password_login_disabled_when_disabled()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        await SignInViaSso(
            authProvider: authProvider,
            email: Users.AppOwner.Email,
            sub: Random.Sub());

        await Api.AuthProviders.SetPasswordLogin(
            request: new SetPasswordLoginRequestDto { IsEnabled = false },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var entryPage = await Api.EntryPage.GetSettings(cookie: null);

        //then
        entryPage.IsPasswordLoginEnabled.Should().BeFalse();
    }

    public void Dispose()
    {
        _hostFixture.RemoveAllAuthProviders();
        _hostFixture.RemoveAllUserLogins();
        _hostFixture.ResetPasswordLoginSetting();
    }
}
