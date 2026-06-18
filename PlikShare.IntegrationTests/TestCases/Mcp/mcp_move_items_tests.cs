using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_move_items_tests : TestFixture
{
    public mcp_move_items_tests(
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
            ["move_items", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task moves_a_file_into_a_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file.ExternalId.Value },
            ["destinationFolderExternalId"] = destination.ExternalId.Value
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("movedFileCount").GetInt32().Should().Be(1);
        result.GetProperty("result").GetProperty("destinationFolderExternalId").GetString().Should().Be(destination.ExternalId.Value);

        (await FilePath(mcp, file.ExternalId.Value)).Should().Equal(destination.ExternalId.Value);

        await AssertAuditLogContains(AuditLogEventTypes.Folder.ItemsMoved);
    }

    [Fact]
    public async Task moves_a_file_to_the_workspace_root()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file.ExternalId.Value }
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("movedFileCount").GetInt32().Should().Be(1);

        (await FilePath(mcp, file.ExternalId.Value)).Should().BeEmpty();
    }

    [Fact]
    public async Task moves_a_folder_with_its_contents_into_another_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("inside.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { source.ExternalId.Value },
            ["destinationFolderExternalId"] = destination.ExternalId.Value
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("movedFolderCount").GetInt32().Should().Be(1);

        var destinationContent = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalId"] = destination.ExternalId.Value
        });

        destinationContent.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().Contain(source.ExternalId.Value);

        (await FilePath(mcp, file.ExternalId.Value))
            .Should().BeEquivalentTo(new[] { destination.ExternalId.Value, source.ExternalId.Value });
    }

    [Fact]
    public async Task requires_at_least_one_id()
    {
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value
            });

        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task moving_to_an_unknown_destination_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalIds"] = new[] { file.ExternalId.Value },
                ["destinationFolderExternalId"] = FolderExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task moving_a_folder_into_its_own_subfolder_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var parent = await CreateFolder(workspace, owner);
        var child = await CreateFolder(parent: parent, workspace: workspace, user: owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalIds"] = new[] { parent.ExternalId.Value },
                ["destinationFolderExternalId"] = child.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task moving_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "move_items",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalIds"] = new[] { file.ExternalId.Value },
                ["destinationFolderExternalId"] = destination.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_moving()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(moveItemsRequiresApproval: true);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file.ExternalId.Value },
            ["destinationFolderExternalId"] = destination.ExternalId.Value
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await FilePath(mcp, file.ExternalId.Value)).Should().Equal(source.ExternalId.Value);
    }

    [Fact]
    public async Task approving_then_executing_runs_the_move()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(moveItemsRequiresApproval: true);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitMoveForApproval(mcp, workspace, file, destination);

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
        committed.GetProperty("result").GetProperty("movedFileCount").GetInt32().Should().Be(1);

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();
        (await FilePath(mcp, file.ExternalId.Value)).Should().Equal(destination.ExternalId.Value);
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_moving_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(moveItemsRequiresApproval: true);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitMoveForApproval(mcp, workspace, file, destination);

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

        //then — the second call returns the stored result rather than re-running the (now impossible) move
        first.GetProperty("result").GetProperty("movedFileCount").GetInt32().Should().Be(1);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("movedFileCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_move()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(moveItemsRequiresApproval: true);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitMoveForApproval(mcp, workspace, file, destination);

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

        (await FilePath(mcp, file.ExternalId.Value)).Should().Equal(source.ExternalId.Value);
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally move_items runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(moveItemsRequiresApproval: false);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "move_items",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file.ExternalId.Value },
            ["destinationFolderExternalId"] = destination.ExternalId.Value
        });

        //then — the workspace override wins; nothing is moved yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await FilePath(mcp, file.ExternalId.Value)).Should().Equal(source.ExternalId.Value);
    }

    [Fact]
    public async Task operation_details_resolve_items_and_destination()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(moveItemsRequiresApproval: true);
        var source = await CreateFolder(workspace, owner);
        var destination = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("note.txt", source, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { source.ExternalId.Value },
            ["fileExternalIds"] = new[] { file.ExternalId.Value },
            ["destinationFolderExternalId"] = destination.ExternalId.Value
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("move_items");
        details.GetProperty("destinationFolderExternalId").GetString().Should().Be(destination.ExternalId.Value);
        details.GetProperty("destinationName").GetString().Should().Be(destination.Name);

        details.GetProperty("folders").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .Should().Contain(source.Name);

        details.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .Should().Contain("note.txt");
    }

    private async Task<string> SubmitMoveForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        AppFile file,
        AppFolder destination)
    {
        var pending = await CallTool(mcp, "move_items", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file.ExternalId.Value },
            ["destinationFolderExternalId"] = destination.ExternalId.Value
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

    private static async Task<List<string?>> FilePath(McpAgentSession mcp, string fileExternalId)
    {
        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileExternalId
        });

        return details.GetProperty("result").GetProperty("path").EnumerateArray()
            .Select(p => p.GetProperty("externalId").GetString())
            .ToList();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool moveItemsRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "move_items",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = moveItemsRequiresApproval
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
