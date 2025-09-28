using FluentAssertions;
using PlikShare.Account.Contracts;
using PlikShare.Auth.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Users.Cache;
using Xunit.Abstractions;

#pragma warning disable CS8604 // Possible null reference argument.

namespace PlikShare.IntegrationTests.TestCases.Account;

[Collection(IntegrationTestsCollection.Name)]
public class account_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public account_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task app_owner_account_details_should_reflect_his_roles_and_permissions()
    {
        // when
        var accountDetails = await Api.Account.GetDetails(
            cookie: AppOwner.Cookie);

        // then
        accountDetails.Email.Should().Be(Users.AppOwner.Email);

        accountDetails.Permissions.Should().BeEquivalentTo(new UserPermissions
        {
            CanAddWorkspace = true,
            CanManageEmailProviders = true,
            CanManageStorages = true,
            CanManageUsers = true,
            CanManageGeneralSettings = true,
            CanManageAuth = true,
            CanManageIntegrations = true
        });

        accountDetails.Roles.Should().BeEquivalentTo(new UserRoles
        {
            IsAppOwner = true,
            IsAdmin = true
        });
    }

    [Fact]
    public async Task by_default_2fa_is_disabled()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        // when
        var (status, _) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        // then
        status.IsEnabled.Should().BeFalse();
        status.RecoveryCodesLeft.Should().BeNull();
        status.QrCodeUri.Should().NotBeNull();
    }

    [Fact]
    public async Task can_enable_2fa_with_correct_totp_code()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        // when
        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        // then
        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);
        enableResult.RecoveryCodes.Should().HaveCount(5);
    }

    [Fact]
    public async Task wrong_totp_code_does_not_enable_2fa()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        // when
        var (_, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);


        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: "000000"),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        // then
        enableResult.Code.Should().Be(Enable2FaResponseDto.InvalidVerificationCode.Code);
        enableResult.RecoveryCodes.Should().BeEmpty();
    }

    [Fact]
    public async Task when_2fa_gets_enabled_user_is_not_signed_out()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);
        
        // when
        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, newestCookie) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // then
        await AssertUserCanGetHisAccountDetails(newestCookie, user);
    }

    [Fact]
    public async Task when_2fa_is_enabled_correct_totp_is_required_to_sign_in()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when

        var (signInResponse, _, _) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: await Api.Antiforgery.GetToken());
        
        // then
        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);
    }

    [Fact]
    public async Task when_2fa_is_enabled_user_can_sign_in_with_correct_totp()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);
        
        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var loginTotp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (signIn2FaResponse, signIn2FaCookie, twoFactorRememberMeCookie) = await Api.Auth.SignIn2Fa(
            request: new SignInUser2FaRequestDto(
                VerificationCode: loginTotp,
                RememberDevice: false,
                RememberMe: false),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // then
        signIn2FaResponse.Should().Be(SignInUser2FaResponseDto.Successful);
        signIn2FaCookie.Should().NotBeNull();
        twoFactorRememberMeCookie.Should().BeNull();

        await AssertUserCanGetHisAccountDetails(signIn2FaCookie, user);
    }

    [Fact]
    public async Task when_2fa_is_enabled_wrong_totp_does_not_allow_to_log_in()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);
        
        var (signIn2FaResponse, signIn2FaCookie, twoFactorRememberMeCookie) = await Api.Auth.SignIn2Fa(
            request: new SignInUser2FaRequestDto(
                VerificationCode: "000000",
                RememberDevice: false,
                RememberMe: false),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // then
        signIn2FaResponse.Should().Be(SignInUser2FaResponseDto.InvalidVerificationCode);
        signIn2FaCookie.Should().BeNull();
        twoFactorRememberMeCookie.Should().BeNull();
    }

    [Fact]
    public async Task when_2fa_is_enabled_user_can_sign_in_with_correct_recovery_code()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);
        
        var (signIn2FaResponse, signIn2FaCookie) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: enableResult.RecoveryCodes[0]),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // then
        signIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.Successful);
        signIn2FaCookie.Should().NotBeNull();

        await AssertUserCanGetHisAccountDetails(signIn2FaCookie, user);
    }

    [Fact]
    public async Task when_2fa_is_enabled_user_cannot_sign_in_with_wrong_recovery_code()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var (signIn2FaResponse, signIn2FaCookie) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: "0000-0000"),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // then
        signIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.InvalidRecoveryCode);
        signIn2FaCookie.Should().BeNull();
    }

    [Fact]
    public async Task one_recovery_code_can_be_used_only_once()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var (signIn2FaResponse, _) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: enableResult.RecoveryCodes[0]),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.Successful);

        // when
        var (secondSignIn2FaResponse, _) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: enableResult.RecoveryCodes[0]),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // then
        secondSignIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.InvalidRecoveryCode);
        secondSignIn2FaResponse.Should().NotBeNull();
    }

    [Fact]
    public async Task when_recover_code_is_used_number_of_left_recovery_codes_is_updated()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var (signIn2FaResponse, newestCookie) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: enableResult.RecoveryCodes[0]),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.Successful);

        // when
        var (new2FaStatus, _) = await Api.Account.Get2FaStatus(
            cookie: newestCookie);

        // then
        new2FaStatus.Should().BeEquivalentTo(new Get2FaStatusResponseDto(
            IsEnabled: true,
            RecoveryCodesLeft: 4,
            QrCodeUri: null));
    }

    [Fact]
    public async Task can_generate_new_set_of_recovery_codes_and_use_them_to_sign_in()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, newestCookie) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var recoveryCodes = await Api.Account.GenerateRecoveryCode(
            cookie: newestCookie,
            antiforgeryCookies: user.Antiforgery);

        // then
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var (signIn2FaResponse, signIn2FaCookie) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: recoveryCodes.RecoveryCodes[0]),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.Successful);
        signIn2FaCookie.Should().NotBeNull();

        await AssertUserCanGetHisAccountDetails(signIn2FaCookie, user);
    }

    [Fact]
    public async Task when_new_recovery_codes_are_generated_old_ones_doest_work_any_longer()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, newestCookie) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        await Api.Account.GenerateRecoveryCode(
            cookie: newestCookie,
            antiforgeryCookies: user.Antiforgery);

        // then
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var (signIn2FaResponse, signIn2FaCookie) = await Api.Auth.SignInRecoveryCode(
            request: new SignInUserRecoveryCodeRequestDto(
                RecoveryCode: enableResult.RecoveryCodes[0]),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signIn2FaResponse.Should().Be(SignInUserRecoveryCodeResponseDto.InvalidRecoveryCode);
        signIn2FaCookie.Should().BeNull();
    }

    [Fact]
    public async Task when_2fa_is_disabled_email_and_password_are_enough_to_log_in()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, newestCookie) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var (disableResult, _) = await Api.Account.Disable2Fa(
            cookie: newestCookie,
            antiforgeryCookies: user.Antiforgery);

        // then
        disableResult.Should().BeEquivalentTo(Disable2FaResponseDto.Disabled);


        var (signInResponse, theNewestCookie, _) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: await Api.Antiforgery.GetToken());

        signInResponse.Code.Should().Be(SignInUserResponseDto.Successful.Code);

        await AssertUserCanGetHisAccountDetails(
            theNewestCookie,
            user);
    }

    [Fact]
    public async Task when_2fa_get_disabled_user_should_not_be_logged_out()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, newestCookie) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        // when
        var (disableResult, theNewestCookie) = await Api.Account.Disable2Fa(
            cookie: newestCookie,
            antiforgeryCookies: user.Antiforgery);

        // then
        disableResult.Should().BeEquivalentTo(Disable2FaResponseDto.Disabled);
        theNewestCookie.Should().NotBeNull();

        await AssertUserCanGetHisAccountDetails(
            theNewestCookie,
            user);
    }

    [Fact]
    public async Task when_2fa_is_enabled_user_can_remember_device_and_he_wont_be_asked_for_totp_again()
    {
        //given
        var user = await InviteAndRegisterUser(
            user: AppOwner);

        var (status, newCookie) = await Api.Account.Get2FaStatus(
            cookie: user.Cookie);

        var totp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (enableResult, _) = await Api.Account.Enable2Fa(
            request: new Enable2FaRequestDto(
                VerificationCode: totp),
            cookie: newCookie,
            antiforgeryCookies: user.Antiforgery);

        enableResult.Code.Should().Be(Enable2FaResponseDto.EnabledCode);

        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signInResponse, _, twoFactorCookie) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signInResponse.Code.Should().Be(SignInUserResponseDto.Required2Fa.Code);

        var loginTotp = TotpCodes.Generate(
            uri: status.QrCodeUri);

        var (signIn2FaResponse, signIn2FaCookie, twoFactorRememberMeCookie) = await Api.Auth.SignIn2Fa(
            request: new SignInUser2FaRequestDto(
                VerificationCode: loginTotp,
                RememberDevice: true,
                RememberMe: false),
            cookie: twoFactorCookie,
            antiforgeryCookies: anonymousAntiforgeryCookies);
        
        signIn2FaResponse.Should().Be(SignInUser2FaResponseDto.Successful);
        signIn2FaCookie.Should().NotBeNull();
        twoFactorRememberMeCookie.Should().NotBeNull();

        // when
        var (singInResponse, singInCookie, _) = await Api.Auth.SignIn(
            email: user.Email,
            password: user.Password,
            antiforgeryCookies: await Api.Antiforgery.GetToken(),
            twoFactorRememberMeCookie: twoFactorRememberMeCookie);
        
        // then
        singInResponse.Should().BeEquivalentTo(SignInUserResponseDto.Successful);
        singInCookie.Should().NotBeNull();

        await AssertUserCanGetHisAccountDetails(singInCookie, user);
    }

    private async Task AssertUserCanGetHisAccountDetails(SessionAuthCookie cookie, AppSignedInUser user)
    {
        var accountDetails = await Api.Account.GetDetails(
            cookie);

        accountDetails.Should().BeEquivalentTo(new GetAccountDetailsResponseDto
        {
            ExternalId = user.ExternalId,
            Email = user.Email,
            Roles = new UserRoles
            {
                IsAdmin = false,
                IsAppOwner = false
            },
            Permissions = new UserPermissions()
            {
                CanAddWorkspace = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = false,
                CanManageStorages = false,
                CanManageUsers = false,
                CanManageAuth = false,
                CanManageIntegrations = false
            },
            MaxWorkspaceNumber = 0
        });
    }
}