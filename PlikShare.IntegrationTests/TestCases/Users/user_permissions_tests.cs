using FluentAssertions;
using PlikShare.Account.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Users.Cache;
using PlikShare.Users.PermissionsAndRoles;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Users;

[Collection(IntegrationTestsCollection.Name)]
public class user_permissions_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppSignedInUser User { get; }

    public user_permissions_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
        User = InviteAndRegisterUser(AppOwner).Result;
    }

    [Fact]
    public async Task app_owner_can_set_user_role_to_admin()
    {
        //when
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: User.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = true,

                CanManageAuth = false,
                CanManageIntegrations = false,
                CanAddWorkspace = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = false,
                CanManageStorages = false,
                CanManageUsers = false,
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Users.GetDetails(
            userExternalId: User.ExternalId,
            cookie: AppOwner.Cookie);

        details.User.Roles.Should().BeEquivalentTo(new UserRoles
        {
            IsAdmin = true,
            IsAppOwner = false
        });

        details.User.Permissions.Should().BeEquivalentTo(new UserPermissions
        {
            CanAddWorkspace = false,
            CanManageEmailProviders = false,
            CanManageGeneralSettings = false,
            CanManageStorages = false,
            CanManageUsers = false,
            CanManageAuth = false,
            CanManageIntegrations = false
        });
    }

    [Theory]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanAddWorkspace), true, false, false, false, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageEmailProviders), false, true, false, false, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageGeneralSettings), false, false, true, false, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageStorages), false, false, false, true, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageUsers), false, false, false, false, true, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageAuth), false, false, false, false, false, true, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageIntegrations), false, false, false, false, false, false, true)]
    [InlineData("AllPermissions", true, true, true, true, true, true, true)]
    public async Task app_owner_can_set_admin_user_permissions(
        string permissionName,
        bool canAddWorkspace,
        bool canManageEmailProviders,
        bool canManageGeneralSettings,
        bool canManageStorages,
        bool canManageUsers,
        bool canManageAuth,
        bool canManageIntegrations)
    {
        //when
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: User.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = true,
                CanManageAuth = canManageAuth,
                CanManageIntegrations = canManageIntegrations,
                CanAddWorkspace = canAddWorkspace,
                CanManageEmailProviders = canManageEmailProviders,
                CanManageGeneralSettings = canManageGeneralSettings,
                CanManageStorages = canManageStorages,
                CanManageUsers = canManageUsers,
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Users.GetDetails(
            userExternalId: User.ExternalId,
            cookie: AppOwner.Cookie);

        details.User.Roles.Should().BeEquivalentTo(new UserRoles
        {
            IsAdmin = true,
            IsAppOwner = false
        });

        details.User.Permissions.Should().BeEquivalentTo(new UserPermissions
        {
            CanAddWorkspace = canAddWorkspace,
            CanManageEmailProviders = canManageEmailProviders,
            CanManageGeneralSettings = canManageGeneralSettings,
            CanManageStorages = canManageStorages,
            CanManageUsers = canManageUsers,
            CanManageAuth = canManageAuth,
            CanManageIntegrations = canManageIntegrations
        });
    }


    [Theory]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanAddWorkspace), true, false, false, false, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageEmailProviders), false, true, false, false, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageGeneralSettings), false, false, true, false, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageStorages), false, false, false, true, false, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageUsers), false, false, false, false, true, false, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageAuth), false, false, false, false, false, true, false)]
    [InlineData(nameof(UserPermissionsAndRolesDto.CanManageIntegrations), false, false, false, false, false, false, true)]
    [InlineData("AllPermissions", true, true, true, true, true, true, true)]
    public async Task admin_permissions_reflects_ones_set_by_the_app_owner(
        string permissionName,
        bool canAddWorkspace,
        bool canManageEmailProviders,
        bool canManageGeneralSettings,
        bool canManageStorages,
        bool canManageUsers,
        bool canManageAuth,
        bool canManageIntegrations)
    {
        //given
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: User.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = true, 

                CanAddWorkspace = canAddWorkspace,
                CanManageAuth = canManageAuth,
                CanManageIntegrations = canManageIntegrations,
                CanManageEmailProviders = canManageEmailProviders,
                CanManageGeneralSettings = canManageGeneralSettings,
                CanManageStorages = canManageStorages,
                CanManageUsers = canManageUsers,
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        var details = await Api.Account.GetDetails(
            cookie: User.Cookie);

        details.Should().BeEquivalentTo(new GetAccountDetailsResponseDto
        {
            Email = User.Email,
            ExternalId = User.ExternalId,
            Permissions = new UserPermissions
            {
                CanAddWorkspace = true, //admin can always add workspaces, now matter the permission

                CanManageAuth = canManageAuth,
                CanManageIntegrations = canManageIntegrations,
                CanManageEmailProviders = canManageEmailProviders,
                CanManageGeneralSettings = canManageGeneralSettings,
                CanManageStorages = canManageStorages,
                CanManageUsers = canManageUsers
            },
            Roles = new UserRoles
            {
                IsAdmin = true,
                IsAppOwner = false
            },
            MaxWorkspaceNumber = AppSettings.NewUserDefaultMaxWorkspaceNumber.Value
        });
    }
}
