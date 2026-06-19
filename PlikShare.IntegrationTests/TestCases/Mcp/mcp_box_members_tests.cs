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
public class mcp_box_members_tests : TestFixture
{
    public mcp_box_members_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tools_are_discoverable()
    {
        var (_, _, _, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain(
            ["list_box_members", "invite_box_members", "update_box_member_permissions", "revoke_box_member"]);
    }

    [Fact]
    public async Task invites_a_member_and_lists_it()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var email = Random.Email();

        //when
        var result = await CallTool(mcp, "invite_box_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["memberEmails"] = new[] { email }
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("members").EnumerateArray()
            .Select(m => m.GetProperty("email").GetString())
            .Should().Contain(email.ToLowerInvariant());

        var members = await ListMembers(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        var member = members.Single(m => m.GetProperty("email").GetString() == email.ToLowerInvariant());
        member.GetProperty("invitationAccepted").GetBoolean().Should().BeFalse();
        member.GetProperty("permissions").GetProperty("allowList").GetBoolean().Should().BeTrue();

        await AssertAuditLogContains(AuditLogEventTypes.Box.MemberInvited);
    }

    [Fact]
    public async Task inviting_to_a_box_in_another_workspace_returns_an_error()
    {
        //given
        var (owner, workspace, _, agent) = await CreateOwnerWorkspaceBoxAgent();
        var otherBox = await CreateBox(owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "invite_box_members",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["boxExternalId"] = otherBox.ExternalId.Value,
                ["memberEmails"] = new[] { Random.Email() }
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task updates_a_member_permissions_merging_over_current()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var memberExternalId = await InviteMember(mcp, workspace.ExternalId.Value, box.ExternalId.Value, Random.Email());

        //when — flip only allowDownload; allowList must keep its current (true) value
        var result = await CallTool(mcp, "update_box_member_permissions", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["memberExternalId"] = memberExternalId,
            ["allowDownload"] = true
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var members = await ListMembers(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        var member = members.Single(m => m.GetProperty("memberExternalId").GetString() == memberExternalId);
        member.GetProperty("permissions").GetProperty("allowDownload").GetBoolean().Should().BeTrue();
        member.GetProperty("permissions").GetProperty("allowList").GetBoolean().Should().BeTrue();

        await AssertAuditLogContains(AuditLogEventTypes.Box.MemberPermissionsUpdated);
    }

    [Fact]
    public async Task update_member_permissions_without_any_flag_returns_an_error()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var memberExternalId = await InviteMember(mcp, workspace.ExternalId.Value, box.ExternalId.Value, Random.Email());

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_box_member_permissions",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["boxExternalId"] = box.ExternalId.Value,
                ["memberExternalId"] = memberExternalId
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task revokes_a_member()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var memberExternalId = await InviteMember(mcp, workspace.ExternalId.Value, box.ExternalId.Value, Random.Email());

        //when
        var result = await CallTool(mcp, "revoke_box_member", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["memberExternalId"] = memberExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var members = await ListMembers(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        members.Should().NotContain(m => m.GetProperty("memberExternalId").GetString() == memberExternalId);

        await AssertAuditLogContains(AuditLogEventTypes.Box.MemberRevoked);
    }

    [Fact]
    public async Task inviting_waits_for_approval_when_required()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent(inviteRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "invite_box_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["memberEmails"] = new[] { Random.Email() }
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await ListMembers(mcp, workspace.ExternalId.Value, box.ExternalId.Value)).Should().BeEmpty();
    }

    [Fact]
    public async Task approving_then_executing_runs_the_invite()
    {
        //given
        var (owner, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent(inviteRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var email = Random.Email();

        var pending = await CallTool(mcp, "invite_box_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["memberEmails"] = new[] { email }
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
        (await ListMembers(mcp, workspace.ExternalId.Value, box.ExternalId.Value))
            .Should().ContainSingle(m => m.GetProperty("email").GetString() == email.ToLowerInvariant());
    }

    [Fact]
    public async Task operation_details_resolve_the_invited_emails_and_box()
    {
        //given
        var (owner, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent(inviteRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var email = Random.Email();

        var pending = await CallTool(mcp, "invite_box_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["memberEmails"] = new[] { email }
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("invite_box_members");
        details.GetProperty("boxExternalId").GetString().Should().Be(box.ExternalId.Value);
        details.GetProperty("memberEmails").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(email);
    }

    private async Task<string> InviteMember(
        McpAgentSession mcp,
        string workspaceExternalId,
        string boxExternalId,
        string email)
    {
        var result = await CallTool(mcp, "invite_box_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["boxExternalId"] = boxExternalId,
            ["memberEmails"] = new[] { email }
        });

        result.GetProperty("status").GetString().Should().Be("executed");
        return result.GetProperty("result").GetProperty("members").EnumerateArray()
            .First().GetProperty("externalId").GetString()!;
    }

    private static async Task<List<JsonElement>> ListMembers(
        McpAgentSession mcp,
        string workspaceExternalId,
        string boxExternalId)
    {
        var result = await CallTool(mcp, "list_box_members", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["boxExternalId"] = boxExternalId
        });

        return result.GetProperty("result").GetProperty("members").EnumerateArray().ToList();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, AppBox Box, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceBoxAgent(
        bool grantWorkspaceAccess = true,
        bool inviteRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var folder = await CreateFolder(workspace, owner);
        var box = await CreateBox(folder, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await ConfigureTool(owner, agent, "list_box_members", requiresApproval: false);
        await ConfigureTool(owner, agent, "invite_box_members", requiresApproval: inviteRequiresApproval);
        await ConfigureTool(owner, agent, "update_box_member_permissions", requiresApproval: false);
        await ConfigureTool(owner, agent, "revoke_box_member", requiresApproval: false);

        if (grantWorkspaceAccess)
            await Api.Agents.GrantWorkspaceAccess(
                externalId: agent.ExternalId,
                workspaceExternalId: workspace.ExternalId,
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery);

        return (owner, workspace, box, agent);
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
