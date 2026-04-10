using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using FluentAssertions;
using PlikShare.Account.Contracts;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.Auth.Contracts;
using PlikShare.Core.Emails;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Auth;

[Collection(IntegrationTestsCollection.Name)]
public class user_registration_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public user_registration_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;

        //we need email provider configured to be able to extract email confirmation code
        CreateAndActivateEmailProviderIfMissing(user: AppOwner).Wait();

        Api.GeneralSettings
            .SetApplicationSignUp(
                value: AppSettings.SignUpSetting.Everyone,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery)
            .Wait();
    }

    [Fact]
    public async Task when_everyone_can_sign_up_new_user_can_sign_up_without_invitation_code()
    {
        //given
        var userEmail = Random.Email();

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, _) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = userEmail,
                InvitationCode = null,
                Password = Random.Password(),
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        signUpResponse.Should().Be(SignUpUserResponseDto.ConfirmationEmailSent);

        var (expectedEmailTitle, _) = Emails.ConfirmationEmail(
            applicationName: AppSettings.ApplicationName.Name,
            link: null);

        await WaitFor(() =>
        {
            var confirmationEmail = ResendEmailServer.GetLastEmailTo(
                userEmail);

            confirmationEmail.Should().NotBeNull();
            confirmationEmail.Body.Subject.Should().Be(expectedEmailTitle);
        });
    }

    [Fact]
    public async Task code_from_confirmation_email_can_be_used_to_finalize_sign_up_process()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, confirmationCode, antiforgeryCookies) = await SignUp(
            email: userEmail,
            password: userPassword);

        //when
        var result = await Api.Auth.ConfirmEmail(
            request: new ConfirmEmailRequestDto
            {
                UserExternalId = userExternalId.Value,
                Code = confirmationCode
            },
            antiforgeryCookies: antiforgeryCookies);

        //then
        result.Should().Be(ConfirmEmailResponseDto.EmailConfirmed);
    }

    [Fact]
    public async Task cannot_confirm_email_with_wrong_code()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, _, antiforgeryCookies) = await SignUp(
            email: userEmail,
            password: userPassword);

        //when
        var result = await Api.Auth.ConfirmEmail(
            request: new ConfirmEmailRequestDto
            {
                UserExternalId = userExternalId.Value,
                Code = "wrong-confirmation-code"
            },
            antiforgeryCookies: antiforgeryCookies);

        //then
        result.Should().Be(ConfirmEmailResponseDto.InvalidToken);
    }

    [Fact]
    public async Task when_email_is_confirmed_user_can_sign_in()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, antiforgeryCookies) = await SignUpAndConfirmEmail(
            email: userEmail,
            password: userPassword);

        //when
        var (signInResponse, cookie, _) = await Api.Auth.SignIn(
            email: userEmail,
            password: userPassword,
            antiforgeryCookies: antiforgeryCookies);

        //then
        signInResponse.Should().Be(SignInUserResponseDto.Successful);

        var userDetails = await Api.Account.GetDetails(
            cookie: cookie);

        userDetails.Should().BeEquivalentTo(new GetAccountDetailsResponseDto
        {
            Email = userEmail,
            ExternalId = userExternalId,
            MaxWorkspaceNumber = AppSettings.NewUserDefaultMaxWorkspaceNumber.Value,
            Permissions = new UserPermissions
            {
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanAddWorkspace = false,
                CanManageAuditLog = false
            },
            Roles = new UserRoles
            {
                IsAdmin = false,
                IsAppOwner = false
            },
            HasPassword = true
        });
    }

    [Fact]
    public async Task when_email_is_not_confirmed_user_cannot_sign_in()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (_, _, antiforgeryCookies) = await SignUp(
            email: userEmail,
            password: userPassword);

        //when
        var (signInResponse, cookie, _) = await Api.Auth.SignIn(
            email: userEmail,
            password: userPassword,
            antiforgeryCookies: antiforgeryCookies);

        //then
        signInResponse.Should().Be(SignInUserResponseDto.Failed);
    }

    [Fact]
    public async Task successful_sign_up_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, _) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = userEmail,
                InvitationCode = null,
                Password = userPassword,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        signUpResponse.Should().Be(SignUpUserResponseDto.ConfirmationEmailSent);

        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.SignedUp,
            expectedActorEmail: userEmail,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task successful_email_confirmation_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, confirmationCode, antiforgeryCookies) = await SignUp(
            email: userEmail,
            password: userPassword);

        //when
        var result = await Api.Auth.ConfirmEmail(
            request: new ConfirmEmailRequestDto
            {
                UserExternalId = userExternalId.Value,
                Code = confirmationCode
            },
            antiforgeryCookies: antiforgeryCookies);

        //then
        result.Should().Be(ConfirmEmailResponseDto.EmailConfirmed);

        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.EmailConfirmed,
            expectedActorEmail: userEmail,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task failed_email_confirmation_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, _, antiforgeryCookies) = await SignUp(
            email: userEmail,
            password: userPassword);

        //when
        var result = await Api.Auth.ConfirmEmail(
            request: new ConfirmEmailRequestDto
            {
                UserExternalId = userExternalId.Value,
                Code = "wrong-confirmation-code"
            },
            antiforgeryCookies: antiforgeryCookies);

        //then
        result.Should().Be(ConfirmEmailResponseDto.InvalidToken);

        await AssertAuditLogContains<Audit.Auth.Failed>(
            expectedEventType: AuditLogEventTypes.Auth.EmailConfirmationFailed,
            assertDetails: details => details.Reason.Should().Be(AuditLogFailureReasons.Auth.InvalidToken),
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task successful_sign_in_after_registration_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (_, antiforgeryCookies) = await SignUpAndConfirmEmail(
            email: userEmail,
            password: userPassword);

        //when
        var (signInResponse, _, _) = await Api.Auth.SignIn(
            email: userEmail,
            password: userPassword,
            antiforgeryCookies: antiforgeryCookies);

        //then
        signInResponse.Should().Be(SignInUserResponseDto.Successful);

        await AssertAuditLogContains<Audit.Auth.SignedIn>(
            expectedEventType: AuditLogEventTypes.Auth.SignedIn,
            assertDetails: details => details.Method.Should().Be(AuditLogSignInMethods.Password),
            expectedActorEmail: userEmail);
    }

    [Fact]
    public async Task forgot_password_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (_, antiforgeryCookies) = await SignUpAndConfirmEmail(
            email: userEmail,
            password: userPassword);

        //when
        await Api.Auth.ForgotPassword(
            request: new ForgotPasswordRequestDto(
                Email: userEmail),
            antiforgeryCookies: antiforgeryCookies);

        //then
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.PasswordResetRequested,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task successful_password_reset_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, antiforgeryCookies) = await SignUpAndConfirmEmail(
            email: userEmail,
            password: userPassword);

        await Api.Auth.ForgotPassword(
            request: new ForgotPasswordRequestDto(
                Email: userEmail),
            antiforgeryCookies: antiforgeryCookies);

        var (resetUserId, resetCode) = await ExtractCodeFromResetPasswordEmail(userEmail);

        //when
        var result = await Api.Auth.ResetPassword(
            request: new ResetPasswordRequestDto(
                UserExternalId: resetUserId,
                Code: resetCode,
                NewPassword: Random.Password()),
            antiforgeryCookies: antiforgeryCookies);

        //then
        result.Should().Be(ResetPasswordResponseDto.PasswordReset);

        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.PasswordResetCompleted);
    }

    [Fact]
    public async Task failed_password_reset_with_invalid_token_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();
        var userPassword = Random.Password();

        var (userExternalId, antiforgeryCookies) = await SignUpAndConfirmEmail(
            email: userEmail,
            password: userPassword);

        //when
        var result = await Api.Auth.ResetPassword(
            request: new ResetPasswordRequestDto(
                UserExternalId: userExternalId.Value,
                Code: "invalid-reset-token",
                NewPassword: Random.Password()),
            antiforgeryCookies: antiforgeryCookies);

        //then
        result.Should().Be(ResetPasswordResponseDto.InvalidToken);

        await AssertAuditLogContains<Audit.Auth.Failed>(
            expectedEventType: AuditLogEventTypes.Auth.PasswordResetFailed,
            assertDetails: details => details.Reason.Should().Be(AuditLogFailureReasons.Auth.InvalidToken),
            expectedSeverity: AuditLogSeverities.Warning);
    }

    private async Task<(string UserId, string Code)> ExtractCodeFromResetPasswordEmail(
        string userEmail)
    {
        var (expectedEmailTitle, _) = Emails.ResetPassword(
            applicationName: AppSettings.ApplicationName.Name,
            link: null!);

        string? userId = null;
        string? code = null;

        await WaitFor(() =>
        {
            var resetEmail = ResendEmailServer.ReceivedEmails
                .LastOrDefault(e =>
                    e.Body.To.Contains(userEmail, StringComparer.OrdinalIgnoreCase) &&
                    e.Body.Subject == expectedEmailTitle);

            resetEmail.Should().NotBeNull();

            (userId, code) = ResetPasswordExtractor.ExtractResetData(resetEmail!.Body.Html);
        });

        return (
            UserId: userId!,
            Code: code!);
    }

    public static class ResetPasswordExtractor
    {
        public static (string UserId, string Code) ExtractResetData(string emailHtml)
        {
            if (string.IsNullOrWhiteSpace(emailHtml))
                return default;

            var urlPattern = @"https?://[^/]+/reset-password\?userId=([^&\s<]+)&code=([^&\s<]+)";
            var match = Regex.Match(emailHtml, urlPattern, RegexOptions.IgnoreCase);

            if (!match.Success)
                return default;

            var userId = HttpUtility.UrlDecode(match.Groups[1].Value);
            var code = HttpUtility.UrlDecode(match.Groups[2].Value);

            return (
                UserId: userId,
                Code: code);
        }
    }

    private async Task<SignUpResult> SignUp(string email, string password)
    {
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, _) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = email,
                InvitationCode = null,
                Password = password,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signUpResponse.Should().Be(SignUpUserResponseDto.ConfirmationEmailSent);

        var (userExternalId, confirmationCode) = await ExtractCodeFromConfirmationEmail(
            email);

        return new SignUpResult(
            userExternalId, 
            confirmationCode,
            anonymousAntiforgeryCookies);
    }

    private async Task<SignUpAndConfirmEmailResult> SignUpAndConfirmEmail(string email, string password)
    {
        var (userExternalId, confirmationCode, antiforgeryCookies) = await SignUp(
            email: email,
            password: password);

        var result = await Api.Auth.ConfirmEmail(
            request: new ConfirmEmailRequestDto
            {
                UserExternalId = userExternalId.Value,
                Code = confirmationCode
            },
            antiforgeryCookies: antiforgeryCookies);

        result.Should().Be(ConfirmEmailResponseDto.EmailConfirmed);

        return new SignUpAndConfirmEmailResult(userExternalId, antiforgeryCookies);
    }

    private async Task<(UserExtId UserExternalId, string Code)> ExtractCodeFromConfirmationEmail(
        string userEmail)
    {
        var (expectedEmailTitle, _) = Emails.ConfirmationEmail(
            applicationName: AppSettings.ApplicationName.Name,
            link: null);

        string? userId = null;
        string? code = null;

        await WaitFor(() =>
        {
            var confirmationEmail = ResendEmailServer.GetLastEmailTo(
                userEmail);

            confirmationEmail.Should().NotBeNull();
            confirmationEmail.Body.Subject.Should().Be(expectedEmailTitle);

            (userId, code) = EmailConfirmationExtractor.ExtractConfirmationData(confirmationEmail.Body.Html);
        });

        return (
            UserExternalId: UserExtId.Parse(userId!), 
            Code: code!
            );
    }

    public static class EmailConfirmationExtractor
    {
        public static (string UserId, string Code) ExtractConfirmationData(string emailHtml)
        {
            if (string.IsNullOrWhiteSpace(emailHtml))
                return default;

            // Pattern to match the confirmation URL
            var urlPattern = @"https?://[^/]+/email-confirmation\?userId=([^&\s<]+)&code=([^&\s<]+)";
            var match = Regex.Match(emailHtml, urlPattern, RegexOptions.IgnoreCase);

            if (!match.Success)
                return default;

            var userId = HttpUtility.UrlDecode(match.Groups[1].Value);
            var code = HttpUtility.UrlDecode(match.Groups[2].Value);

            return 
            (
                UserId: userId,
                Code: code
            );
        }
    }

    private record SignUpResult(
        UserExtId UserExternalId,
        string EmailConfirmationCode,
        AntiforgeryCookies AntiforgeryCookies);


    private record SignUpAndConfirmEmailResult(
        UserExtId UserExternalId,
        AntiforgeryCookies AntiforgeryCookies);
}
