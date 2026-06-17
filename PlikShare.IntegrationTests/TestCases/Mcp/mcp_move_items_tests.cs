using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_move_items_tests : TestFixture
{
    public mcp_move_items_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tool_is_discoverable()
    {
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain("move_items");
    }

    [Fact]
    public async Task moves_a_file_into_a_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalIds"] = new[] { file.ExternalId.Value },
                ["destinationFolderExternalId"] = destination.ExternalId.Value
            });

        //then
        result.IsError.Should().NotBe(true);

        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        details.GetProperty("path").EnumerateArray()
            .Select(p => p.GetProperty("externalId").GetString())
            .Should().Equal(destination.ExternalId.Value);

        await AssertAuditLogContains(AuditLogEventTypes.Folder.ItemsMoved);
    }

    [Fact]
    public async Task moves_a_file_to_the_workspace_root()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalIds"] = new[] { file.ExternalId.Value }
            });

        //then
        result.IsError.Should().NotBe(true);

        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        details.GetProperty("path").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task moves_a_folder_with_its_contents_into_another_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("inside.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalIds"] = new[] { source.ExternalId.Value },
                ["destinationFolderExternalId"] = destination.ExternalId.Value
            });

        //then
        result.IsError.Should().NotBe(true);

        var destinationContent = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalId"] = destination.ExternalId.Value
        });

        destinationContent.GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().Contain(source.ExternalId.Value);

        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        details.GetProperty("path").EnumerateArray()
            .Select(p => p.GetProperty("externalId").GetString())
            .Should().BeEquivalentTo(new[] { destination.ExternalId.Value, source.ExternalId.Value });
    }

    [Fact]
    public async Task requires_at_least_one_id()
    {
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value
            });

        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task moving_to_an_unknown_destination_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalIds"] = new[] { file.ExternalId.Value },
                ["destinationFolderExternalId"] = FolderExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task moving_a_folder_into_its_own_subfolder_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var parent = await CreateFolder(workspace, owner);
        var child = await CreateFolder(parent: parent, workspace: workspace, user: owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalIds"] = new[] { parent.ExternalId.Value },
                ["destinationFolderExternalId"] = child.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task moving_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalIds"] = new[] { file.ExternalId.Value },
                ["destinationFolderExternalId"] = destination.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        if (grantWorkspaceAccess)
            await Api.Agents.GrantWorkspaceAccess(
                externalId: agent.ExternalId,
                workspaceExternalId: workspace.ExternalId,
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery);

        return (owner, workspace, agent);
    }

    private Task<AppFile> UploadTextFile(
        string fileName,
        AppFolder folder,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        return UploadFile(
            content: Encoding.UTF8.GetBytes($"content of {fileName}"),
            fileName: fileName,
            contentType: "text/markdown",
            folder: folder,
            workspace: workspace,
            user: user);
    }

    private static async Task<JsonElement> CallTool(
        McpAgentSession mcp,
        string toolName,
        Dictionary<string, object?> arguments)
    {
        var result = await mcp.Client.CallToolAsync(
            toolName: toolName,
            arguments: arguments);

        result.IsError.Should().NotBe(true);

        var text = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();

        text.Should().NotBeNullOrEmpty($"{toolName} should return its result as JSON content");

        using var document = JsonDocument.Parse(text!);
        return document.RootElement.Clone();
    }
}
