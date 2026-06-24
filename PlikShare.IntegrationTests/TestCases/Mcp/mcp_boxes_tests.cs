using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.Boxes.Id;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_boxes_tests : TestFixture
{
    public mcp_boxes_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tools_are_discoverable()
    {
        var (_, _, _, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain(
            ["list_workspace_boxes", "get_box", "create_box", "update_box", "delete_box"]);
    }

    [Fact]
    public async Task creates_a_box_and_lists_it()
    {
        //given
        var (_, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "create_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "Quarterly archive",
            ["folderExternalId"] = folder.ExternalId.Value
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var boxExternalId = result.GetProperty("result").GetProperty("boxExternalId").GetString()!;
        boxExternalId.Should().StartWith("bo_");

        var boxes = await ListBoxes(mcp, workspace.ExternalId.Value);
        boxes.Should().ContainSingle(b => b.GetProperty("externalId").GetString() == boxExternalId);

        await AssertAuditLogContains(AuditLogEventTypes.Box.Created);
    }

    [Fact]
    public async Task creating_a_box_on_an_unknown_folder_returns_an_error()
    {
        //given
        var (_, workspace, _, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_box",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "Box",
                ["folderExternalId"] = FolderExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task gets_a_box_details()
    {
        //given
        var (_, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var boxExternalId = await CreateBoxViaTool(mcp, workspace.ExternalId.Value, folder.ExternalId.Value, "My box");

        //when
        var result = await CallTool(mcp, "get_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = boxExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var box = result.GetProperty("result");
        box.GetProperty("externalId").GetString().Should().Be(boxExternalId);
        box.GetProperty("name").GetString().Should().Be("My box");
        box.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        box.GetProperty("membersCount").GetInt32().Should().Be(0);
        box.GetProperty("linksCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task updates_a_box_name_and_enabled_state()
    {
        //given
        var (_, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var boxExternalId = await CreateBoxViaTool(mcp, workspace.ExternalId.Value, folder.ExternalId.Value, "Before");

        //when
        var result = await CallTool(mcp, "update_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = boxExternalId,
            ["name"] = "After",
            ["isEnabled"] = false
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var boxes = await ListBoxes(mcp, workspace.ExternalId.Value);
        var box = boxes.Single(b => b.GetProperty("externalId").GetString() == boxExternalId);
        box.GetProperty("name").GetString().Should().Be("After");
        box.GetProperty("isEnabled").GetBoolean().Should().BeFalse();

        await AssertAuditLogContains(AuditLogEventTypes.Box.NameUpdated);
        await AssertAuditLogContains(AuditLogEventTypes.Box.IsEnabledUpdated);
    }

    [Fact]
    public async Task update_box_without_any_field_returns_an_error()
    {
        //given
        var (_, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var boxExternalId = await CreateBoxViaTool(mcp, workspace.ExternalId.Value, folder.ExternalId.Value, "Box");

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_box",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["boxExternalId"] = boxExternalId
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deletes_a_box()
    {
        //given
        var (_, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var boxExternalId = await CreateBoxViaTool(mcp, workspace.ExternalId.Value, folder.ExternalId.Value, "Doomed");

        //when
        var result = await CallTool(mcp, "delete_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["boxExternalId"] = boxExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");

        var boxes = await ListBoxes(mcp, workspace.ExternalId.Value);
        boxes.Should().NotContain(b => b.GetProperty("externalId").GetString() == boxExternalId);

        await AssertAuditLogContains(AuditLogEventTypes.Box.Deleted);
    }

    [Fact]
    public async Task box_in_another_workspace_is_not_accessible()
    {
        //given — a box created in a workspace the agent cannot access
        var (owner, _, _, agent) = await CreateOwnerWorkspaceFolderAgent();
        var otherBox = await CreateBox(owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_box",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = otherBox.WorkspaceExternalId.Value,
                ["boxExternalId"] = otherBox.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task creating_a_box_waits_for_approval_when_required()
    {
        //given
        var (_, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent(createBoxRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "create_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "Pending box",
            ["folderExternalId"] = folder.ExternalId.Value
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await ListBoxes(mcp, workspace.ExternalId.Value)).Should().BeEmpty();
    }

    [Fact]
    public async Task approving_then_executing_creates_the_box()
    {
        //given
        var (owner, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent(createBoxRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "create_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "Approved box",
            ["folderExternalId"] = folder.ExternalId.Value
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
        (await ListBoxes(mcp, workspace.ExternalId.Value))
            .Should().ContainSingle(b => b.GetProperty("name").GetString() == "Approved box");
    }

    [Fact]
    public async Task operation_details_resolve_the_box_name_and_folder()
    {
        //given
        var (owner, workspace, folder, agent) = await CreateOwnerWorkspaceFolderAgent(createBoxRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "create_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "Detailed box",
            ["folderExternalId"] = folder.ExternalId.Value
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("create_box");
        details.GetProperty("workspaceExternalId").GetString().Should().Be(workspace.ExternalId.Value);
        details.GetProperty("name").GetString().Should().Be("Detailed box");
        details.GetProperty("folderExternalId").GetString().Should().Be(folder.ExternalId.Value);
    }

    private async Task<string> CreateBoxViaTool(
        McpAgentSession mcp,
        string workspaceExternalId,
        string folderExternalId,
        string name)
    {
        var result = await CallTool(mcp, "create_box", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["name"] = name,
            ["folderExternalId"] = folderExternalId
        });

        result.GetProperty("status").GetString().Should().Be("executed");
        return result.GetProperty("result").GetProperty("boxExternalId").GetString()!;
    }

    private static async Task<List<JsonElement>> ListBoxes(
        McpAgentSession mcp,
        string workspaceExternalId)
    {
        var result = await CallTool(mcp, "list_workspace_boxes", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId
        });

        return result.GetProperty("result").GetProperty("boxes").EnumerateArray().ToList();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, AppFolder Folder, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceFolderAgent(
        bool grantWorkspaceAccess = true,
        bool createBoxRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var folder = await CreateFolder(workspace, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await ConfigureTool(owner, agent, "list_workspace_boxes", requiresApproval: false);
        await ConfigureTool(owner, agent, "get_box", requiresApproval: false);
        await ConfigureTool(owner, agent, "create_box", requiresApproval: createBoxRequiresApproval);
        await ConfigureTool(owner, agent, "update_box", requiresApproval: false);
        await ConfigureTool(owner, agent, "delete_box", requiresApproval: false);

        if (grantWorkspaceAccess)
            await Api.Agents.GrantWorkspaceAccess(
                externalId: agent.ExternalId,
                workspaceExternalId: workspace.ExternalId,
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery);

        return (owner, workspace, folder, agent);
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
