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
public class mcp_workspace_members_tests : TestFixture
{
    public mcp_workspace_members_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tools_are_discoverable()
    {
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain(
        [
            "list_workspace_members",
            "invite_workspace_members",
            "update_workspace_member_permissions",
            "revoke_workspace_member"
        ]);
    }

    [Fact]
    public async Task invites_a_member_and_lists_it()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var email = Random.Email();

        //when
        var result = await CallTool(mcp, "invite_workspace_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["memberEmails"] = new[] { email },
            ["allowShare"] = false
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("members").EnumerateArray()
            .Select(m => m.GetProperty("email").GetString())
            .Should().Contain(email.ToLowerInvariant());

        var members = await ListMembers(mcp, workspace.ExternalId.Value);
        members.Should().ContainSingle(m => m.GetProperty("email").GetString() == email.ToLowerInvariant());

        await AssertAuditLogContains(AuditLogEventTypes.Workspace.MemberInvited);
    }

    [Fact]
    public async Task inviting_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "invite_workspace_members",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["memberEmails"] = new[] { Random.Email() },
                ["allowShare"] = false
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task inviting_without_any_email_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "invite_workspace_members",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["memberEmails"] = Array.Empty<string>(),
                ["allowShare"] = false
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task updates_a_member_permissions()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var memberExternalId = await InviteMember(mcp, workspace.ExternalId.Value, Random.Email());

        //when
        var result = await CallTool(mcp, "update_workspace_member_permissions", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["memberExternalId"] = memberExternalId,
            ["allowShare"] = true
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var members = await ListMembers(mcp, workspace.ExternalId.Value);
        members.Single(m => m.GetProperty("memberExternalId").GetString() == memberExternalId)
            .GetProperty("allowShare").GetBoolean().Should().BeTrue();

        await AssertAuditLogContains(AuditLogEventTypes.Workspace.MemberPermissionsUpdated);
    }

    [Fact]
    public async Task revokes_a_member()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var memberExternalId = await InviteMember(mcp, workspace.ExternalId.Value, Random.Email());

        //when
        var result = await CallTool(mcp, "revoke_workspace_member", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["memberExternalId"] = memberExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var members = await ListMembers(mcp, workspace.ExternalId.Value);
        members.Should().NotContain(m => m.GetProperty("memberExternalId").GetString() == memberExternalId);

        await AssertAuditLogContains(AuditLogEventTypes.Workspace.MemberRevoked);
    }

    [Fact]
    public async Task inviting_waits_for_approval_when_required()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(inviteRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "invite_workspace_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["memberEmails"] = new[] { Random.Email() },
            ["allowShare"] = false
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await ListMembers(mcp, workspace.ExternalId.Value)).Should().BeEmpty();
    }

    [Fact]
    public async Task approving_then_executing_runs_the_invite()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(inviteRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var email = Random.Email();

        var pending = await CallTool(mcp, "invite_workspace_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["memberEmails"] = new[] { email },
            ["allowShare"] = false
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

        var members = await ListMembers(mcp, workspace.ExternalId.Value);
        members.Should().ContainSingle(m => m.GetProperty("email").GetString() == email.ToLowerInvariant());
    }

    [Fact]
    public async Task operation_details_resolve_the_invited_emails()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(inviteRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var email = Random.Email();

        var pending = await CallTool(mcp, "invite_workspace_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["memberEmails"] = new[] { email },
            ["allowShare"] = true
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("invite_workspace_members");
        details.GetProperty("workspaceExternalId").GetString().Should().Be(workspace.ExternalId.Value);
        details.GetProperty("workspaceName").GetString().Should().Be(workspace.Name);
        details.GetProperty("allowShare").GetBoolean().Should().BeTrue();
        details.GetProperty("memberEmails").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(email);
    }

    private async Task<string> InviteMember(
        McpAgentSession mcp,
        string workspaceExternalId,
        string email)
    {
        var result = await CallTool(mcp, "invite_workspace_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["memberEmails"] = new[] { email },
            ["allowShare"] = false
        });

        result.GetProperty("status").GetString().Should().Be("executed");

        return result.GetProperty("result").GetProperty("members").EnumerateArray()
            .First().GetProperty("externalId").GetString()!;
    }

    private static async Task<List<JsonElement>> ListMembers(
        McpAgentSession mcp,
        string workspaceExternalId)
    {
        var result = await CallTool(mcp, "list_workspace_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId
        });

        return result.GetProperty("result").GetProperty("members").EnumerateArray().ToList();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool inviteRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await ConfigureTool(owner, agent, "list_workspace_members", requiresApproval: false);
        await ConfigureTool(owner, agent, "invite_workspace_members", requiresApproval: inviteRequiresApproval);
        await ConfigureTool(owner, agent, "update_workspace_member_permissions", requiresApproval: false);
        await ConfigureTool(owner, agent, "revoke_workspace_member", requiresApproval: false);

        if (grantWorkspaceAccess)
            await Api.Agents.GrantWorkspaceAccess(
                externalId: agent.ExternalId,
                workspaceExternalId: workspace.ExternalId,
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery);

        return (owner, workspace, agent);
    }

    private Task ConfigureTool(
        AppSignedInUser owner,
        CreateAgentResponseDto agent,
        string toolName,
        bool requiresApproval)
    {
        return Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: toolName,
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = requiresApproval
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);
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
