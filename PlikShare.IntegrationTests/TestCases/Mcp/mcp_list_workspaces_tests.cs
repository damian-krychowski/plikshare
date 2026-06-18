using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_list_workspaces_tests : TestFixture
{
    public mcp_list_workspaces_tests(
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
            ["list_workspaces", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task lists_accessible_workspace_with_its_current_size()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);

        await UploadFile(
            content: Encoding.UTF8.GetBytes("hello world content for size"),
            fileName: "f.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        //then
        json.GetProperty("status").GetString().Should().Be("executed");

        var entry = json.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Single(w => w.GetProperty("workspaceExternalId").GetString() == workspace.ExternalId.Value);

        entry.GetProperty("name").GetString().Should().Be(workspace.Name);
        entry.GetProperty("currentSizeInBytes").GetInt64().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_listing()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent(listWorkspacesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");
    }

    [Fact]
    public async Task approving_then_executing_returns_the_workspaces()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listWorkspacesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

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

        committed.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Select(w => w.GetProperty("workspaceExternalId").GetString())
            .Should().Contain(workspace.ExternalId.Value);
    }

    [Fact]
    public async Task executing_twice_re_lists()
    {
        //given — an idempotent read is not persisted, so each execute re-lists
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listWorkspacesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

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
        first.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Select(w => w.GetProperty("workspaceExternalId").GetString())
            .Should().Contain(workspace.ExternalId.Value);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Select(w => w.GetProperty("workspaceExternalId").GetString())
            .Should().Contain(workspace.ExternalId.Value);
    }

    [Fact]
    public async Task denying_rejects_the_execution()
    {
        //given
        var (owner, _, agent) = await CreateOwnerWorkspaceAgent(listWorkspacesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

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
    }

    [Fact]
    public async Task operation_details_carry_the_discriminator()
    {
        //given
        var (owner, _, agent) = await CreateOwnerWorkspaceAgent(listWorkspacesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("list_workspaces");
    }

    private async Task<string> SubmitListForApproval(McpAgentSession mcp)
    {
        var pending = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        return pending.GetProperty("approvalRequestId").GetString()!;
    }

    private static async Task<string?> OperationStatus(McpAgentSession mcp, string approvalRequestId)
    {
        var approvals = await CallTool(mcp, "check_approvals", new Dictionary<string, object?>());

        return approvals.GetProperty("approvals").EnumerateArray()
            .Where(a => a.GetProperty("approvalRequestId").GetString() == approvalRequestId)
            .Select(a => a.GetProperty("status").GetString())
            .FirstOrDefault();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool listWorkspacesRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "list_workspaces",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = listWorkspacesRequiresApproval
            },
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
