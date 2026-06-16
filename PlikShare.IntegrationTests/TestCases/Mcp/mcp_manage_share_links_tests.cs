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
public class mcp_manage_share_links_tests : TestFixture
{
    public mcp_manage_share_links_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tools_are_discoverable()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var tools = (await mcp.Client.ListToolsAsync()).Select(t => t.Name).ToList();

        //then
        tools.Should().Contain("list_share_links");
        tools.Should().Contain("get_share_link");
        tools.Should().Contain("update_share_link");
        tools.Should().Contain("delete_share_link");
    }

    [Fact]
    public async Task lists_all_share_links_of_the_workspace()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var fileA = await UploadTextFile("a.txt", folder, workspace, owner);
        var fileB = await UploadTextFile("b.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkA = await CreateShareLink(mcp, workspace, "link a", fileExternalIds: [fileA.ExternalId.Value]);
        var linkB = await CreateShareLink(mcp, workspace, "link b", fileExternalIds: [fileB.ExternalId.Value]);

        //when
        var json = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        //then
        var links = json.GetProperty("shareLinks").EnumerateArray().ToList();

        links.Select(l => l.GetProperty("externalId").GetString())
            .Should().Contain(new[] { linkA, linkB });

        var entryA = links.Single(l => l.GetProperty("externalId").GetString() == linkA);
        entryA.GetProperty("name").GetString().Should().Be("link a");
        entryA.GetProperty("url").GetString().Should().Contain("/share/");
        entryA.GetProperty("selectedFilesCount").GetInt32().Should().Be(1);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.ShareLinksListed);
    }

    [Fact]
    public async Task gets_the_details_of_a_share_link()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("keep.txt", folder, workspace, owner);
        var excludedFile = await UploadTextFile("secret.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkId = await CreateShareLink(
            mcp,
            workspace,
            "folder minus secret",
            folderExternalIds: [folder.ExternalId.Value],
            excludedFileExternalIds: [excludedFile.ExternalId.Value]);

        //when
        var json = await CallTool(mcp, "get_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });

        //then
        json.GetProperty("externalId").GetString().Should().Be(linkId);
        json.GetProperty("name").GetString().Should().Be("folder minus secret");
        json.GetProperty("createdByAgentExternalId").GetString().Should().Be(agent.ExternalId.Value);

        json.GetProperty("selectedFolderExternalIds").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain(folder.ExternalId.Value);
        json.GetProperty("excludedFileExternalIds").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain(excludedFile.ExternalId.Value);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.ShareLinkViewed);
    }

    [Fact]
    public async Task deletes_a_share_link()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkId = await CreateShareLink(mcp, workspace, "to delete", fileExternalIds: [file.ExternalId.Value]);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "delete_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = linkId
            });

        //then
        result.IsError.Should().NotBe(true);

        var afterDelete = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        afterDelete.GetProperty("shareLinks").EnumerateArray()
            .Select(l => l.GetProperty("externalId").GetString())
            .Should().NotContain(linkId);

        await AssertAuditLogContains(AuditLogEventTypes.QuickShare.Deleted);
    }

    [Fact]
    public async Task getting_an_unknown_share_link_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = "qs_does_not_exist"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deleting_an_unknown_share_link_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "delete_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = "qs_does_not_exist"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task listing_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "list_share_links",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<string> CreateShareLink(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string name,
        string[]? fileExternalIds = null,
        string[]? folderExternalIds = null,
        string[]? excludedFileExternalIds = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = name
        };

        if (fileExternalIds is not null)
            arguments["fileExternalIds"] = fileExternalIds;

        if (folderExternalIds is not null)
            arguments["folderExternalIds"] = folderExternalIds;

        if (excludedFileExternalIds is not null)
            arguments["excludedFileExternalIds"] = excludedFileExternalIds;

        var json = await CallTool(mcp, "create_share_link", arguments);

        return json.GetProperty("externalId").GetString()!;
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
