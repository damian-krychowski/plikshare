using FluentAssertions;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.List.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class create_box_tests: TestFixture
{
    [Fact]
    public async Task when_box_is_created_its_visible_on_the_workspace_boxes_list()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            user: user);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            user: user);

        var folder = await CreateFolder(
            workspace: workspace,
            user: user);
        
        //when
        var box = await Api.Boxes.Create(
            workspaceExternalId: workspace.ExternalId,
            request: new CreateBoxRequestDto(
                Name: "my first box",
                FolderExternalId: folder.ExternalId),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxes = await Api.Boxes.GetList(
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        boxes.Should().BeEquivalentTo(new GetBoxesResponseDto
        {
            Items = [
            
                new GetBoxesResponseDto.Box
                {
                    ExternalId = box.ExternalId,
                    IsEnabled = true,
                    Name = "my first box",
                    FolderPath = [
                        new GetBoxesResponseDto.FolderItem
                        {
                            Name = folder.Name,
                            ExternalId = folder.ExternalId.Value
                        }
                    ]
                }
            ]
        });
    }
    
    [Fact]
    public async Task when_box_is_created_it_should_bet_possible_to_open_it()
    {
        //given
        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            user: user);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            user: user);

        var folder = await CreateFolder(
            workspace: workspace,
            user: user);
        
        //when
        var box = await Api.Boxes.Create(
            workspaceExternalId: workspace.ExternalId,
            request: new CreateBoxRequestDto(
                Name: "my first box",
                FolderExternalId: folder.ExternalId),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: workspace.ExternalId,
            boxExternalId: box.ExternalId,
            cookie: user.Cookie);

        boxContent.Should().BeEquivalentTo(new GetBoxResponseDto
        {
            Details = new GetBoxResponseDto.BoxDetails
            {
                ExternalId = box.ExternalId.Value,
                FolderPath = [
                    new GetBoxResponseDto.FolderItem
                    {
                        Name = folder.Name,
                        ExternalId = folder.ExternalId.Value
                    }
                ],
                IsEnabled = true,
                Name = "my first box",
                Footer = new GetBoxResponseDto.Section
                {
                    IsEnabled = false,
                    Json = null
                },
                Header = new GetBoxResponseDto.Section
                {
                    IsEnabled = false,
                    Json = null
                }
            },
            Files = [],
            Links = [],
            Members = [],
            Subfolders = []
        });
    }
    
    public create_box_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    { 
    }
}