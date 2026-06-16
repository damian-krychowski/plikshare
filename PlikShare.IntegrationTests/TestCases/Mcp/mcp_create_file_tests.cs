using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_create_file_tests : TestFixture
{
    public mcp_create_file_tests(
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

        tools.Select(t => t.Name).Should().Contain("create_file");
    }

    [Fact]
    public async Task creates_a_file_whose_content_can_be_read_back()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string content = "Hello from the agent. zażółć gęślą jaźń";

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "note.txt",
            ["content"] = content
        });

        //then
        var fileId = created.GetProperty("fileExternalId").GetString();
        fileId.Should().NotBeNullOrEmpty();

        var read = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileId
        });

        read.GetProperty("content").GetString().Should().Be(content);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.FileCreated);
    }

    [Fact]
    public async Task creates_a_file_inside_a_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "in-folder.txt",
            ["content"] = "x",
            ["folderExternalId"] = folder.ExternalId.Value
        });

        //then
        var fileId = created.GetProperty("fileExternalId").GetString();

        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileId
        });

        details.GetProperty("path").EnumerateArray()
            .Select(p => p.GetProperty("externalId").GetString())
            .Should().Contain(folder.ExternalId.Value);
    }

    [Fact]
    public async Task derives_content_type_from_the_extension()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "doc.md",
            ["content"] = "# title"
        });

        //then
        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = created.GetProperty("fileExternalId").GetString()
        });

        details.GetProperty("contentType").GetString().Should().Be("text/markdown");
    }

    [Fact]
    public async Task uses_an_explicit_content_type_when_given()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "data.txt",
            ["content"] = "{}",
            ["contentType"] = "application/json"
        });

        //then
        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = created.GetProperty("fileExternalId").GetString()
        });

        details.GetProperty("contentType").GetString().Should().Be("application/json");
    }

    [Fact]
    public async Task creates_an_encrypted_file_in_a_managed_workspace()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerManagedWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string content = "secret content, encrypted at rest";

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "secret.txt",
            ["content"] = content
        });

        //then — written encrypted, read back decrypted
        var read = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = created.GetProperty("fileExternalId").GetString()
        });

        read.GetProperty("content").GetString().Should().Be(content);
    }

    [Fact]
    public async Task creating_in_an_unknown_folder_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_file",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "x.txt",
                ["content"] = "x",
                ["folderExternalId"] = FolderExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task creating_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_file",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "x.txt",
                ["content"] = "x"
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        await WaitForBucketReady(workspace, owner);

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

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerManagedWorkspaceAgent()
    {
        var owner = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(owner, StorageEncryptionType.Managed);
        var workspace = await CreateWorkspace(storage, owner);
        await WaitForBucketReady(workspace, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        return (owner, workspace, agent);
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
