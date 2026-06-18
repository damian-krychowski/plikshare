using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Workspaces.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_rename_workspace_tests : TestFixture
{
    public mcp_rename_workspace_tests(
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

        tools.Select(t => t.Name).Should().Contain(
            ["rename_workspace", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task renames_a_workspace_the_agent_can_access()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "rename_workspace", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "renamed workspace"
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("workspaceExternalId").GetString().Should().Be(workspace.ExternalId.Value);
        result.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed workspace");

        (await WorkspaceName(mcp, workspace.ExternalId.Value)).Should().Be("renamed workspace");

        await AssertAuditLogContains(AuditLogEventTypes.Workspace.NameUpdated);
    }

    [Fact]
    public async Task renaming_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "renamed"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task renaming_an_unknown_workspace_returns_an_error()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = WorkspaceExtId.NewId().Value,
                ["name"] = "renamed"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_renaming()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "rename_workspace", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "renamed workspace"
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await WorkspaceName(mcp, workspace.ExternalId.Value)).Should().Be(workspace.Name);
    }

    [Fact]
    public async Task approving_then_executing_runs_the_rename()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, "renamed workspace");

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
        committed.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed workspace");

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();
        (await WorkspaceName(mcp, workspace.ExternalId.Value)).Should().Be("renamed workspace");
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_renaming_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, "renamed workspace");

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

        //then
        first.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed workspace");

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed workspace");
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_rename()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, "renamed workspace");

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

        (await WorkspaceName(mcp, workspace.ExternalId.Value)).Should().Be(workspace.Name);
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally rename_workspace runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameWorkspaceRequiresApproval: false);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "rename_workspace",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "rename_workspace", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "renamed workspace"
        });

        //then — the workspace override wins; nothing is renamed yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await WorkspaceName(mcp, workspace.ExternalId.Value)).Should().Be(workspace.Name);
    }

    [Fact]
    public async Task operation_details_resolve_the_current_and_new_workspace_name()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, "quarterly archive");

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("rename_workspace");
        details.GetProperty("workspaceExternalId").GetString().Should().Be(workspace.ExternalId.Value);
        details.GetProperty("currentName").GetString().Should().Be(workspace.Name);
        details.GetProperty("newName").GetString().Should().Be("quarterly archive");
    }

    private async Task<string> SubmitRenameForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string newName)
    {
        var pending = await CallTool(mcp, "rename_workspace", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = newName
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

    private static async Task<string?> WorkspaceName(McpAgentSession mcp, string workspaceExternalId)
    {
        var workspaces = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        return workspaces.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Where(w => w.GetProperty("workspaceExternalId").GetString() == workspaceExternalId)
            .Select(w => w.GetProperty("name").GetString())
            .FirstOrDefault();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool renameWorkspaceRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "rename_workspace",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = renameWorkspaceRequiresApproval
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
