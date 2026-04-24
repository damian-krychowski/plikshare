using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlikShare.Account.Contracts;
using PlikShare.AuditLog;
using PlikShare.Auth.Contracts;
using PlikShare.Core.Emails;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.GeneralSettings;
using System.Text;
using System.Text.Json;
using PlikShare.AuditLog.Details;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Users.Cache;
using PlikShare.Users.Invite;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Users.PermissionsAndRoles;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Auth;

[Collection(IntegrationTestsCollection.Name)]
public class user_invitation_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppEmailProvider EmailProvider { get; }

    public user_invitation_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
        EmailProvider = CreateAndActivateEmailProviderIfMissing(user: AppOwner).Result;

        Api.GeneralSettings
            .SetApplicationSignUp(
                value: AppSettings.SignUpSetting.OnlyInvitedUsers,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery)
            .Wait();
    }

    [Fact]
    public async Task app_owner_can_invite_new_user()
    {
        //given
        var user1Email = Random.Email();
        var user2Email = Random.Email();

        //when
        var invitationResult = await Api.Users.InviteUsers(
            request: new InviteUsersRequestDto
            {
                Emails =
                [
                    user1Email,
                    user2Email
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        invitationResult.Should().BeEquivalentTo(new InviteUsersResponseDto
        { 
            Users = 
            [
                new InvitedUserDto 
                {
                    ExternalId = default,
                    Email = user1Email,
                    MaxWorkspaceNumber = 0,
                    DefaultMaxWorkspaceSizeInBytes = 0,
                    DefaultMaxWorkspaceTeamMembers = 0,
                    PermissionsAndRoles = new UserPermissionsAndRolesDto
                    {
                        IsAdmin = false,
                        CanAddWorkspace = false,
                        CanManageEmailProviders = false,
                        CanManageGeneralSettings = false,
                        CanManageStorages = false,
                        CanManageUsers = false,
                        CanManageAuth = false,
                        CanManageIntegrations = false,
                        CanManageAuditLog = false
                    }
                },

                new InvitedUserDto
                {
                    ExternalId = default,
                    Email = user2Email,
                    MaxWorkspaceNumber = 0,
                    DefaultMaxWorkspaceSizeInBytes = 0,
                    DefaultMaxWorkspaceTeamMembers = 0,
                    PermissionsAndRoles = new UserPermissionsAndRolesDto
                    {
                        IsAdmin = false,
                        CanAddWorkspace = false,
                        CanManageEmailProviders = false,
                        CanManageGeneralSettings = false,
                        CanManageStorages = false,
                        CanManageUsers = false,
                        CanManageAuth = false,
                        CanManageIntegrations = false,
                        CanManageAuditLog = false
                    }
                }
            ]
        },
            opt => opt.For(x => x.Users).Exclude(x => x.ExternalId));
    }

    [Fact]
    public async Task when_new_users_are_invited_they_should_receive_emails_with_invitation_codes()
    {
        //given
        var user1 = new
        {
            Email = Random.Email(),
            Code = Random.InvitationCode()
        };
        
        var user2 = new
        {
            Email = Random.Email(),
            Code = Random.InvitationCode()
        };
        
        OneTimeInvitationCode.AddCodes([
            user1.Code,
            user2.Code
        ]);

        //when
        await Api.Users.InviteUsers(
            request: new InviteUsersRequestDto
            {
                Emails = 
                [
                    user1.Email,
                    user2.Email
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var (expectedTitle1, expectedContent1) = Emails.UserInvitation(
            applicationName: AppSettings.ApplicationName.Name,
            appUrl: AppUrl,
            inviterEmail: AppOwner.Email,
            invitationCode: user1.Code);

        var expectedHtml1 = EmailTemplates.Generic.Build(
            title: expectedTitle1,
            content: expectedContent1);

        var (expectedTitle2, expectedContent2) = Emails.UserInvitation(
            applicationName: AppSettings.ApplicationName.Name,
            appUrl: AppUrl,
            inviterEmail: AppOwner.Email,
            invitationCode: user2.Code);

        var expectedHtml2 = EmailTemplates.Generic.Build(
            title: expectedTitle2,
            content: expectedContent2);

        await WaitFor(() =>
        {
            ResendEmailServer.ShouldContainEmails([
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [user1.Email],
                    Subject: expectedTitle1,
                    Html: expectedHtml1),
                new ResendRequestBody(
                    From: EmailProvider.EmailFrom,
                    To: [user2.Email],
                    Subject: expectedTitle2,
                    Html: expectedHtml2)
            ]);
        });
    }

    [Fact]
    public async Task invited_user_can_register_using_the_invitation_code()
    {
        //given
        var invitedUser = await InviteUser(
            user: AppOwner);

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, cookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = Random.Password(),
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        signUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.SingedUpAndSignedIn);
        cookie.Should().NotBeNull();
    }

    [Fact]
    public async Task freshly_invited_and_registered_user_can_access_his_account_details()
    {
        //given
        var invitedUser = await InviteUser(
            user: AppOwner);

        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (_, cookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = Random.Password(),
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //when
        var accountDetails = await Api.Account.GetDetails(
            cookie: cookie);

        //then
        accountDetails.Should().BeEquivalentTo(new GetAccountDetailsResponseDto
        {
            ExternalId = invitedUser.ExternalId,
            Email = invitedUser.Email,
            Roles = new UserRoles
            {
                IsAdmin = false,
                IsAppOwner = false
            },
            Permissions = new UserPermissions
            {
                CanAddWorkspace = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = false,
                CanManageStorages = false,
                CanManageUsers = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageAuditLog = false
            },
            MaxWorkspaceNumber = 0,
            HasPassword = true,
            IsEncryptionConfigured = false,
            IsEncryptionUnlocked = false
        });
    }

    [Fact]
    public async Task when_invitation_code_is_wrong_user_cannot_register()
    {
        //given
        var invitedUser = await InviteUser(
            user: AppOwner);

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, cookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = Random.Password(),
                InvitationCode = "wrong-invitation-code",
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        signUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.InvitationRequired);
        cookie.Should().BeNull();
    }

    [Fact]
    public async Task when_invitation_code_is_correct_but_user_email_does_not_match_then_cannot_register()
    {
        //given
        var invitedUser = await InviteUser(
            user: AppOwner);

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, cookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = Random.Email("wrong-email"),
                Password = Random.Password(),
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        signUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.InvitationRequired);
        cookie.Should().BeNull();
    }

    [Fact]
    public async Task when_everyone_can_sing_up_to_application_invitation_codes_still_works()
    {
        //given
        await Api
            .GeneralSettings
            .SetApplicationSignUp(
                value: AppSettings.SignUpSetting.Everyone,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery);

        var invitedUser = await InviteUser(
            user: AppOwner);

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        var (signUpResponse, cookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = Random.Password(),
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        signUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.SingedUpAndSignedIn);
        cookie.Should().NotBeNull();
    }

    [Fact]
    public async Task successful_invited_user_registration_should_produce_audit_log_entry()
    {
        //given
        var invitedUser = await InviteUser(
            user: AppOwner);

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = Random.Password(),
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.SignedUp,
            expectedActorEmail: invitedUser.Email);
    }

    [Fact]
    public async Task inviting_user_should_produce_audit_log_entry()
    {
        //given
        var userEmail = Random.Email();

        //when
        await Api.Users.InviteUsers(
            request: new InviteUsersRequestDto
            {
                Emails =
                [
                    userEmail
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.User.Invited>(
            expectedEventType: AuditLogEventTypes.User.Invited,
            assertDetails: details => details.Users.Should().Contain(u => u.Email == userEmail),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public void invitation_code_is_256_bit_base62_when_not_mocked()
    {
        // Uses the real generator (not the mock) to assert the entropy/encoding contract.
        var realGenerator = new OneTimeInvitationCode();

        var code = realGenerator.Generate();

        // 32 bytes of entropy → up to 43 Base62 chars; ~97.6% of draws hit 43, the rest
        // a few chars shorter because the high bits happened to be zero.
        code.Length.Should().BeInRange(41, 43);
        code.Should().MatchRegex("^[0-9a-zA-Z]+$");

        // Two independent draws should differ with overwhelming probability.
        var anotherCode = realGenerator.Generate();
        anotherCode.Should().NotBe(code);
    }

    [Fact]
    public async Task invitation_code_stored_in_database_is_hashed_not_plaintext()
    {
        //given
        var predefined = Random.InvitationCode();
        OneTimeInvitationCode.AddCode(predefined);

        var email = Random.Email();

        //when
        await Api.Users.InviteUsers(
            request: new InviteUsersRequestDto { Emails = [email] },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var storedHash = GetStoredInvitationCodeHash(email);
        storedHash.Should().NotBeNull("an invited user must have a stored hash");
        storedHash!.Length.Should().Be(32, "HMAC-SHA256 output is 32 bytes");

        var plaintextUtf8 = Encoding.UTF8.GetBytes(predefined);
        storedHash.Should().NotEqual(plaintextUtf8, "the plaintext code must not leak into storage");

        var expectedHash = InvitationCodeHasher.Hash(predefined);
        storedHash.Should().Equal(expectedHash, "the stored value must be the SHA-256 of the plaintext");
    }

    [Fact]
    public async Task registration_with_wrong_invitation_code_should_produce_audit_log_entry()
    {
        //given
        var invitedUser = await InviteUser(
            user: AppOwner);

        //when
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = Random.Password(),
                InvitationCode = "wrong-invitation-code",
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        await AssertAuditLogContains<Audit.Auth.Failed>(
            expectedEventType: AuditLogEventTypes.Auth.SignUpFailed,
            assertDetails: details => details.Reason.Should().Be(AuditLogFailureReasons.Auth.WrongInvitationCode),
            expectedSeverity: AuditLogSeverities.Warning);
    }
}
