using FluentAssertions;
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

    public auth_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}
