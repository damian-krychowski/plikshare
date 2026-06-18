using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
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
        var links = json.GetProperty("result").GetProperty("shareLinks").EnumerateArray().ToList();

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
        var json = (await CallTool(mcp, "get_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        })).GetProperty("result");

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
        var result = await CallTool(mcp, "delete_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("externalId").GetString().Should().Be(linkId);
        result.GetProperty("result").GetProperty("name").GetString().Should().Be("to delete");

        var afterDelete = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        afterDelete.GetProperty("result").GetProperty("shareLinks").EnumerateArray()
            .Select(l => l.GetProperty("externalId").GetString())
            .Should().NotContain(linkId);

        await AssertAuditLogContains(AuditLogEventTypes.QuickShare.Deleted);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_deleting()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(deleteShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "to delete", fileExternalIds: [file.ExternalId.Value]);

        //when
        var pending = await CallTool(mcp, "delete_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        await AssertShareLinkStillExists(mcp, workspace, linkId);
    }

    [Fact]
    public async Task approving_then_executing_runs_the_deletion()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(deleteShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "to delete", fileExternalIds: [file.ExternalId.Value]);

        var approvalRequestId = await SubmitDeleteForApproval(mcp, workspace, linkId);

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
        committed.GetProperty("result").GetProperty("externalId").GetString().Should().Be(linkId);

        // Once executed it drops off the agent's approvals list.
        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();

        var afterDelete = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        afterDelete.GetProperty("result").GetProperty("shareLinks").EnumerateArray()
            .Select(l => l.GetProperty("externalId").GetString())
            .Should().NotContain(linkId);
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_deleting_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(deleteShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "to delete", fileExternalIds: [file.ExternalId.Value]);

        var approvalRequestId = await SubmitDeleteForApproval(mcp, workspace, linkId);

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

        //then — the second call returns the stored result rather than re-running the (now impossible) delete
        first.GetProperty("result").GetProperty("externalId").GetString().Should().Be(linkId);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("externalId").GetString().Should().Be(linkId);
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_delete()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(deleteShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "to delete", fileExternalIds: [file.ExternalId.Value]);

        var approvalRequestId = await SubmitDeleteForApproval(mcp, workspace, linkId);

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

        await AssertShareLinkStillExists(mcp, workspace, linkId);
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally delete_share_link runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(deleteShareLinkRequiresApproval: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "to delete", fileExternalIds: [file.ExternalId.Value]);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "delete_share_link",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        //when
        var result = await CallTool(mcp, "delete_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });

        //then — the workspace override wins; nothing is deleted yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        await AssertShareLinkStillExists(mcp, workspace, linkId);
    }

    [Fact]
    public async Task operation_details_resolve_the_share_link_name()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(deleteShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "quarterly report", fileExternalIds: [file.ExternalId.Value]);

        var approvalRequestId = await SubmitDeleteForApproval(mcp, workspace, linkId);

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("delete_share_link");
        details.GetProperty("externalId").GetString().Should().Be(linkId);
        details.GetProperty("name").GetString().Should().Be("quarterly report");
    }

    [Fact]
    public async Task list_share_links_when_approval_required_the_call_waits()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listShareLinksRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        await CreateShareLink(mcp, workspace, "link", fileExternalIds: [file.ExternalId.Value]);

        //when
        var pending = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");
    }

    [Fact]
    public async Task list_share_links_approving_then_executing_returns_the_links()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listShareLinksRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "link", fileExternalIds: [file.ExternalId.Value]);

        var pending = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then
        committed.GetProperty("status").GetString().Should().Be("executed");
        committed.GetProperty("result").GetProperty("shareLinks").EnumerateArray()
            .Select(l => l.GetProperty("externalId").GetString())
            .Should().Contain(linkId);
    }

    [Fact]
    public async Task list_share_links_operation_details_carry_the_discriminator()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listShareLinksRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("list_share_links");
    }

    [Fact]
    public async Task list_share_links_workspace_override_can_require_approval()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listShareLinksRequiresApproval: false);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "list_share_links",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        //then — the workspace override wins
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
    }

    [Fact]
    public async Task get_share_link_when_approval_required_the_call_waits()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "link", fileExternalIds: [file.ExternalId.Value]);

        //when
        var pending = await CallTool(mcp, "get_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");
    }

    [Fact]
    public async Task get_share_link_approving_then_executing_returns_the_details()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "quarterly", fileExternalIds: [file.ExternalId.Value]);

        var pending = await CallTool(mcp, "get_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then
        committed.GetProperty("status").GetString().Should().Be("executed");
        committed.GetProperty("result").GetProperty("externalId").GetString().Should().Be(linkId);
        committed.GetProperty("result").GetProperty("name").GetString().Should().Be("quarterly");
    }

    [Fact]
    public async Task get_share_link_operation_details_resolve_the_name()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getShareLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkId = await CreateShareLink(mcp, workspace, "quarterly report", fileExternalIds: [file.ExternalId.Value]);

        var pending = await CallTool(mcp, "get_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = linkId
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("get_share_link");
        details.GetProperty("shareLinkExternalId").GetString().Should().Be(linkId);
        details.GetProperty("name").GetString().Should().Be("quarterly report");
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

    private async Task<string> SubmitDeleteForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string shareLinkExternalId)
    {
        var pending = await CallTool(mcp, "delete_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = shareLinkExternalId
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

    private static async Task AssertShareLinkStillExists(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string shareLinkExternalId)
    {
        var links = await CallTool(mcp, "list_share_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        links.GetProperty("result").GetProperty("shareLinks").EnumerateArray()
            .Select(l => l.GetProperty("externalId").GetString())
            .Should().Contain(shareLinkExternalId);
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

        return json.GetProperty("result").GetProperty("externalId").GetString()!;
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool deleteShareLinkRequiresApproval = false,
        bool listShareLinksRequiresApproval = false,
        bool getShareLinkRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        // delete_share_link is destructive — it defaults to requiring approval, so pin it explicitly.
        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "delete_share_link",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = deleteShareLinkRequiresApproval
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "list_share_links",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = listShareLinksRequiresApproval
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "get_share_link",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = getShareLinkRequiresApproval
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
