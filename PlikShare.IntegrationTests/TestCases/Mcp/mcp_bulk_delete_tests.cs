using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_bulk_delete_tests : TestFixture
{
    public mcp_bulk_delete_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tool_is_discoverable()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var tools = await mcp.Client.ListToolsAsync();

        //then
        tools.Select(t => t.Name).Should().Contain("bulk_delete");
    }

    [Fact]
    public async Task deletes_a_folder_with_its_whole_subtree()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderA = await CreateFolder(workspace, owner);
        var folderB = await CreateFolder(parent: folderA, workspace: workspace, user: owner);
        await UploadTextFile("a.txt", folderA, workspace, owner);
        await UploadTextFile("b.txt", folderA, workspace, owner);
        await UploadTextFile("c.txt", folderB, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folderA.ExternalId.Value }
        });

        //then
        json.GetProperty("deletedFileCount").GetInt32().Should().Be(3);
        json.GetProperty("deletedSizeInBytes").GetInt64().Should().BeGreaterThan(0);

        var root = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        root.GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().NotContain(folderA.ExternalId.Value);
    }

    [Fact]
    public async Task deletes_individual_files()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file1 = await UploadTextFile("a.txt", folder, workspace, owner);
        var file2 = await UploadTextFile("b.txt", folder, workspace, owner);
        var keptFile = await UploadTextFile("keep.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file1.ExternalId.Value, file2.ExternalId.Value }
        });

        //then
        json.GetProperty("deletedFileCount").GetInt32().Should().Be(2);

        var content = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalId"] = folder.ExternalId.Value
        });

        content.GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().Equal(keptFile.ExternalId.Value);
    }

    [Fact]
    public async Task deletes_folders_and_files_together()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderToDelete = await CreateFolder(workspace, owner);
        await UploadTextFile("inside.txt", folderToDelete, workspace, owner);

        var otherFolder = await CreateFolder(workspace, owner);
        var fileToDelete = await UploadTextFile("file-to-delete.txt", otherFolder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folderToDelete.ExternalId.Value },
            ["fileExternalIds"] = new[] { fileToDelete.ExternalId.Value }
        });

        //then
        json.GetProperty("deletedFileCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task requires_at_least_one_id()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "bulk_delete",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deleting_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "bulk_delete",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalIds"] = new[] { folder.ExternalId.Value }
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deleting_writes_an_audit_log_entry()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folder.ExternalId.Value }
        });

        //then
        await AssertAuditLogContains(AuditLogEventTypes.Workspace.BulkDeleteRequested);
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
