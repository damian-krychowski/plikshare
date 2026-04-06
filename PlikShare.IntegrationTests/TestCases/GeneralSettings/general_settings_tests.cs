using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.GeneralSettings;
using PlikShare.GeneralSettings.Contracts;
using PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Core.Authorization;
using PlikShare.Users.PermissionsAndRoles;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.GeneralSettings;

[Collection(IntegrationTestsCollection.Name)]
public class general_settings_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public general_settings_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task can_change_application_name()
    {
        //when
        await Api.GeneralSettings.SetApplicationName(
            name: "My Custom App",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.ApplicationName.Should().Be("My Custom App");
    }

    [Fact]
    public async Task changing_app_name_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetApplicationName(
            name: "Audit Test App",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Settings.ValueChanged>(
            expectedEventType: AuditLogEventTypes.Settings.AppNameChanged,
            assertDetails: details => details.Value.Should().Be("Audit Test App"),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_change_sign_up_option()
    {
        //when
        await Api.GeneralSettings.SetApplicationSignUp(
            value: AppSettings.SignUpSetting.Everyone,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.ApplicationSignUp.Should().Be(AppSettings.SignUpSetting.Everyone.Value);
    }

    [Fact]
    public async Task changing_sign_up_option_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetApplicationSignUp(
            value: AppSettings.SignUpSetting.Everyone,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Settings.ValueChanged>(
            expectedEventType: AuditLogEventTypes.Settings.SignUpOptionChanged,
            assertDetails: details => details.Value.Should().Be(AppSettings.SignUpSetting.Everyone.Value),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task can_change_default_permissions_and_roles()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultPermissionsAndRoles(
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = false,
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
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.NewUserDefaultPermissionsAndRoles.CanAddWorkspace.Should().BeTrue();
        settings.NewUserDefaultPermissionsAndRoles.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task changing_default_permissions_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultPermissionsAndRoles(
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = false,
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
        await AssertAuditLogContains<AuditLogDetails.Settings.DefaultPermissionsChanged>(
            expectedEventType: AuditLogEventTypes.Settings.DefaultPermissionsChanged,
            assertDetails: details =>
            {
                details.IsAdmin.Should().BeFalse();
                details.Permissions.Should().Contain(Permissions.AddWorkspace);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task can_change_default_max_workspace_number()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultMaxWorkspaceNumber(
            value: 10,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.NewUserDefaultMaxWorkspaceNumber.Should().Be(10);
    }

    [Fact]
    public async Task changing_default_max_workspace_number_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultMaxWorkspaceNumber(
            value: 10,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.LimitUpdated>(
            expectedEventType: AuditLogEventTypes.Settings.DefaultMaxWorkspaceNumberChanged,
            assertDetails: details => details.Value.Should().Be(10),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_change_default_max_workspace_size()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultMaxWorkspaceSizeInBytes(
            value: 1024 * 1024 * 200,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.NewUserDefaultMaxWorkspaceSizeInBytes.Should().Be(1024 * 1024 * 200);
    }

    [Fact]
    public async Task changing_default_max_workspace_size_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultMaxWorkspaceSizeInBytes(
            value: 1024 * 1024 * 200,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.LimitUpdated>(
            expectedEventType: AuditLogEventTypes.Settings.DefaultMaxWorkspaceSizeChanged,
            assertDetails: details => details.Value.Should().Be(1024 * 1024 * 200),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_change_default_max_workspace_team_members()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultMaxWorkspaceTeamMembers(
            value: 15,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.NewUserDefaultMaxWorkspaceTeamMembers.Should().Be(15);
    }

    [Fact]
    public async Task changing_default_max_workspace_team_members_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetNewUserDefaultMaxWorkspaceTeamMembers(
            value: 15,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.User.LimitUpdated>(
            expectedEventType: AuditLogEventTypes.Settings.DefaultMaxWorkspaceTeamMembersChanged,
            assertDetails: details => details.Value.Should().Be(15),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_change_alert_on_new_user_registered()
    {
        //when
        await Api.GeneralSettings.SetAlertOnNewUserRegistered(
            isTurnedOn: true,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.AlertOnNewUserRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task changing_alert_on_new_user_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.SetAlertOnNewUserRegistered(
            isTurnedOn: true,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Settings.ToggleChanged>(
            expectedEventType: AuditLogEventTypes.Settings.AlertOnNewUserChanged,
            assertDetails: details => details.Value.Should().BeTrue(),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_create_sign_up_checkbox()
    {
        //when
        var response = await Api.GeneralSettings.CreateOrUpdateSignUpCheckbox(
            request: new CreateOrUpdateSignUpCheckboxRequestDto
            {
                Id = null,
                Text = "I agree to terms",
                IsRequired = true
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.NewId.Should().BeGreaterThan(0);

        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.SignUpCheckboxes.Should().Contain(c => c.Text == "I agree to terms" && c.IsRequired);
    }

    [Fact]
    public async Task creating_sign_up_checkbox_should_produce_audit_log_entry()
    {
        //when
        await Api.GeneralSettings.CreateOrUpdateSignUpCheckbox(
            request: new CreateOrUpdateSignUpCheckboxRequestDto
            {
                Id = null,
                Text = "I agree to audit terms",
                IsRequired = true
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Settings.SignUpCheckbox>(
            expectedEventType: AuditLogEventTypes.Settings.SignUpCheckboxCreatedOrUpdated,
            assertDetails: details =>
            {
                details.Text.Should().Be("I agree to audit terms");
                details.IsRequired.Should().BeTrue();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task can_delete_sign_up_checkbox()
    {
        //given
        var response = await Api.GeneralSettings.CreateOrUpdateSignUpCheckbox(
            request: new CreateOrUpdateSignUpCheckboxRequestDto
            {
                Id = null,
                Text = "Checkbox to delete",
                IsRequired = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.GeneralSettings.DeleteSignUpCheckbox(
            signUpCheckboxId: response.NewId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var settings = await Api.GeneralSettings.Get(cookie: AppOwner.Cookie);
        settings.SignUpCheckboxes.Should().NotContain(c => c.Text == "Checkbox to delete");
    }

    [Fact]
    public async Task deleting_sign_up_checkbox_should_produce_audit_log_entry()
    {
        //given
        var response = await Api.GeneralSettings.CreateOrUpdateSignUpCheckbox(
            request: new CreateOrUpdateSignUpCheckboxRequestDto
            {
                Id = null,
                Text = "Checkbox for audit delete",
                IsRequired = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.GeneralSettings.DeleteSignUpCheckbox(
            signUpCheckboxId: response.NewId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Settings.SignUpCheckbox>(
            expectedEventType: AuditLogEventTypes.Settings.SignUpCheckboxDeleted,
            assertDetails: details => details.Id.Should().Be(response.NewId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
