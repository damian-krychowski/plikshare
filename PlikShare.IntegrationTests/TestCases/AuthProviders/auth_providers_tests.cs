using FluentAssertions;
using PlikShare.AuthProviders.Create.Contracts;
using PlikShare.AuthProviders.Entities;
using PlikShare.AuthProviders.List.Contracts;
using PlikShare.AuthProviders.TestConfiguration.Contracts;
using PlikShare.AuthProviders.Update.Contracts;
using PlikShare.AuthProviders.UpdateName.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.AuthProviders;

[Collection(IntegrationTestsCollection.Name)]
public class auth_providers_tests : TestFixture
{
    public auth_providers_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        hostFixture.RemoveAllAuthProviders();
        MockOidcServer.Reset();
    }

    [Fact]
    public async Task when_oidc_provider_is_created_it_is_visible_on_the_list()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var providerName = Random.Name("OidcProvider");
        var clientId = Random.ClientId();
        var clientSecret = Random.ClientSecret();

        //when
        var response = await Api.AuthProviders.CreateOidc(
            request: new CreateOidcAuthProviderRequestDto
            {
                Name = providerName,
                ClientId = clientId,
                ClientSecret = clientSecret,
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var providers = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        providers.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new GetAuthProvidersItemDto
            {
                ExternalId = providers.Items[0].ExternalId,
                Name = providerName,
                Type = AuthProviderType.Oidc.Value,
                IsActive = false,
                ClientId = clientId,
                IssuerUrl = MockOidcServer.IssuerUrl
            });

        response.ExternalId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task when_oidc_provider_is_updated_changes_are_reflected_on_the_list()
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

        var newName = Random.Name("UpdatedProvider");
        var newClientId = Random.ClientId();
        var newClientSecret = Random.ClientSecret();

        //when
        await Api.AuthProviders.Update(
            externalId: provider.ExternalId,
            request: new UpdateAuthProviderRequestDto
            {
                Name = newName,
                ClientId = newClientId,
                ClientSecret = newClientSecret,
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var providers = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        providers.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new GetAuthProvidersItemDto
            {
                ExternalId = providers.Items[0].ExternalId,
                Name = newName,
                Type = AuthProviderType.Oidc.Value,
                IsActive = false,
                ClientId = newClientId,
                IssuerUrl = MockOidcServer.IssuerUrl
            });
    }

    [Fact]
    public async Task when_oidc_provider_name_is_updated_it_is_reflected_on_the_list()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var clientId = Random.ClientId();

        var provider = await Api.AuthProviders.CreateOidc(
            request: new CreateOidcAuthProviderRequestDto
            {
                Name = Random.Name("OidcProvider"),
                ClientId = clientId,
                ClientSecret = Random.ClientSecret(),
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        var newName = Random.Name("RenamedProvider");

        //when
        await Api.AuthProviders.UpdateName(
            externalId: provider.ExternalId,
            request: new UpdateAuthProviderNameRequestDto
            {
                Name = newName
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var providers = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        providers.Items.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new GetAuthProvidersItemDto
            {
                ExternalId = providers.Items[0].ExternalId,
                Name = newName,
                Type = AuthProviderType.Oidc.Value,
                IsActive = false,
                ClientId = clientId,
                IssuerUrl = MockOidcServer.IssuerUrl
            });
    }

    [Fact]
    public async Task when_oidc_provider_is_activated_it_is_reflected_on_the_list()
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

        //when
        await Api.AuthProviders.Activate(
            externalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var providers = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        providers.Items.Should().ContainSingle()
            .Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task when_oidc_provider_is_deactivated_it_is_reflected_on_the_list()
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

        await Api.AuthProviders.Activate(
            externalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        await Api.AuthProviders.Deactivate(
            externalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var providers = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        providers.Items.Should().ContainSingle()
            .Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task when_oidc_provider_is_deleted_it_is_removed_from_the_list()
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

        //when
        await Api.AuthProviders.Delete(
            externalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var providers = await Api.AuthProviders.Get(
            cookie: user.Cookie);

        providers.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task test_configuration_with_valid_config_returns_ok()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        MockOidcServer.ShouldFailClientCredentials = false;

        //when
        var result = await Api.AuthProviders.TestConfiguration(
            request: new TestAuthProviderConfigurationRequestDto
            {
                IssuerUrl = MockOidcServer.IssuerUrl,
                ClientId = Random.ClientId(),
                ClientSecret = Random.ClientSecret()
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        result.Code.Should().Be("ok");
    }

    [Fact]
    public async Task test_configuration_with_invalid_credentials_returns_failed()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        MockOidcServer.ShouldFailClientCredentials = true;

        //when
        var result = await Api.AuthProviders.TestConfiguration(
            request: new TestAuthProviderConfigurationRequestDto
            {
                IssuerUrl = MockOidcServer.IssuerUrl,
                ClientId = Random.ClientId(),
                ClientSecret = Random.ClientSecret()
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        result.Code.Should().Be("failed");
    }

    [Fact]
    public async Task creating_provider_with_duplicate_name_should_fail()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var providerName = Random.Name("OidcProvider");

        await Api.AuthProviders.CreateOidc(
            request: new CreateOidcAuthProviderRequestDto
            {
                Name = providerName,
                ClientId = Random.ClientId(),
                ClientSecret = Random.ClientSecret(),
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var act = () => Api.AuthProviders.CreateOidc(
            request: new CreateOidcAuthProviderRequestDto
            {
                Name = providerName,
                ClientId = Random.ClientId(),
                ClientSecret = Random.ClientSecret(),
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        await act.Should().ThrowAsync<TestApiCallException>()
            .Where(e => e.StatusCode == 400);
    }
}
