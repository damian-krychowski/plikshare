using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_create_folder_tests : TestFixture
{
    public mcp_create_folder_tests(
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
            ["create_folder", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task creates_a_top_level_folder_recording_the_agent_as_creator()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string folderName = "agent-created-folder";

        //when
        var result = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = folderName
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("name").GetString().Should().Be(folderName);
        result.GetProperty("result").GetProperty("folderExternalId").GetString().Should().StartWith("fo_");

        (await FolderExistsByName(mcp, workspace, folderName)).Should().BeTrue();

        var creator = GetFolderCreator(workspace.ExternalId.Value, folderName);
        creator.Should().NotBeNull();
        creator!.Value.IdentityType.Should().Be(AgentIdentity.Type);
        creator.Value.Identity.Should().Be(agent.ExternalId.Value);

        await AssertAuditLogContains(AuditLogEventTypes.Folder.Created);
    }

    [Fact]
    public async Task creating_under_an_unknown_parent_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_folder",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "child",
                ["parentFolderExternalId"] = FolderExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task creating_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_folder",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "child"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_creating()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFolderRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string folderName = "needs-approval";

        //when
        var pending = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = folderName
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await FolderExistsByName(mcp, workspace, folderName)).Should().BeFalse();
    }

    [Fact]
    public async Task approving_then_executing_creates_the_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFolderRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string folderName = "approved-folder";
        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, folderName);

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
        committed.GetProperty("result").GetProperty("name").GetString().Should().Be(folderName);

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();
        (await FolderExistsByName(mcp, workspace, folderName)).Should().BeTrue();
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_creating_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFolderRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, "approved-folder");

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

        //then — the same folder id comes back both times; the folder is created exactly once
        var folderExternalId = first.GetProperty("result").GetProperty("folderExternalId").GetString();

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("folderExternalId").GetString().Should().Be(folderExternalId);
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_create()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFolderRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string folderName = "denied-folder";
        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, folderName);

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

        (await FolderExistsByName(mcp, workspace, folderName)).Should().BeFalse();
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally create_folder runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFolderRequiresApproval: false);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "create_folder",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string folderName = "override-folder";

        //when
        var result = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = folderName
        });

        //then — the workspace override wins; nothing is created yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await FolderExistsByName(mcp, workspace, folderName)).Should().BeFalse();
    }

    [Fact]
    public async Task operation_details_resolve_the_new_folder_name_and_parent()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFolderRequiresApproval: true);
        var parent = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string folderName = "new-subfolder";

        var pending = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = folderName,
            ["parentFolderExternalId"] = parent.ExternalId.Value
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("create_folder");
        details.GetProperty("name").GetString().Should().Be(folderName);
        details.GetProperty("parentFolderExternalId").GetString().Should().Be(parent.ExternalId.Value);
        details.GetProperty("parentLocation").GetString().Should().Be(parent.Name);
    }

    private async Task<string> SubmitCreateForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string name)
    {
        var pending = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = name
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

    private static async Task<bool> FolderExistsByName(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string name)
    {
        var root = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        return root.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Any(e => e.GetProperty("type").GetString() == "folder"
                      && e.GetProperty("name").GetString() == name);
    }

    private (string IdentityType, string Identity)? GetFolderCreator(
        string workspaceExternalId,
        string folderName)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT fo_creator_identity_type, fo_creator_identity
                     FROM fo_folders
                     INNER JOIN w_workspaces ON w_id = fo_workspace_id
                     WHERE w_external_id = $workspaceExternalId
                         AND fo_name = $folderName
                     LIMIT 1
                     """,
                readRowFunc: reader => (
                    IdentityType: reader.GetString(0),
                    Identity: reader.GetString(1)))
            .WithParameter("$workspaceExternalId", workspaceExternalId)
            .WithParameter("$folderName", folderName)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool createFolderRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "create_folder",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = createFolderRequiresApproval
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
