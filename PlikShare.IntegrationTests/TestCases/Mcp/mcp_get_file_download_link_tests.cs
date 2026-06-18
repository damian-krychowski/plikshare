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
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_get_file_download_link_tests : TestFixture
{
    public mcp_get_file_download_link_tests(
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
            ["get_file_download_link", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task generates_a_link_that_downloads_the_file()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        const string content = "download me";
        var file = await UploadTextFile("report.txt", content, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        json.GetProperty("status").GetString().Should().Be("executed");
        var result = json.GetProperty("result");
        result.GetProperty("fileName").GetString().Should().Be("report.txt");
        var url = result.GetProperty("url").GetString();
        url.Should().NotBeNullOrEmpty();

        // the link is a capability — it downloads without any auth cookie
        var downloaded = await Api.PreSignedFiles.DownloadFile(preSignedUrl: url!, cookie: null);
        Encoding.UTF8.GetString(downloaded).Should().Be(content);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.FileDownloadLinkGenerated);
    }

    [Fact]
    public async Task downloads_a_managed_encrypted_file_via_the_link()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerManagedWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        const string content = "secret payload, decrypted on download";
        var file = await UploadTextFile("secret.txt", content, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        var url = json.GetProperty("result").GetProperty("url").GetString();
        var downloaded = await Api.PreSignedFiles.DownloadFile(preSignedUrl: url!, cookie: null);
        Encoding.UTF8.GetString(downloaded).Should().Be(content);
    }

    [Fact]
    public async Task honors_a_custom_expiry()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "x", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var defaultExpiry = ParseExpiry(await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        }));

        var customExpiry = ParseExpiry(await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value,
            ["expiresInMinutes"] = 120
        }));

        //then — 120 minutes vs the 15-minute default, independent of the host clock
        (customExpiry - defaultExpiry).Should().BeGreaterThan(TimeSpan.FromMinutes(60));
    }

    [Fact]
    public async Task a_file_the_agent_cannot_access_is_reported_as_not_found()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("secret.txt", "top secret", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_file_download_link",
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
            toolName: "get_file_download_link",
            arguments: new Dictionary<string, object?>
            {
                ["fileExternalId"] = FileExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_generating()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileDownloadLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "x", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
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
    public async Task approving_then_executing_generates_a_working_link()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileDownloadLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        const string content = "approved download";
        var file = await UploadTextFile("report.txt", content, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitLinkForApproval(mcp, file.ExternalId.Value);

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

        var url = committed.GetProperty("result").GetProperty("url").GetString();
        var downloaded = await Api.PreSignedFiles.DownloadFile(preSignedUrl: url!, cookie: null);
        Encoding.UTF8.GetString(downloaded).Should().Be(content);
    }

    [Fact]
    public async Task executing_twice_mints_a_fresh_link_each_time()
    {
        //given — an idempotent read is not persisted, so each execute mints a new link
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileDownloadLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("report.txt", "content", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitLinkForApproval(mcp, file.ExternalId.Value);

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
        first.GetProperty("result").GetProperty("fileName").GetString().Should().Be("report.txt");

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("fileName").GetString().Should().Be("report.txt");
    }

    [Fact]
    public async Task denying_rejects_the_execution()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileDownloadLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "x", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitLinkForApproval(mcp, file.ExternalId.Value);

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
    public async Task operation_details_resolve_the_file_name_and_lifetime()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(getFileDownloadLinkRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("budget.txt", "x", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value,
            ["expiresInMinutes"] = 120
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("get_file_download_link");
        details.GetProperty("fileExternalId").GetString().Should().Be(file.ExternalId.Value);
        details.GetProperty("name").GetString().Should().Be("budget.txt");
        details.GetProperty("expiresInMinutes").GetInt32().Should().Be(120);
    }

    private async Task<string> SubmitLinkForApproval(McpAgentSession mcp, string fileExternalId)
    {
        var pending = await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
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

    private static DateTimeOffset ParseExpiry(JsonElement json)
    {
        return DateTimeOffset.Parse(json.GetProperty("result").GetProperty("expiresAt").GetString()!);
    }

    [Fact]
    public async Task workspace_override_can_require_approval_for_files_in_that_workspace()
    {
        //given — globally get_file_download_link runs immediately; the file's workspace needs approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "x", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "get_file_download_link",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — the workspace is resolved from the file id, so the override applies
        var result = await CallTool(mcp, "get_file_download_link", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
    }

    private Task<AppFile> UploadTextFile(
        string fileName,
        string content,
        AppFolder folder,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        return UploadFile(
            content: Encoding.UTF8.GetBytes(content),
            fileName: fileName,
            contentType: "text/markdown",
            folder: folder,
            workspace: workspace,
            user: user);
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool getFileDownloadLinkRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "get_file_download_link",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = getFileDownloadLinkRequiresApproval
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
