using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_link_external_access_tests: TestFixture
{
    [Fact]
    public async Task can_get_box_link_content_anonymously_with_its_access_code()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var workspace = await CreateWorkspace(
            user: user);

        var boxFolder = await CreateFolder(
            workspace: workspace,
            user: user);

        var subFolderA = await CreateFolder(
            parent: boxFolder,
            workspace: workspace,
            user: user);

        var subFolderB = await CreateFolder(
            parent: boxFolder,
            workspace: workspace,
            user: user);
        
        var subFolderC = await CreateFolder(
            parent: boxFolder,
            workspace: workspace,
            user: user);

        var box = await CreateBox(
            folder: boxFolder,
            user: user);

        var boxLink = await CreateBoxLink(
            box: box,
            user: user);

        //when
        var boxLinkSession = await StartBoxLinkSession();

        var boxContent = await Api.AccessCodesApi.GetBoxDetailsAndContent(
            accessCode: boxLink.AccessCode,
            boxLinkToken: boxLinkSession.Token);

        //then
        boxContent.Should().BeEquivalentTo(new GetBoxDetailsAndContentResponseDto
        {
            Details = new BoxDetailsDto
            {
                Name = null,
                OwnerEmail = null,
                WorkspaceExternalId = null,
                IsTurnedOn = true,

                AllowList = true,

                AllowCreateFolder = false,
                AllowDeleteFile = false,
                AllowDeleteFolder = false,
                AllowDownload = false,
                AllowMoveItems = false,
                AllowRenameFile = false,
                AllowRenameFolder = false,
                AllowUpload = false
            },
            Files = null,
            Folder = null,
            Uploads = null,
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = subFolderA.ExternalId.Value,
                    Name = subFolderA.Name,
                    WasCreatedByUser = false,
                    CreatedAt = null
                },
                new SubfolderDto
                {
                    ExternalId = subFolderB.ExternalId.Value,
                    Name = subFolderB.Name,
                    WasCreatedByUser = false,
                    CreatedAt = null
                },
                new SubfolderDto
                {
                    ExternalId = subFolderC.ExternalId.Value,
                    Name = subFolderC.Name,
                    WasCreatedByUser = false,
                    CreatedAt = null
                },
            ]
        });
    }
    
    [Fact]
    public async Task when_box_link_allows_for_folder_creation_it_should_be_possible_to_create_a_folder_anonymously()
    {
        //given
        var now = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(now);
        
        var user = await SignIn(
            user: Users.AppOwner);

        var boxLink = await CreateBoxLink(
            user: user);

        await Api.BoxLinks.UpdatePermissions(
            workspaceExternalId: boxLink.WorkspaceExternalId,
            boxLinkExternalId: boxLink.ExternalId,
            request: new UpdateBoxLinkPermissionsRequestDto(
                AllowList: true,
                AllowCreateFolder: true),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //when
        var boxLinkSession = await StartBoxLinkSession();

        var folder = await Api.AccessCodesApi.CreateFolder(
            accessCode: boxLink.AccessCode,
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my new box folder",
            },
            boxLinkToken: boxLinkSession.Token);
       
        //then
        var boxContent = await Api.AccessCodesApi.GetBoxDetailsAndContent(
            accessCode: boxLink.AccessCode,
            boxLinkToken: boxLinkSession.Token);
        
        boxContent.Should().BeEquivalentTo(new GetBoxDetailsAndContentResponseDto
        {
            Details = new BoxDetailsDto
            {
                Name = null,
                OwnerEmail = null,
                WorkspaceExternalId = null,
                IsTurnedOn = true,

                AllowList = true,
                AllowCreateFolder = true,

                AllowDeleteFile = false,
                AllowDeleteFolder = false,
                AllowDownload = false,
                AllowMoveItems = false,
                AllowRenameFile = false,
                AllowRenameFolder = false,
                AllowUpload = false
            },
            Files = null,
            Folder = null,
            Uploads = null,
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folder.ExternalId.Value,
                    Name = "my new box folder",
                    WasCreatedByUser = true,
                    CreatedAt = now.DateTime
                }
            ]
        });
    }
    
    [Fact]
    public async Task anonymous_user_who_created_the_folder_should_be_allowed_to_rename_it_even_though_rename_folder_permission_is_not_given_before_5min_period_elapses()
    {
        //given
        var createdAtTime = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(createdAtTime);
        
        var user = await SignIn(
            user: Users.AppOwner);

        var boxLink = await CreateBoxLink(
            permissions: new (
                AllowList: true,
                AllowCreateFolder: true,
                AllowRenameFolder: false),
            user: user);

        var boxLinkSession = await StartBoxLinkSession();

        var folder = await Api.AccessCodesApi.CreateFolder(
            accessCode: boxLink.AccessCode,
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId= null,
                Name= "my new box folder", 
            },
            boxLinkToken: boxLinkSession.Token);

        //when
        var littleLater = createdAtTime.AddMinutes(3);
        Clock.CurrentTime(littleLater);
        
        await Api.AccessCodesApi.UpdateFolderName(
            accessCode: boxLink.AccessCode,
            folderExternalId: folder.ExternalId,
            request: new UpdateBoxFolderNameRequestDto(
                Name: "new name for my box folder"),
            boxLinkToken: boxLinkSession.Token);
        
        //then
        var boxContent = await Api.AccessCodesApi.GetBoxDetailsAndContent(
            accessCode: boxLink.AccessCode,
            boxLinkToken: boxLinkSession.Token);

        boxContent.Subfolders.Should().BeEquivalentTo(
        [
            new SubfolderDto
            {
                ExternalId = folder.ExternalId.Value,
                Name = "new name for my box folder",
                WasCreatedByUser = true,
                CreatedAt = createdAtTime.DateTime
            }
        ]);
    }
    
    [Fact]
    public async Task anonymous_user_who_created_the_folder_should_not_be_allowed_to_rename_it_after_5min_period_elapses()
    {
        //given
        var createdAtTime = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(createdAtTime);
        
        var user = await SignIn(
            user: Users.AppOwner);

        var boxLink = await CreateBoxLink(
            permissions: new (
                AllowList: true,
                AllowCreateFolder: true,
                AllowRenameFolder: false),
            user: user);

        var boxLinkSession = await StartBoxLinkSession();

        var folder = await Api.AccessCodesApi.CreateFolder(
            accessCode: boxLink.AccessCode,
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId= null,
                Name = "my new box folder",
            },
            boxLinkToken: boxLinkSession.Token);

        //when
        var tooLate = createdAtTime.AddMinutes(5).AddSeconds(1);
        Clock.CurrentTime(tooLate);
        
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.AccessCodesApi.UpdateFolderName(
                accessCode: boxLink.AccessCode,
                folderExternalId: folder.ExternalId,
                request: new UpdateBoxFolderNameRequestDto(
                    Name: "new name for my box folder"),
                boxLinkToken: boxLinkSession.Token)
        );
        
        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
    
    public box_link_external_access_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    { 
    }
}