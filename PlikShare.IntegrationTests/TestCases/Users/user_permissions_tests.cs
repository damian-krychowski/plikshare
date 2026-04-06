using FluentAssertions;
using PlikShare.Account.Contracts;
using PlikShare.AuditLog;
using System.Text.Json;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Users.Cache;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.UpdateDefaultMaxWorkspaceSizeInBytes.Contracts;
using PlikShare.Users.UpdateDefaultMaxWorkspaceTeamMembers.Contracts;
using PlikShare.Users.UpdateMaxWorkspaceNumber.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Users;

[Collection(IntegrationTestsCollection.Name)]
public class user_permissions_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppSignedInUser User { get; }

    public user_permissions_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        ClearAuditLog();
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
                CanManageAuditLog = false
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
            CanManageIntegrations = false,
            CanManageAuditLog = false
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
                CanManageAuditLog = false
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
            CanManageIntegrations = canManageIntegrations,
            CanManageAuditLog = false
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
                CanManageAuditLog = false
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
                CanManageUsers = canManageUsers,
                CanManageAuditLog = false
            },
            Roles = new UserRoles
            {
                IsAdmin = true,
                IsAppOwner = false
            },
            MaxWorkspaceNumber = AppSettings.NewUserDefaultMaxWorkspaceNumber.Value,
            HasPassword = true
        });
    }

    [Fact]
    public async Task updating_user_permissions_should_produce_audit_log_entry()
    {
        //when
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: User.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = true,
                CanAddWorkspace = true,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageEmailProviders = false,
                CanManageGeneralSettings = false,
                CanManageStorages = false,
                CanManageUsers = false,
                CanManageAuditLog = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.PermissionsAndRolesUpdated>(
            expectedEventType: AuditLogEventTypes.User.PermissionsAndRolesUpdated,
            assertDetails: details =>
            {
                details.TargetEmail.Should().Be(User.Email);
                details.IsAdmin.Should().BeTrue();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Critical);
    }

    [Fact]
    public async Task deleting_user_should_produce_audit_log_entry()
    {
        //given
        var userToDelete = await InviteAndRegisterUser(AppOwner);

        //when
        await Api.Users.DeleteUser(
            userExternalId: userToDelete.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.Deleted>(
            expectedEventType: AuditLogEventTypes.User.Deleted,
            assertDetails: details => details.TargetEmail.Should().Be(userToDelete.Email),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Critical);
    }

    [Fact]
    public async Task updating_max_workspace_number_should_produce_audit_log_entry()
    {
        //when
        await Api.Users.UpdateMaxWorkspaceNumber(
            userExternalId: User.ExternalId,
            request: new UpdateUserMaxWorkspaceNumberRequestDto
            {
                MaxWorkspaceNumber = 5
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.LimitUpdated>(
            expectedEventType: AuditLogEventTypes.User.MaxWorkspaceNumberUpdated,
            assertDetails: details => details.Value.Should().Be(5),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_default_max_workspace_size_should_produce_audit_log_entry()
    {
        //when
        await Api.Users.UpdateDefaultMaxWorkspaceSizeInBytes(
            userExternalId: User.ExternalId,
            request: new UpdateUserDefaultMaxWorkspaceSizeInBytesRequestDto
            {
                DefaultMaxWorkspaceSizeInBytes = 1024 * 1024 * 100
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.LimitUpdated>(
            expectedEventType: AuditLogEventTypes.User.DefaultMaxWorkspaceSizeUpdated,
            assertDetails: details => details.Value.Should().Be(1024 * 1024 * 100),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_default_max_workspace_team_members_should_produce_audit_log_entry()
    {
        //when
        await Api.Users.UpdateDefaultMaxWorkspaceTeamMembers(
            userExternalId: User.ExternalId,
            request: new UpdateUserDefaultMaxWorkspaceTeamMembersRequestDto
            {
                DefaultMaxWorkspaceTeamMembers = 10
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.LimitUpdated>(
            expectedEventType: AuditLogEventTypes.User.DefaultMaxWorkspaceTeamMembersUpdated,
            assertDetails: details => details.Value.Should().Be(10),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_list_users()
    {
        //when
        var result = await Api.Users.Get(
            cookie: AppOwner.Cookie);

        //then
        result.Items.Should().NotBeEmpty();
        result.Items.Should().Contain(u => u.Email == Users.AppOwner.Email);
        result.Items.Should().Contain(u => u.Email == User.Email);
    }

    [Fact]
    public async Task can_update_default_max_workspace_size()
    {
        //given
        long newSize = 1024 * 1024 * 500;

        //when
        await Api.Users.UpdateDefaultMaxWorkspaceSizeInBytes(
            userExternalId: User.ExternalId,
            request: new UpdateUserDefaultMaxWorkspaceSizeInBytesRequestDto
            {
                DefaultMaxWorkspaceSizeInBytes = newSize
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Users.GetDetails(
            userExternalId: User.ExternalId,
            cookie: AppOwner.Cookie);

        details.User.DefaultMaxWorkspaceSizeInBytes.Should().Be(newSize);
    }

    [Fact]
    public async Task can_update_default_max_workspace_team_members()
    {
        //given
        int newTeamMembers = 25;

        //when
        await Api.Users.UpdateDefaultMaxWorkspaceTeamMembers(
            userExternalId: User.ExternalId,
            request: new UpdateUserDefaultMaxWorkspaceTeamMembersRequestDto
            {
                DefaultMaxWorkspaceTeamMembers = newTeamMembers
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Users.GetDetails(
            userExternalId: User.ExternalId,
            cookie: AppOwner.Cookie);

        details.User.DefaultMaxWorkspaceTeamMembers.Should().Be(newTeamMembers);
    }
}
