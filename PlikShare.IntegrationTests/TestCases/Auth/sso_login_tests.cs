using FluentAssertions;
using PlikShare.AuthProviders.Create.Contracts;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Users.Id;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.UpdateMaxWorkspaceNumber.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Auth;

[Collection(IntegrationTestsCollection.Name)]
public class sso_login_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public sso_login_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
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
    public async Task sso_login_creates_new_user_when_user_does_not_exist()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        var ssoEmail = Random.Email("sso");
        var ssoSub = Random.Sub();
        var authCode = Random.AuthCode();

        //when
        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);

        initiateResult.StatusCode.Should().Be(302);
        initiateResult.LocationHeader.Should().NotBeNullOrEmpty();

        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");

        state.Should().NotBeNullOrEmpty();

        MockOidcServer.RegisterAuthCode(authCode, ssoEmail, ssoSub, nonce!, clientId!);

        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().NotContain("error=");
        callbackResult.LocationHeader.Should().BeEquivalentUrl(AppUrl);

        var users = await Api.Users.Get(cookie: user.Cookie);
        var ssoUser = users.Items.FirstOrDefault(u => u.Email == ssoEmail);
        ssoUser.Should().NotBeNull();
        ssoUser!.IsEmailConfirmed.Should().BeTrue();

        var ssoUserDetails = await Api.Users.GetDetails(
            userExternalId: ssoUser.ExternalId,
            cookie: user.Cookie);

        ssoUserDetails.User.Email.Should().Be(ssoEmail);
        ssoUserDetails.User.HasPassword.Should().BeFalse();
        ssoUserDetails.User.SsoProviders.Should().ContainSingle()
            .Which.Should().Be(authProvider.Name);
    }

    [Fact]
    public async Task sso_login_signs_in_existing_user()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        var ssoEmail = Random.Email("sso");
        var ssoSub = Random.Sub();

        // First login - creates user
        var firstCode = Random.AuthCode();
        var firstInitiate = await Api.Sso.Initiate(authProvider.ExternalId);
        var firstState = UrlHelper.ExtractQueryParam(firstInitiate.LocationHeader!, "state");
        var firstNonce = UrlHelper.ExtractQueryParam(firstInitiate.LocationHeader!, "nonce");
        var firstClientId = UrlHelper.ExtractQueryParam(firstInitiate.LocationHeader!, "client_id");
        MockOidcServer.RegisterAuthCode(firstCode, ssoEmail, ssoSub, firstNonce!, firstClientId!);

        await Api.Sso.Callback(code: firstCode, state: firstState!);

        // Second login - same user
        var secondCode = Random.AuthCode();

        //when
        var secondInitiate = await Api.Sso.Initiate(authProvider.ExternalId);
        var secondState = UrlHelper.ExtractQueryParam(secondInitiate.LocationHeader!, "state");
        var secondNonce = UrlHelper.ExtractQueryParam(secondInitiate.LocationHeader!, "nonce");
        var secondClientId = UrlHelper.ExtractQueryParam(secondInitiate.LocationHeader!, "client_id");
        MockOidcServer.RegisterAuthCode(secondCode, ssoEmail, ssoSub, secondNonce!, secondClientId!);

        var callbackResult = await Api.Sso.Callback(
            code: secondCode,
            state: secondState!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().NotContain("error=");
        callbackResult.LocationHeader.Should().BeEquivalentUrl(AppUrl);
    }

    [Fact]
    public async Task sso_login_with_inactive_provider_redirects_with_error()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        var provider = await Api.AuthProviders.CreateOidc(
            request: new CreateOidcAuthProviderRequestDto
            {
                Name = Random.Name("OidcProvider"),
                ClientId = Random.ClientId(),
                ClientSecret = Random.ClientSecret(),
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        // Provider is NOT activated

        //when
        var initiateResult = await Api.Sso.Initiate(provider.ExternalId);

        //then
        initiateResult.StatusCode.Should().Be(302);
        initiateResult.LocationHeader.Should().Contain("error=provider-not-found");
    }

    [Fact]
    public async Task sso_login_with_nonexistent_provider_redirects_with_error()
    {
        //when
        var initiateResult = await Api.Sso.Initiate("ap_nonexistent12345");

        //then
        initiateResult.StatusCode.Should().Be(302);
        initiateResult.LocationHeader.Should().Contain("error=provider-not-found");
    }

    [Fact]
    public async Task sso_user_can_create_and_access_workspace()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var ssoEmail = Random.Email("sso");
        var ssoSub = Random.Sub();

        var ssoUser = await SignInViaSso(authProvider, ssoEmail, ssoSub);

        await GrantWorkspacePermission(ssoUser.ExternalId, admin);

        var storage = await CreateHardDriveStorage(admin);

        // re-login SSO user to pick up new permissions
        ssoUser = await SignInViaSso(authProvider, ssoEmail, ssoSub);

        //when
        var workspace = await CreateWorkspace(storage, ssoUser);

        //then
        var dashboard = await Api.Dashboard.Get(cookie: ssoUser.Cookie);
        dashboard.Workspaces.Should().ContainSingle()
            .Which.ExternalId.Should().Be(workspace.ExternalId.Value);
    }

    [Fact]
    public async Task password_user_retains_resources_after_sso_login_with_same_email()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        var passwordUser = await InviteAndRegisterUser(admin);

        await GrantWorkspacePermission(passwordUser.ExternalId, admin);

        var storage = await CreateHardDriveStorage(admin);

        // re-login to pick up new permissions
        passwordUser = await SignIn(new User(passwordUser.Email, passwordUser.Password));

        var workspace = await CreateWorkspace(storage, passwordUser);

        //when - SSO login with the same email
        var ssoSub = Random.Sub();
        var ssoUser = await SignInViaSso(authProvider, passwordUser.Email, ssoSub);

        //then
        var dashboard = await Api.Dashboard.Get(cookie: ssoUser.Cookie);
        dashboard.Workspaces.Should().ContainSingle()
            .Which.ExternalId.Should().Be(workspace.ExternalId.Value);

        var userDetails = await Api.Users.GetDetails(
            userExternalId: ssoUser.ExternalId,
            cookie: admin.Cookie);

        userDetails.User.HasPassword.Should().BeTrue();
        userDetails.User.SsoProviders.Should().ContainSingle()
            .Which.Should().Be(authProvider.Name);
    }

    [Fact]
    public async Task user_linked_to_multiple_providers_can_access_resources_via_each()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var provider1 = await CreateAndActivateAuthProvider(admin);
        var provider2 = await CreateAndActivateAuthProvider(admin);

        var ssoEmail = Random.Email("sso");
        var sub1 = Random.Sub();
        var sub2 = Random.Sub();

        // First login via provider1 - creates user
        var user1 = await SignInViaSso(provider1, ssoEmail, sub1);

        await GrantWorkspacePermission(user1.ExternalId, admin);

        var storage = await CreateHardDriveStorage(admin);

        // re-login to pick up new permissions, then create workspace
        user1 = await SignInViaSso(provider1, ssoEmail, sub1);
        var workspace = await CreateWorkspace(storage, user1);

        //when - login via provider2
        var user2 = await SignInViaSso(provider2, ssoEmail, sub2);

        //then - same user, same workspace visible
        user2.ExternalId.Should().Be(user1.ExternalId);

        var dashboard = await Api.Dashboard.Get(cookie: user2.Cookie);
        dashboard.Workspaces.Should().ContainSingle()
            .Which.ExternalId.Should().Be(workspace.ExternalId.Value);

        var userDetails = await Api.Users.GetDetails(
            userExternalId: user1.ExternalId,
            cookie: admin.Cookie);

        userDetails.User.SsoProviders.Should().HaveCount(2);
        userDetails.User.SsoProviders.Should().Contain(provider1.Name);
        userDetails.User.SsoProviders.Should().Contain(provider2.Name);
    }

    [Fact]
    public async Task sso_callback_with_invalid_code_redirects_with_error()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(user);

        // Do NOT register any auth code in MockOidcServer

        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");

        //when
        var callbackResult = await Api.Sso.Callback(
            code: "invalid-code-that-does-not-exist",
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=token-exchange-failed");
    }

    [Fact]
    public async Task deleting_auth_provider_removes_sso_link_but_user_retains_resources()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var provider1 = await CreateAndActivateAuthProvider(admin);
        var provider2 = await CreateAndActivateAuthProvider(admin);

        var ssoEmail = Random.Email("sso");
        var sub1 = Random.Sub();
        var sub2 = Random.Sub();

        // Create user via provider1, then link provider2
        var ssoUser = await SignInViaSso(provider1, ssoEmail, sub1);
        await SignInViaSso(provider2, ssoEmail, sub2);

        await GrantWorkspacePermission(ssoUser.ExternalId, admin);

        var storage = await CreateHardDriveStorage(admin);
        ssoUser = await SignInViaSso(provider1, ssoEmail, sub1);
        var workspace = await CreateWorkspace(storage, ssoUser);

        //when
        await Api.AuthProviders.Delete(
            externalId: provider1.ExternalId,
            cookie: admin.Cookie,
            antiforgery: admin.Antiforgery);

        //then - user still exists with resources, provider1 removed from SsoProviders
        var userDetails = await Api.Users.GetDetails(
            userExternalId: ssoUser.ExternalId,
            cookie: admin.Cookie);

        userDetails.User.Email.Should().Be(ssoEmail);
        userDetails.User.SsoProviders.Should().ContainSingle()
            .Which.Should().Be(provider2.Name);
        userDetails.Workspaces.Should().ContainSingle()
            .Which.ExternalId.Should().Be(workspace.ExternalId);

        // user can still log in via provider2
        var reloggedUser = await SignInViaSso(provider2, ssoEmail, sub2);
        reloggedUser.ExternalId.Should().Be(ssoUser.ExternalId);

        var dashboard = await Api.Dashboard.Get(cookie: reloggedUser.Cookie);
        dashboard.Workspaces.Should().ContainSingle()
            .Which.ExternalId.Should().Be(workspace.ExternalId.Value);
    }

    [Fact]
    public async Task sso_login_with_signup_disabled_fails_for_new_user()
    {
        //given
        var admin = await SignIn(Users.AppOwner);

        await Api.GeneralSettings.SetApplicationSignUp(
            value: AppSettings.SignUpSetting.OnlyInvitedUsers,
            cookie: admin.Cookie,
            antiforgery: admin.Antiforgery);

        var authProvider = await CreateAndActivateAuthProvider(admin);

        var ssoEmail = Random.Email("sso");
        var ssoSub = Random.Sub();
        var authCode = Random.AuthCode();

        //when
        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");

        MockOidcServer.RegisterAuthCode(authCode, ssoEmail, ssoSub, nonce!, clientId!);

        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().Contain("error=account-not-found");
    }

    [Fact]
    public async Task sso_login_with_signup_disabled_works_for_invited_user()
    {
        //given
        var admin = await SignIn(Users.AppOwner);
        var authProvider = await CreateAndActivateAuthProvider(admin);

        await Api.GeneralSettings.SetApplicationSignUp(
            value: AppSettings.SignUpSetting.OnlyInvitedUsers,
            cookie: admin.Cookie,
            antiforgery: admin.Antiforgery);

        // Admin invites user (creates invitation record), user does NOT register with password
        var invitedUser = await InviteUser(admin);

        var ssoSub = Random.Sub();
        var authCode = Random.AuthCode();

        //when - invited user logs in via SSO instead of registering with password
        var initiateResult = await Api.Sso.Initiate(authProvider.ExternalId);
        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");

        MockOidcServer.RegisterAuthCode(authCode, invitedUser.Email, ssoSub, nonce!, clientId!);

        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        //then
        callbackResult.StatusCode.Should().Be(302);
        callbackResult.LocationHeader.Should().NotContain("error=");
        callbackResult.LocationHeader.Should().BeEquivalentUrl(AppUrl);

        var userDetails = await Api.Users.GetDetails(
            userExternalId: invitedUser.ExternalId,
            cookie: admin.Cookie);

        userDetails.User.Email.Should().Be(invitedUser.Email);
        userDetails.User.IsEmailConfirmed.Should().BeTrue();
        userDetails.User.HasPassword.Should().BeFalse();
        userDetails.User.SsoProviders.Should().ContainSingle()
            .Which.Should().Be(authProvider.Name);
    }

    private async Task GrantWorkspacePermission(
        UserExtId userExternalId,
        AppSignedInUser admin,
        int maxWorkspaces = 1)
    {
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: userExternalId,
            request: new UserPermissionsAndRolesDto
            {
                CanAddWorkspace = true,

                IsAdmin = false,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanManageAuth = false,
                CanManageIntegrations = false
            },
            cookie: admin.Cookie,
            antiforgery: admin.Antiforgery);

        await Api.Users.UpdateMaxWorkspaceNumber(
            userExternalId: userExternalId,
            request: new UpdateUserMaxWorkspaceNumberRequestDto { 
                MaxWorkspaceNumber = maxWorkspaces },
            cookie: admin.Cookie,
            antiforgery: admin.Antiforgery);
    }
}
