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
public class mcp_box_links_tests : TestFixture
{
    public mcp_box_links_tests(
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
            ["list_box_links", "create_box_link", "update_box_link", "delete_box_link", "regenerate_box_link_access_code"]);
    }

    [Fact]
    public async Task creates_a_box_link_and_lists_it()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "create_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["name"] = "Public link"
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var externalId = result.GetProperty("result").GetProperty("externalId").GetString()!;
        externalId.Should().StartWith("bl_");
        result.GetProperty("result").GetProperty("accessCode").GetString().Should().NotBeNullOrEmpty();

        var links = await ListLinks(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        var link = links.Single(l => l.GetProperty("externalId").GetString() == externalId);
        link.GetProperty("permissions").GetProperty("allowList").GetBoolean().Should().BeTrue();
        link.GetProperty("permissions").GetProperty("allowDownload").GetBoolean().Should().BeFalse();

        await AssertAuditLogContains(AuditLogEventTypes.Box.LinkCreated);
    }

    [Fact]
    public async Task creating_a_link_on_a_box_in_another_workspace_returns_an_error()
    {
        //given — a box in a workspace the agent cannot access
        var (owner, workspace, _, agent) = await CreateOwnerWorkspaceBoxAgent();
        var otherBox = await CreateBox(owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — using the accessible workspace id but a foreign box
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_box_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["boxExternalId"] = otherBox.ExternalId.Value,
                ["name"] = "Nope"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task updates_a_box_link_permissions_merging_over_current()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkExternalId = await CreateLink(mcp, workspace.ExternalId.Value, box.ExternalId.Value, "Link");

        //when — flip only allowDownload; allowList must keep its current (true) value
        var result = await CallTool(mcp, "update_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxLinkExternalId"] = linkExternalId,
            ["allowDownload"] = true
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var links = await ListLinks(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        var link = links.Single(l => l.GetProperty("externalId").GetString() == linkExternalId);
        link.GetProperty("permissions").GetProperty("allowDownload").GetBoolean().Should().BeTrue();
        link.GetProperty("permissions").GetProperty("allowList").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task updates_a_box_link_name_and_enabled_state()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkExternalId = await CreateLink(mcp, workspace.ExternalId.Value, box.ExternalId.Value, "Before");

        //when
        var result = await CallTool(mcp, "update_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxLinkExternalId"] = linkExternalId,
            ["name"] = "After",
            ["isEnabled"] = false
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var links = await ListLinks(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        var link = links.Single(l => l.GetProperty("externalId").GetString() == linkExternalId);
        link.GetProperty("name").GetString().Should().Be("After");
        link.GetProperty("isEnabled").GetBoolean().Should().BeFalse();

        await AssertAuditLogContains(AuditLogEventTypes.BoxLink.NameUpdated);
        await AssertAuditLogContains(AuditLogEventTypes.BoxLink.IsEnabledUpdated);
    }

    [Fact]
    public async Task update_box_link_without_any_field_returns_an_error()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkExternalId = await CreateLink(mcp, workspace.ExternalId.Value, box.ExternalId.Value, "Link");

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_box_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["boxLinkExternalId"] = linkExternalId
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deletes_a_box_link()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var linkExternalId = await CreateLink(mcp, workspace.ExternalId.Value, box.ExternalId.Value, "Doomed");

        //when
        var result = await CallTool(mcp, "delete_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxLinkExternalId"] = linkExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var links = await ListLinks(mcp, workspace.ExternalId.Value, box.ExternalId.Value);
        links.Should().NotContain(l => l.GetProperty("externalId").GetString() == linkExternalId);

        await AssertAuditLogContains(AuditLogEventTypes.BoxLink.Deleted);
    }

    [Fact]
    public async Task regenerates_the_access_code()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var created = await CallTool(mcp, "create_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["name"] = "Link"
        });
        var linkExternalId = created.GetProperty("result").GetProperty("externalId").GetString()!;
        var originalAccessCode = created.GetProperty("result").GetProperty("accessCode").GetString()!;

        //when
        var result = await CallTool(mcp, "regenerate_box_link_access_code", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxLinkExternalId"] = linkExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var newAccessCode = result.GetProperty("result").GetProperty("accessCode").GetString()!;
        newAccessCode.Should().NotBe(originalAccessCode);

        await AssertAuditLogContains(AuditLogEventTypes.BoxLink.AccessCodeRegenerated);
    }

    [Fact]
    public async Task creating_a_box_link_waits_for_approval_when_required()
    {
        //given
        var (_, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent(createBoxLinkRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "create_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["name"] = "Pending"
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await ListLinks(mcp, workspace.ExternalId.Value, box.ExternalId.Value)).Should().BeEmpty();
    }

    [Fact]
    public async Task approving_then_executing_creates_the_box_link()
    {
        //given
        var (owner, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent(createBoxLinkRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "create_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["name"] = "Approved"
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
        (await ListLinks(mcp, workspace.ExternalId.Value, box.ExternalId.Value))
            .Should().ContainSingle(l => l.GetProperty("name").GetString() == "Approved");
    }

    [Fact]
    public async Task operation_details_resolve_the_link_name_and_box()
    {
        //given
        var (owner, workspace, box, agent) = await CreateOwnerWorkspaceBoxAgent(createBoxLinkRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "create_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = box.ExternalId.Value,
            ["name"] = "Detailed"
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("create_box_link");
        details.GetProperty("boxExternalId").GetString().Should().Be(box.ExternalId.Value);
        details.GetProperty("name").GetString().Should().Be("Detailed");
    }

    private async Task<string> CreateLink(
        McpAgentSession mcp,
        string workspaceExternalId,
        string boxExternalId,
        string name)
    {
        var result = await CallTool(mcp, "create_box_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["boxExternalId"] = boxExternalId,
            ["name"] = name
        });

        result.GetProperty("status").GetString().Should().Be("executed");
        return result.GetProperty("result").GetProperty("externalId").GetString()!;
    }

    private static async Task<List<JsonElement>> ListLinks(
        McpAgentSession mcp,
        string workspaceExternalId,
        string boxExternalId)
    {
        var result = await CallTool(mcp, "list_box_links", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["boxExternalId"] = boxExternalId
        });

        return result.GetProperty("result").GetProperty("links").EnumerateArray().ToList();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, AppBox Box, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceBoxAgent(
        bool grantWorkspaceAccess = true,
        bool createBoxLinkRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var folder = await CreateFolder(workspace, owner);
        var box = await CreateBox(folder, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await ConfigureTool(owner, agent, "list_box_links", requiresApproval: false);
        await ConfigureTool(owner, agent, "create_box_link", requiresApproval: createBoxLinkRequiresApproval);
        await ConfigureTool(owner, agent, "update_box_link", requiresApproval: false);
        await ConfigureTool(owner, agent, "delete_box_link", requiresApproval: false);
        await ConfigureTool(owner, agent, "regenerate_box_link_access_code", requiresApproval: false);

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
