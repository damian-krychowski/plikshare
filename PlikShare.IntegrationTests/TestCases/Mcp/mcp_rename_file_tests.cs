using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_rename_file_tests : TestFixture
{
    public mcp_rename_file_tests(
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
            ["rename_file", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task renames_the_file_keeping_its_extension()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "rename_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalId"] = file.ExternalId.Value,
            ["name"] = "renamed-report"
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("fileExternalId").GetString().Should().Be(file.ExternalId.Value);
        result.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed-report");

        var json = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        json.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed-report");
        json.GetProperty("result").GetProperty("extension").GetString().Should().Contain("txt");

        await AssertAuditLogContains(AuditLogEventTypes.File.Renamed);
    }

    [Fact]
    public async Task renaming_a_file_the_agent_cannot_access_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("secret.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_file",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalId"] = file.ExternalId.Value,
                ["name"] = "renamed"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task renaming_an_unknown_file_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_file",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["fileExternalId"] = FileExtId.NewId().Value,
                ["name"] = "renamed"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_renaming()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "rename_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalId"] = file.ExternalId.Value,
            ["name"] = "renamed-report"
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await FileName(mcp, file.ExternalId.Value)).Should().Be("report");
    }

    [Fact]
    public async Task approving_then_executing_runs_the_rename()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, file, "renamed-report");

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
        committed.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed-report");

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();
        (await FileName(mcp, file.ExternalId.Value)).Should().Be("renamed-report");
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_renaming_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, file, "renamed-report");

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
        first.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed-report");

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("name").GetString().Should().Be("renamed-report");
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_rename()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, file, "renamed-report");

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

        (await FileName(mcp, file.ExternalId.Value)).Should().Be("report");
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally rename_file runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameFileRequiresApproval: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "rename_file",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "rename_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalId"] = file.ExternalId.Value,
            ["name"] = "renamed-report"
        });

        //then — the workspace override wins; nothing is renamed yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await FileName(mcp, file.ExternalId.Value)).Should().Be("report");
    }

    [Fact]
    public async Task operation_details_resolve_the_current_and_new_file_name()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(renameFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitRenameForApproval(mcp, workspace, file, "quarterly-budget");

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("rename_file");
        details.GetProperty("fileExternalId").GetString().Should().Be(file.ExternalId.Value);
        details.GetProperty("folderExternalId").GetString().Should().Be(folder.ExternalId.Value);
        details.GetProperty("currentName").GetString().Should().Be("report.txt");
        details.GetProperty("newName").GetString().Should().Be("quarterly-budget.txt");
    }

    private async Task<string> SubmitRenameForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        AppFile file,
        string newName)
    {
        var pending = await CallTool(mcp, "rename_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalId"] = file.ExternalId.Value,
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

    private static async Task<string?> FileName(McpAgentSession mcp, string fileExternalId)
    {
        var json = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileExternalId
        });

        return json.GetProperty("result").GetProperty("name").GetString();
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool renameFileRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "rename_file",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = renameFileRequiresApproval
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
