using FluentAssertions;
using PlikShare.Account.Contracts;
using PlikShare.Auth.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Auth;

[Collection(IntegrationTestsCollection.Name)]
public class user_registration_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public user_registration_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
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
        accountDetails.Should().BeEquivalentTo(new GetAccountDetailsResponseDto(
            ExternalId: invitedUser.ExternalId,
            Email: invitedUser.Email,
            Roles: new GetAccountRolesResponseDto(
                IsAdmin: false,
                IsAppOwner: false),
            Permissions: new GetAccountPermissionsResponseDto(
                CanAddWorkspace: true,
                CanManageEmailProviders: false,
                CanManageGeneralSettings: false,
                CanManageStorages: false,
                CanManageUsers: false),
            MaxWorkspaceNumber: null));
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
                InvitationCode = Random.InvitationCode("wrong-code"),
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
}
