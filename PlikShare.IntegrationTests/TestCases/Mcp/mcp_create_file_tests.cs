using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_create_file_tests : TestFixture
{
    public mcp_create_file_tests(
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
            ["create_file", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task creates_a_file_whose_content_can_be_read_back()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string content = "Hello from the agent. zażółć gęślą jaźń";

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "note.txt",
            ["content"] = content
        });

        //then
        created.GetProperty("status").GetString().Should().Be("executed");
        var fileId = created.GetProperty("result").GetProperty("fileExternalId").GetString();
        fileId.Should().NotBeNullOrEmpty();

        var read = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileId
        });

        read.GetProperty("result").GetProperty("content").GetString().Should().Be(content);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.FileCreated);
    }

    [Fact]
    public async Task creates_a_file_inside_a_folder()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "in-folder.txt",
            ["content"] = "x",
            ["folderExternalId"] = folder.ExternalId.Value
        });

        //then
        var fileId = created.GetProperty("result").GetProperty("fileExternalId").GetString();

        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileId
        });

        details.GetProperty("result").GetProperty("path").EnumerateArray()
            .Select(p => p.GetProperty("externalId").GetString())
            .Should().Contain(folder.ExternalId.Value);
    }

    [Fact]
    public async Task derives_content_type_from_the_extension()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "doc.md",
            ["content"] = "# title"
        });

        //then
        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = created.GetProperty("result").GetProperty("fileExternalId").GetString()
        });

        details.GetProperty("result").GetProperty("contentType").GetString().Should().Be("text/markdown");
    }

    [Fact]
    public async Task uses_an_explicit_content_type_when_given()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "data.txt",
            ["content"] = "{}",
            ["contentType"] = "application/json"
        });

        //then
        var details = await CallTool(mcp, "get_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = created.GetProperty("result").GetProperty("fileExternalId").GetString()
        });

        details.GetProperty("result").GetProperty("contentType").GetString().Should().Be("application/json");
    }

    [Fact]
    public async Task creates_an_encrypted_file_in_a_managed_workspace()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerManagedWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string content = "secret content, encrypted at rest";

        //when
        var created = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "secret.txt",
            ["content"] = content
        });

        //then — written encrypted, read back decrypted
        var read = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = created.GetProperty("result").GetProperty("fileExternalId").GetString()
        });

        read.GetProperty("result").GetProperty("content").GetString().Should().Be(content);
    }

    [Fact]
    public async Task creating_in_an_unknown_folder_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_file",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "x.txt",
                ["content"] = "x",
                ["folderExternalId"] = FolderExtId.NewId().Value
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
            toolName: "create_file",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "x.txt",
                ["content"] = "x"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_creating()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFileRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "note.txt",
            ["content"] = "waiting for approval"
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await AnyFileAtRoot(mcp, workspace)).Should().BeFalse();
    }

    [Fact]
    public async Task approving_then_executing_creates_the_file()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFileRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string content = "approved content";
        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, "note.txt", content);

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

        var fileId = committed.GetProperty("result").GetProperty("fileExternalId").GetString();

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();

        var read = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileId
        });

        read.GetProperty("result").GetProperty("content").GetString().Should().Be(content);
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_creating_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFileRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, "note.txt", "approved content");

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

        //then — the same file id comes back both times; the file is created exactly once
        var fileExternalId = first.GetProperty("result").GetProperty("fileExternalId").GetString();

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("fileExternalId").GetString().Should().Be(fileExternalId);
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_create()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFileRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, workspace, "note.txt", "denied content");

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

        (await AnyFileAtRoot(mcp, workspace)).Should().BeFalse();
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally create_file runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFileRequiresApproval: false);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "create_file",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "note.txt",
            ["content"] = "x"
        });

        //then — the workspace override wins; nothing is created yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        (await AnyFileAtRoot(mcp, workspace)).Should().BeFalse();
    }

    [Fact]
    public async Task operation_details_resolve_the_new_file_name_parent_and_content_preview()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(createFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        const string content = "# Quarterly report";

        var pending = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = "report.md",
            ["content"] = content,
            ["folderExternalId"] = folder.ExternalId.Value
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("create_file");
        details.GetProperty("name").GetString().Should().Be("report.md");
        details.GetProperty("folderExternalId").GetString().Should().Be(folder.ExternalId.Value);
        details.GetProperty("parentLocation").GetString().Should().Be(folder.Name);
        details.GetProperty("sizeInBytes").GetInt32().Should().Be(content.Length);
        details.GetProperty("contentPreview").GetString().Should().Be(content);
        details.GetProperty("isPreviewTruncated").GetBoolean().Should().BeFalse();
    }

    private async Task<string> SubmitCreateForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string name,
        string content)
    {
        var pending = await CallTool(mcp, "create_file", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = name,
            ["content"] = content
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

    private static async Task<bool> AnyFileAtRoot(McpAgentSession mcp, AppWorkspace workspace)
    {
        var root = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        return root.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Any(e => e.GetProperty("type").GetString() == "file");
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool createFileRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        await WaitForBucketReady(workspace, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "create_file",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = createFileRequiresApproval
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

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerManagedWorkspaceAgent()
    {
        var owner = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(owner, StorageEncryptionType.Managed);
        var workspace = await CreateWorkspace(storage, owner);
        await WaitForBucketReady(workspace, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
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
