using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.UpdateFooter.Contracts;
using PlikShare.Boxes.UpdateFooterIsEnabled.Contracts;
using PlikShare.Boxes.UpdateHeader.Contracts;
using PlikShare.Boxes.UpdateHeaderIsEnabled.Contracts;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases;

[Collection(IntegrationTestsCollection.Name)]
public class api_access_without_logging_in: TestFixture
{
    [Collection(IntegrationTestsCollection.Name)]
    public class Dashboard(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) 
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_dashboard_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Dashboard.Get(cookie: null)
            );
    
            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }

    [Collection(IntegrationTestsCollection.Name)]
    public class Account(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_account_details_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Account.GetDetails(cookie: null)
            );
    
            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }

    [Collection(IntegrationTestsCollection.Name)]
    public class GeneralSettings(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_general_settings_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.GeneralSettings.Get(cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }

    [Collection(IntegrationTestsCollection.Name)]
    public class Storages(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_storages_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Storages.Get(cookie: null)
            );
    
            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_storages_hard_drive_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Storages.CreateHardDriveStorage(
                    request: new CreateHardDriveStorageRequestDto(
                        Name: "some-name",
                        VolumePath: "some-volume-path",
                        FolderPath: "some-folder-path",
                        EncryptionType: StorageEncryptionType.None),
                    cookie: null)
            );
    
            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_storages_hard_drive_volumes_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Storages.GetHardDriveVolumes(
                    cookie: null)
            );
    
            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }

    [Collection(IntegrationTestsCollection.Name)]
    public class EmailProviders(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_email_providers_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.EmailProviders.Get(cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }

    [Collection(IntegrationTestsCollection.Name)]
    public class UsersApi(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_users_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Users.Get(cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }
    
    [Collection(IntegrationTestsCollection.Name)]
    public class Workspaces(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_workspaces_get_details_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Workspaces.GetDetails(
                    externalId: WorkspaceExtId.NewId(),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        
        [Fact]
        public async Task api_workspaces_create_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Workspaces.Create(
                    request: new CreateWorkspaceRequestDto(
                        StorageExternalId: StorageExtId.NewId(),
                        Name: "some-workspace-name"),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }
    
    [Collection(IntegrationTestsCollection.Name)]
    public class Folders(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_folders_create_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Folders.Create(
                    request: new CreateFolderRequestDto
                    {
                        ExternalId = FolderExtId.NewId(),
                        ParentExternalId = null,
                        Name = "some-folder-name"
                    },
                    workspaceExternalId: WorkspaceExtId.NewId(), 
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        
        [Fact]
        public async Task api_folders_get_top_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Folders.GetTop(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_folders_get_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Folders.Get(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    folderExternalId: FolderExtId.NewId(),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }
    
    [Collection(IntegrationTestsCollection.Name)]
    public class Boxes(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_boxes_create_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.Create(
                    request: new CreateBoxRequestDto(
                        FolderExternalId: FolderExtId.NewId(), 
                        Name: "some-folder-name"),
                    workspaceExternalId: WorkspaceExtId.NewId(), 
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        
        [Fact]
        public async Task api_boxes_get_list_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.GetList(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_boxes_get_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.Get(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxExternalId: BoxExtId.NewId(),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_boxes_update_header_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.UpdateHeader(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxExternalId: BoxExtId.NewId(),
                    request: new UpdateBoxHeaderRequestDto(
                        Json: "some-json",
                        Html: "some-html"),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_boxes_update_footer_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.UpdateFooter(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxExternalId: BoxExtId.NewId(),
                    request: new UpdateBoxFooterRequestDto(
                        Json: "some-json",
                        Html: "some-html"),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_boxes_update_header_is_enabled_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.UpdateHeaderIsEnabled(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxExternalId: BoxExtId.NewId(),
                    request: new UpdateBoxHeaderIsEnabledRequestDto(
                        IsEnabled: true),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_boxes_update_footer_is_enabled_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.UpdateFooterIsEnabled(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxExternalId: BoxExtId.NewId(),
                    request: new UpdateBoxFooterIsEnabledRequestDto(
                        IsEnabled: true),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
        
        [Fact]
        public async Task api_boxes_create_box_link_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.Boxes.CreateBoxLink(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxExternalId: BoxExtId.NewId(),
                    request: new CreateBoxLinkRequestDto(
                        Name: "some-box-link-name"),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }
    
    [Collection(IntegrationTestsCollection.Name)]
    public class BoxExternalAccess(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_box_get_html_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.BoxExternalAccess.GetHtml(
                    boxExternalId: BoxExtId.NewId(), 
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }
    
    [Collection(IntegrationTestsCollection.Name)]
    public class BoxLinks(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : TestFixture(hostFixture, testOutputHelper)
    {
        [Fact]
        public async Task api_box_links_update_permissions_should_return_401()
        {
            // when
            var apiError = await Assert.ThrowsAsync<TestApiCallException>(
                async () => await Api.BoxLinks.UpdatePermissions(
                    workspaceExternalId: WorkspaceExtId.NewId(),
                    boxLinkExternalId: BoxLinkExtId.NewId(), 
                    request: new UpdateBoxLinkPermissionsRequestDto(true, true,true, true, true, true, true, true, true),
                    cookie: null)
            );

            // then
            apiError.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        }
    }

    public api_access_without_logging_in(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
    }
}
