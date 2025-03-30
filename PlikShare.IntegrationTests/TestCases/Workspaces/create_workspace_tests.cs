using FluentAssertions;
using PlikShare.Dashboard.Content.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Get.Contracts;
using PlikShare.Workspaces.Permissions;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class create_workspace_tests(
    HostFixture8081 hostFixture, 
    ITestOutputHelper testOutputHelper) : TestFixture(hostFixture, testOutputHelper)
{
    [Fact]
    public async Task when_workspace_is_created_it_should_be_visible_on_the_dashboard()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        //when
        var workspaceResponse = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: user.Cookie);

        //then
        var dashboard = await Api.Dashboard.Get(
            cookie: user.Cookie);

        dashboard.Workspaces.Should().ContainEquivalentOf(new GetDashboardContentResponseDto.WorkspaceDetails
        {
            ExternalId = workspaceResponse.ExternalId.Value,
            CurrentSizeInBytes = 0,
            Name = "my first workspace",
            Owner = new GetDashboardContentResponseDto.User
            {
                ExternalId = user.ExternalId.Value,
                Email = user.Email,
            },
            Permissions = new GetDashboardContentResponseDto.WorkspacePermissions
            {
                AllowShare = true,
            },
            StorageName = hardDrive.Name,
            IsBucketCreated = false,
            IsUsedByIntegration = false,
            MaxSizeInBytes = -1,
        });
    }
    
    [Fact]
    public async Task when_workspace_is_created_its_details_should_be_available()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        //when
        var workspaceResponse = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: hardDrive.ExternalId,
                Name: "my first workspace"),
            cookie: user.Cookie);

        //then
        var details = await Api.Workspaces.GetDetails(
            externalId: workspaceResponse.ExternalId,
        cookie: user.Cookie);

        details.Should().BeEquivalentTo(new GetWorkspaceDetailsResponseDto
        {
            ExternalId = workspaceResponse.ExternalId,
            Name = "my first workspace",
            CurrentSizeInBytes = 0,
            Owner = new WorkspaceOwnerDto
            {
                ExternalId = user.ExternalId,
                Email = user.Email
            },
            PendingUploadsCount = 0,
            Permissions = new WorkspacePermissions(
                AllowShare: true),
            Integrations = new WorkspaceIntegrationsDto
            {
                ChatGpt = [],
                Textract = null
            },
            IsBucketCreated = false,
            MaxSizeInBytes = null
        });
    }
}