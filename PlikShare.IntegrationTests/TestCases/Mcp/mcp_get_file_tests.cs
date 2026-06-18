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
public class mcp_get_file_tests : TestFixture
{
    public mcp_get_file_tests(
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
            ["get_file", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task returns_file_details_with_its_folder_path()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderA = await CreateFolder(workspace, owner);
        var folderB = await CreateFolder(parent: folderA, workspace: workspace, user: owner);
        var file = await UploadTextFile("doc.txt", folderB, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        json.GetProperty("status").GetString().Should().Be("executed");
        var result = json.GetProperty("result");
        result.GetProperty("externalId").GetString().Should().Be(file.ExternalId.Value);
        result.GetProperty("contentType").GetString().Should().Be("text/markdown");
        result.GetProperty("extension").GetString().Should().Contain("txt");
        result.GetProperty("sizeInBytes").GetInt64().Should().BeGreaterThan(0);

        result.GetProperty("path").EnumerateArray()
            .Select(p => p.GetProperty("externalId").GetString())
            .Should().Equal(folderA.ExternalId.Value, folderB.ExternalId.Value);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.FileViewed);
    }

    [Fact]
    public async Task a_file_the_agent_cannot_access_is_reported_as_not_found()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("secret.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_file",
            arguments: new Dictionary<string, object?>
            {
                ["fileExternalId"] = file.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task an_unknown_file_is_reported_as_not_found()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_file",
            arguments: new Dictionary<string, object?>
            {
                ["fileExternalId"] = FileExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_reading()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");
    }

    [Fact]
    public async Task approving_then_executing_returns_the_details()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitGetForApproval(mcp, file.ExternalId.Value);

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
        committed.GetProperty("result").GetProperty("externalId").GetString().Should().Be(file.ExternalId.Value);
    }

    [Fact]
    public async Task executing_twice_re_reads_the_details()
    {
        //given — an idempotent read is not persisted, so each execute re-reads
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitGetForApproval(mcp, file.ExternalId.Value);

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
        first.GetProperty("result").GetProperty("externalId").GetString().Should().Be(file.ExternalId.Value);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("externalId").GetString().Should().Be(file.ExternalId.Value);
    }

    [Fact]
    public async Task denying_rejects_the_execution()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitGetForApproval(mcp, file.ExternalId.Value);

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
    public async Task operation_details_resolve_the_file_name()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("budget.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitGetForApproval(mcp, file.ExternalId.Value);

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("get_file");
        details.GetProperty("fileExternalId").GetString().Should().Be(file.ExternalId.Value);
        details.GetProperty("name").GetString().Should().Be("budget.txt");
    }

    [Fact]
    public async Task workspace_override_can_require_approval_for_files_in_that_workspace()
    {
        //given — globally get_file runs immediately, but the file's workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "get_file",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — the workspace is resolved from the file id, so the override applies
        var result = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
    }

    [Fact]
    public async Task workspace_override_can_disable_the_tool_for_files_in_that_workspace()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "get_file",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_file",
            arguments: new Dictionary<string, object?>
            {
                ["fileExternalId"] = file.ExternalId.Value
            });

        //then — disabled for this workspace
        result.IsError.Should().Be(true);
    }

    private async Task<string> SubmitGetForApproval(McpAgentSession mcp, string fileExternalId)
    {
        var pending = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileExternalId
        });

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
        bool grantWorkspaceAccess = true,
        bool getFileRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "get_file",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = getFileRequiresApproval
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
