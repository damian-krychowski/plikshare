using FluentAssertions;
using PlikShare.Core.Emails;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Users.Invite.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Users;

[Collection(IntegrationTestsCollection.Name)]
public class user_invitation_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppEmailProvider EmailProvider { get; }

    public user_invitation_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
        EmailProvider = CreateAndActivateEmailProviderIfMissing(user: AppOwner).Result;
    }

    [Fact]
    public async Task app_owner_can_invite_new_user()
    {
        //given
        var user1Email = Random.Email();
        var user2Email = Random.Email();

        //when
        var invitationResult = await Api.Users.InviteUsers(
            request: new InviteUsersRequestDto([
                user1Email,
                user2Email
            ]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        invitationResult.Should().BeEquivalentTo(new InviteUsersResponseDto([
                new InvitedUserDto(
                    Email: user1Email,
                    ExternalId: default),
                new InvitedUserDto(
                    Email: user2Email,
                    ExternalId: default)
            ]),
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
            request: new InviteUsersRequestDto([
                user1.Email,
                user2.Email
            ]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var (expectedTitle1, expectedContent1) = Emails.UserInvitation(
            applicationName: "PlikShare",
            appUrl: AppUrl,
            inviterEmail: AppOwner.Email,
            invitationCode: user1.Code);

        var expectedHtml1 = EmailTemplates.Generic.Build(
            title: expectedTitle1,
            content: expectedContent1);

        var (expectedTitle2, expectedContent2) = Emails.UserInvitation(
            applicationName: "PlikShare",
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
}
