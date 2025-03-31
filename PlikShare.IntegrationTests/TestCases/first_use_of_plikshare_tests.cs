using FluentAssertions;
using PlikShare.Core.ExternalIds;
using PlikShare.Dashboard.Content.Contracts;
using PlikShare.EmailProviders.List.Contracts;
using PlikShare.GeneralSettings;
using PlikShare.GeneralSettings.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.HardDrive.GetVolumes.Contracts;
using PlikShare.Storages.List.Contracts;
using PlikShare.Users.Entities;
using PlikShare.Users.List.Contracts;
using Xunit.Abstractions;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace PlikShare.IntegrationTests.TestCases;

public class first_use_of_plikshare_tests : TestFixture, IClassFixture<HostFixture8082>
{
    [Fact]
    public async Task initially_dashboard_should_be_entirely_empty()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        // when
        var dashboardResponse = await Api.Dashboard.Get(
            cookie: user.Cookie);

        // then
        dashboardResponse.Should().BeEquivalentTo(new GetDashboardContentResponseDto
        {
            Boxes = null,
            Storages = null,
            Workspaces = null,
            BoxInvitations = null,
            OtherWorkspaces = null,
            WorkspaceInvitations = null
        });
    }

    [Fact]
    public async Task initially_general_settings_should_be_empty()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        // when
        var generalSettingsResponse = await Api.GeneralSettings.Get(
            cookie: user.Cookie);

        // then
        generalSettingsResponse.Should().BeEquivalentTo(new GetApplicationSettingsResponse
        {
            ApplicationSignUp = AppSettings.SignUpSetting.OnlyInvitedUsers.Value,
            TermsOfService = null,
            ApplicationName = AppSettings.ApplicationNameSetting.Default.Name,
            PrivacyPolicy = null,
            SignUpCheckboxes = []
        });
    }

    [Fact]
    public async Task initially_no_storage_should_be_configured()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        // when
        var storagesResponse = await Api.Storages.Get(
            cookie: user.Cookie);

        // then
        storagesResponse.Should().BeEquivalentTo(new GetStoragesResponseDto
        {
            Items = []
        });
    }

    [Fact]
    public async Task initially_no_email_provider_should_be_configured()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        // when
        var emailProvidersResponse = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        // then
        emailProvidersResponse.Should().BeEquivalentTo(new GetEmailProvidersResponseDto(
            Items: []));
    }

    [Fact]
    public async Task initially_the_only_users_should_be_app_owners()
    {
        //given
        var user = await SignIn(Users.AppOwner);

        // when
        var usersResponseDto = await Api.Users.Get(
            cookie: user.Cookie);

        // then
        usersResponseDto.Should().BeEquivalentTo(new GetUsersResponseDto
        {
            Items =
            [
                 new GetUsersItemDto
                 {
                     ExternalId = user.ExternalId,
                     Email = user.Email,
                     IsEmailConfirmed = true,
                     WorkspacesCount = 0,
                     Roles = new GetUserItemRolesDto
                     {

                         IsAppOwner = true,
                         IsAdmin = false
                     },
                     Permissions = new GetUserItemPermissionsDto
                     {

                         CanAddWorkspace = false,
                         CanManageGeneralSettings = false,
                         CanManageUsers = false,
                         CanManageStorages = false,
                         CanManageEmailProviders = false

                     },
                     MaxWorkspaceNumber = null,
                     DefaultMaxWorkspaceSizeInBytes = null
                 }
            ]
        });
    }


    [Fact]
    public async Task initially_main_hard_drive_volume_should_be_available_in_the_list()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);
        
        // when
        var volumes = await Api.Storages.GetHardDriveVolumes(
            cookie: user.Cookie);
    
        // then
        volumes.Should().BeEquivalentTo(new GetHardDriveVolumesResponseDto(
            Items:
            [
                new HardDriveVolumeItemDto(
                    Path: MainVolume.Path,
                    RestrictedFolderPaths: [
                        $"{MainVolume.Path}/sqlite",
                        $"{MainVolume.Path}/legal"
                    ])
            ]));
    }
    
    public first_use_of_plikshare_tests(HostFixture8082 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}