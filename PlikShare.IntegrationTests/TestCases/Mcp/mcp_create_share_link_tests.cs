using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.QuickShares.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_create_share_link_tests : TestFixture
{
    public mcp_create_share_link_tests(
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
        tools.Select(t => t.Name).Should().Contain("create_share_link");
    }

    [Fact]
    public async Task creates_a_public_link_that_is_listed_with_creators_and_openable_by_anyone()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "shared docs",
            ["fileExternalIds"] = new[] { file.ExternalId.Value }
        });

        var shareExternalId = new QuickShareExtId(json.GetProperty("externalId").GetString()!);

        //then — it shows up on the workspace quick-share list
        var list = await Api.QuickShares.GetList(
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie);

        list.Items.Should().Contain(item => item.ExternalId == shareExternalId);

        //and — the detail (through the real cache + endpoint) records the human owner and the agent
        var detail = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: shareExternalId,
            cookie: owner.Cookie);

        detail.Name.Should().Be("shared docs");
        detail.CreatorExternalId.Should().Be(owner.ExternalId);
        detail.CreatorAgentExternalId.Should().Be(agent.ExternalId);

        //and — an anonymous visitor can open it via the public slug
        var info = await Api.QuickShareExternalAccess.GetInfo(slug: detail.Slug);

        info.Info.Should().NotBeNull();
        info.Info!.Name.Should().Be("shared docs");
        info.Info.RequiresPassword.Should().BeFalse();
        info.Info.IsUnlocked.Should().BeTrue();
    }

    [Fact]
    public async Task shares_a_folder_excluding_items_hides_them_from_the_recipient()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var keptFile = await UploadTextFile("keep.txt", folder, workspace, owner);
        var excludedFile = await UploadTextFile("secret.txt", folder, workspace, owner);
        var excludedSubfolder = await CreateFolder(parent: folder, workspace: workspace, user: owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "folder minus secret",
            ["folderExternalIds"] = new[] { folder.ExternalId.Value },
            ["excludedFileExternalIds"] = new[] { excludedFile.ExternalId.Value },
            ["excludedFolderExternalIds"] = new[] { excludedSubfolder.ExternalId.Value }
        });

        var shareExternalId = new QuickShareExtId(json.GetProperty("externalId").GetString()!);

        //then — the detail reflects the selection and exclusions
        var detail = await Api.QuickShares.Get(
            workspaceExternalId: workspace.ExternalId,
            quickShareExternalId: shareExternalId,
            cookie: owner.Cookie);

        detail.Items.SelectedFolders.Should().Contain(folder.ExternalId);
        detail.Items.ExcludedFiles.Should().Contain(excludedFile.ExternalId);
        detail.Items.ExcludedFolders.Should().Contain(excludedSubfolder.ExternalId);

        //and — the recipient sees the kept file but neither excluded item
        var content = await Api.QuickShareExternalAccess.GetContent(slug: detail.Slug);

        content.Files.Select(f => f.ExternalId).Should().Contain(keptFile.ExternalId);
        content.Files.Select(f => f.ExternalId).Should().NotContain(excludedFile.ExternalId);
        content.Folders.Select(f => f.ExternalId).Should().NotContain(excludedSubfolder.ExternalId);
    }

    [Fact]
    public async Task requires_at_least_one_item()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "empty"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task creating_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "nope",
                ["fileExternalIds"] = new[] { file.ExternalId.Value }
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task creating_writes_an_audit_log_entry()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "shared docs",
            ["fileExternalIds"] = new[] { file.ExternalId.Value }
        });

        //then
        await AssertAuditLogContains(AuditLogEventTypes.QuickShare.Created);
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
