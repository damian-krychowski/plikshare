using FluentAssertions;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Auth;

[Collection(IntegrationTestsCollection.Name)]
public class sso_security_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public sso_security_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        hostFixture.RemoveAllAuthProviders();
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
    public async Task token_signed_with_untrusted_key_is_rejected()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(
            authCode, Random.Email("sso"), Random.Sub(),
            nonce!, clientId!, codeChallenge!,
            tokenOverrides: new MockOidcServer.TokenOverrides
            {
                UseUntrustedSigningKey = true
            });

        //when
        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-validation-failed");
    }

    [Fact]
    public async Task token_with_mismatched_nonce_is_rejected()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(
            authCode, Random.Email("sso"), Random.Sub(),
            nonce!, clientId!, codeChallenge!,
            tokenOverrides: new MockOidcServer.TokenOverrides
            {
                Nonce = "attacker-injected-nonce"
            });

        //when
        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-validation-failed");
    }

    [Fact]
    public async Task token_with_wrong_audience_is_rejected()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(
            authCode, Random.Email("sso"), Random.Sub(),
            nonce!, clientId!, codeChallenge!,
            tokenOverrides: new MockOidcServer.TokenOverrides
            {
                Audience = "wrong-client-id"
            });

        //when
        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-validation-failed");
    }

    [Fact]
    public async Task token_with_wrong_issuer_is_rejected()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(
            authCode, Random.Email("sso"), Random.Sub(),
            nonce!, clientId!, codeChallenge!,
            tokenOverrides: new MockOidcServer.TokenOverrides
            {
                Issuer = "https://evil-provider.com"
            });

        //when
        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-validation-failed");
    }

    [Fact]
    public async Task expired_token_is_rejected()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(
            authCode, Random.Email("sso"), Random.Sub(),
            nonce!, clientId!, codeChallenge!,
            tokenOverrides: new MockOidcServer.TokenOverrides
            {
                Expires = DateTime.UtcNow.AddHours(-1)
            });

        //when
        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-validation-failed");
    }

    [Fact]
    public async Task pkce_violation_wrong_code_challenge_is_rejected()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");

        // Register with a wrong code_challenge - mock will reject the code_verifier
        MockOidcServer.RegisterAuthCode(
            authCode, Random.Email("sso"), Random.Sub(),
            nonce!, clientId!, codeChallenge: "completely-wrong-code-challenge");

        //when
        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-exchange-failed");
    }
}
