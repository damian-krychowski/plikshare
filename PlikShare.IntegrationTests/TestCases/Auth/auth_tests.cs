using System.Text.Json;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Auth.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Auth;

[Collection(IntegrationTestsCollection.Name)]
public class auth_tests : TestFixture
{
    [Fact]
    public async Task logging_in_with_wrong_password_should_fail()
    {
        //given
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        //when
        var (result, sessionAuthCookie, _) = await Api.Auth.SignIn(
            email: Users.AppOwner.Email,
            password: "wrong-password",
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        result.Should().BeEquivalentTo(
            SignInUserResponseDto.Failed);

        sessionAuthCookie.Should().BeNull();
    }

    [Fact]
    public async Task logging_in_with_wrong_email_should_fail()
    {
        //given
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        //when
        var (result, sessionAuthCookie, _) = await Api.Auth.SignIn(
            email: "wrongemail@integrationtests.com",
            password: Users.AppOwner.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        result.Should().BeEquivalentTo(
            SignInUserResponseDto.Failed);

        sessionAuthCookie.Should().BeNull();
    }

    [Fact]
    public async Task can_login_as_application_owner()
    {
        //given
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        //when
        var (result, sessionAuthCookie, _) = await Api.Auth.SignIn(
            email: Users.AppOwner.Email,
            password: Users.AppOwner.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        //then
        result.Should().BeEquivalentTo(
            SignInUserResponseDto.Successful);

        sessionAuthCookie.Should().NotBeNull();
        sessionAuthCookie!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task successful_login_should_produce_audit_log_entry()
    {
        //given & when
        await SignIn(Users.AppOwner);

        //then
        await AssertAuditLogContains<AuditLogDetails.Auth.SignedIn>(
            expectedEventType: AuditLogEventTypes.Auth.SignedIn,
            assertDetails: details => details.Method.Should().Be(AuditLogSignInMethods.Password),
            expectedActorEmail: Users.AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task failed_login_wrong_password_should_produce_audit_log_entry()
    {
        //given
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        //when
        await Api.Auth.SignIn(
            email: Users.AppOwner.Email,
            password: "wrong-password",
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // sign in correctly to have auth for the assertion
        await SignIn(Users.AppOwner);

        //then
        await AssertAuditLogContains<AuditLogDetails.Auth.Failed>(
            expectedEventType: AuditLogEventTypes.Auth.SignInFailed,
            assertDetails: details => details.Reason.Should().Be(AuditLogFailureReasons.Auth.InvalidCredentials),
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task failed_login_wrong_email_should_produce_audit_log_entry()
    {
        //given
        var anonymousAntiforgeryCookies = await Api.Antiforgery.GetToken();

        //when
        await Api.Auth.SignIn(
            email: "wrongemail@integrationtests.com",
            password: Users.AppOwner.Password,
            antiforgeryCookies: anonymousAntiforgeryCookies);

        // sign in correctly to have auth for the assertion
        await SignIn(Users.AppOwner);

        //then
        await AssertAuditLogContains<AuditLogDetails.Auth.Failed>(
            expectedEventType: AuditLogEventTypes.Auth.SignInFailed,
            assertDetails: details => details.Reason.Should().Be(AuditLogFailureReasons.Auth.InvalidCredentials),
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task signing_out_should_produce_audit_log_entry()
    {
        //given
        var signedInUser = await SignIn(Users.AppOwner);

        //when
        await Api.Account.SignOut(
            cookie: signedInUser.Cookie,
            antiforgeryCookies: signedInUser.Antiforgery);

        // sign in again to have auth for the assertion
        await SignIn(Users.AppOwner);

        //then
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.SignedOut);
    }

    public auth_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}
