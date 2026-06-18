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
public class mcp_read_file_tests : TestFixture
{
    public mcp_read_file_tests(
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
            ["read_file", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task reads_the_whole_text_content_of_a_small_file()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        const string text = "Hello, PlikShare!";
        var file = await UploadTextFile("doc.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        json.GetProperty("status").GetString().Should().Be("executed");
        var result = json.GetProperty("result");
        result.GetProperty("content").GetString().Should().Be(text);
        result.GetProperty("totalSizeInBytes").GetInt64().Should().Be(Encoding.UTF8.GetByteCount(text));
        result.GetProperty("nextOffset").GetInt64().Should().Be(Encoding.UTF8.GetByteCount(text));
        result.GetProperty("hasMore").GetBoolean().Should().BeFalse();

        await AssertAuditLogContains(AuditLogEventTypes.Agent.FileContentRead);
    }

    [Fact]
    public async Task pages_through_a_large_ascii_file()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var text = string.Concat(Enumerable.Range(0, 3000).Select(i => (char)('a' + i % 26)));
        var file = await UploadTextFile("big.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var reconstructed = await ReadAll(mcp, file.ExternalId.Value, maxBytes: 1024);

        //then
        reconstructed.Should().Be(text);
    }

    [Fact]
    public async Task preserves_multibyte_utf8_across_page_boundaries()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var text = string.Concat(Enumerable.Repeat("zażółć gęślą jaźń ", 300));
        var file = await UploadTextFile("polish.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — small pages force boundaries to land inside multibyte characters
        var reconstructed = await ReadAll(mcp, file.ExternalId.Value, maxBytes: 1024);

        //then
        reconstructed.Should().Be(text);
    }

    [Fact]
    public async Task reads_content_of_a_managed_encrypted_file()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerManagedWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        const string text = "Encrypted at rest, decrypted on read. zażółć gęślą jaźń";
        var file = await UploadTextFile("secret.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = file.ExternalId.Value
        });

        //then
        json.GetProperty("result").GetProperty("content").GetString().Should().Be(text);
        json.GetProperty("result").GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task pages_through_a_managed_encrypted_file()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerManagedWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var text = string.Concat(Enumerable.Repeat("zażółć gęślą jaźń ", 300));
        var file = await UploadTextFile("polish.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — exercises encrypted byte-range reads (range -> AES segment plan + trim)
        var reconstructed = await ReadAll(mcp, file.ExternalId.Value, maxBytes: 1024);

        //then
        reconstructed.Should().Be(text);
    }

    [Fact]
    public async Task a_binary_file_is_rejected_with_a_clear_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadFile(
            content: [0x25, 0x50, 0x44, 0x46, 0x2D, 0x00, 0x01, 0x02],
            fileName: "report.pdf",
            contentType: "application/pdf",
            folder: folder,
            workspace: workspace,
            user: owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "read_file",
            arguments: new Dictionary<string, object?>
            {
                ["fileExternalId"] = file.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);

        var message = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();

        message.Should().Contain("not a text file");
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
            toolName: "read_file",
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
            toolName: "read_file",
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
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(readFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "approval gated content", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "read_file", new Dictionary<string, object?>
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
    public async Task approving_then_executing_returns_the_content()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(readFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        const string text = "approved content";
        var file = await UploadTextFile("doc.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitReadForApproval(mcp, file.ExternalId.Value);

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
        committed.GetProperty("result").GetProperty("content").GetString().Should().Be(text);
    }

    [Fact]
    public async Task executing_twice_re_reads_the_content()
    {
        //given — an idempotent read is not persisted, so each execute re-reads the file
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(readFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        const string text = "approved content";
        var file = await UploadTextFile("doc.txt", text, folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitReadForApproval(mcp, file.ExternalId.Value);

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
        first.GetProperty("result").GetProperty("content").GetString().Should().Be(text);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("content").GetString().Should().Be(text);
    }

    [Fact]
    public async Task denying_rejects_the_execution()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(readFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "secret", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitReadForApproval(mcp, file.ExternalId.Value);

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
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(readFileRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("budget.txt", "content", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitReadForApproval(mcp, file.ExternalId.Value);

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("read_file");
        details.GetProperty("fileExternalId").GetString().Should().Be(file.ExternalId.Value);
        details.GetProperty("name").GetString().Should().Be("budget.txt");
        details.GetProperty("offset").GetInt64().Should().Be(0);
    }

    private async Task<string> SubmitReadForApproval(McpAgentSession mcp, string fileExternalId)
    {
        var pending = await CallTool(mcp, "read_file", new Dictionary<string, object?>
        {
            ["fileExternalId"] = fileExternalId
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        return pending.GetProperty("approvalRequestId").GetString()!;
    }

    // The agent's own view of an operation's status via check_approvals — or null when it is not
    // (or no longer) listed. Note: an idempotent read stays 'approved' after executing (it is not
    // persisted), so it can be re-read until the approval window's retention purge removes it.
    private static async Task<string?> OperationStatus(McpAgentSession mcp, string approvalRequestId)
    {
        var approvals = await CallTool(mcp, "check_approvals", new Dictionary<string, object?>());

        return approvals.GetProperty("approvals").EnumerateArray()
            .Where(a => a.GetProperty("approvalRequestId").GetString() == approvalRequestId)
            .Select(a => a.GetProperty("status").GetString())
            .FirstOrDefault();
    }

    private async Task<string> ReadAll(
        McpAgentSession mcp,
        string fileExternalId,
        int maxBytes)
    {
        var builder = new StringBuilder();
        long offset = 0;

        for (var page = 0; page < 1000; page++)
        {
            var json = await CallTool(mcp, "read_file", new Dictionary<string, object?>
            {
                ["fileExternalId"] = fileExternalId,
                ["offset"] = offset,
                ["maxBytes"] = maxBytes
            });

            var result = json.GetProperty("result");

            builder.Append(result.GetProperty("content").GetString());

            if (!result.GetProperty("hasMore").GetBoolean())
                return builder.ToString();

            var nextOffset = result.GetProperty("nextOffset").GetInt64();
            nextOffset.Should().BeGreaterThan(offset, "each page must make progress");
            offset = nextOffset;
        }

        throw new InvalidOperationException("read_file did not finish paging within the page limit");
    }

    [Fact]
    public async Task workspace_override_can_require_approval_for_files_in_that_workspace()
    {
        //given — globally read_file runs immediately; the file's workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", "content", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "read_file",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — the workspace is resolved from the file id, so the override applies
        var result = await CallTool(mcp, "read_file", new Dictionary<string, object?>
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
        bool readFileRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "read_file",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = readFileRequiresApproval
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
