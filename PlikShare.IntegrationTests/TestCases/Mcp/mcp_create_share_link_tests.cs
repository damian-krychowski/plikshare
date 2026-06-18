using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
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
        tools.Select(t => t.Name).Should().Contain(
            ["create_share_link", "execute_operation", "check_approvals"]);
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

        //then
        json.GetProperty("status").GetString().Should().Be("executed");
        var shareExternalId = new QuickShareExtId(json.GetProperty("result").GetProperty("externalId").GetString()!);

        //and — it shows up on the workspace quick-share list
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

        var shareExternalId = new QuickShareExtId(json.GetProperty("result").GetProperty("externalId").GetString()!);

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

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_creating()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "pending share",
            ["fileExternalIds"] = new[] { file.ExternalId.Value }
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await ShareLinkCount(workspace, owner)).Should().Be(0);
    }

    [Fact]
    public async Task approving_then_executing_creates_the_share_link()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, file.ExternalId.Value);

        //when
        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var approvedStatus = await OperationStatus(mcp, approvalRequestId);

        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then
        approvedStatus.Should().Be("approved");
        committed.GetProperty("status").GetString().Should().Be("executed");

        var shareExternalId = new QuickShareExtId(committed.GetProperty("result").GetProperty("externalId").GetString()!);

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();

        var list = await Api.QuickShares.GetList(
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie);

        list.Items.Should().Contain(item => item.ExternalId == shareExternalId);
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_creating_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, file.ExternalId.Value);

        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //when
        var first = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        var second = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then — the same link id comes back both times; the link is created exactly once
        var externalId = first.GetProperty("result").GetProperty("externalId").GetString();

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("externalId").GetString().Should().Be(externalId);

        (await ShareLinkCount(workspace, owner)).Should().Be(1);
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_create()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, file.ExternalId.Value);

        //when
        await Api.Agents.DenyOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var deniedStatus = await OperationStatus(mcp, approvalRequestId);

        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then
        deniedStatus.Should().Be("denied");
        committed.GetProperty("status").GetString().Should().Be("rejected");
        committed.GetProperty("reason").GetString().Should().Be("denied");

        (await ShareLinkCount(workspace, owner)).Should().Be(0);
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally create_share_link runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createShareLinkRequiresApproval: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "create_share_link",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "override share",
            ["fileExternalIds"] = new[] { file.ExternalId.Value }
        });

        //then — the workspace override wins; nothing is created yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await ShareLinkCount(workspace, owner)).Should().Be(0);
    }

    [Fact]
    public async Task operation_details_resolve_shared_items_and_settings()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "quarterly share",
            ["folderExternalIds"] = new[] { folder.ExternalId.Value },
            ["fileExternalIds"] = new[] { file.ExternalId.Value },
            ["maxDownloads"] = 5,
            ["password"] = "s3cret"
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("create_share_link");
        details.GetProperty("name").GetString().Should().Be("quarterly share");

        details.GetProperty("sharedFolders").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .Should().Contain(folder.Name);

        details.GetProperty("sharedFiles").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .Should().Contain("doc.txt");

        details.GetProperty("maxDownloads").GetInt32().Should().Be(5);
        details.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
    }

    private async Task<string> SubmitCreateForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string fileExternalId)
    {
        var pending = await CallTool(mcp, "create_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "pending share",
            ["fileExternalIds"] = new[] { fileExternalId }
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        return pending.GetProperty("approvalRequestId").GetString()!;
    }

    // The agent's own view of an operation's status via check_approvals — or null when it is not
    // (or no longer) listed: executed, purged, or belonging to another agent.
    private static async Task<string?> OperationStatus(McpAgentSession mcp, string approvalRequestId)
    {
        var approvals = await CallTool(mcp, "check_approvals", new Dictionary<string, object?>());

        return approvals.GetProperty("approvals").EnumerateArray()
            .Where(a => a.GetProperty("approvalRequestId").GetString() == approvalRequestId)
            .Select(a => a.GetProperty("status").GetString())
            .FirstOrDefault();
    }

    private async Task<int> ShareLinkCount(AppWorkspace workspace, AppSignedInUser owner)
    {
        var list = await Api.QuickShares.GetList(
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie);

        return list.Items.Count;
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool createShareLinkRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "create_share_link",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = createShareLinkRequiresApproval
            },
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
