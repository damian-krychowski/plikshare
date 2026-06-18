using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using PlikShare.Core.SQLite;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Operations;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_bulk_delete_tests : TestFixture
{
    public mcp_bulk_delete_tests(
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
            ["bulk_delete", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task deletes_a_folder_with_its_whole_subtree()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderA = await CreateFolder(workspace, owner);
        var folderB = await CreateFolder(parent: folderA, workspace: workspace, user: owner);
        await UploadTextFile("a.txt", folderA, workspace, owner);
        await UploadTextFile("b.txt", folderA, workspace, owner);
        await UploadTextFile("c.txt", folderB, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallToolExecuted(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folderA.ExternalId.Value }
        });

        //then
        result.GetProperty("deletedFileCount").GetInt32().Should().Be(3);
        result.GetProperty("deletedSizeInBytes").GetInt64().Should().BeGreaterThan(0);

        var root = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        root.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().NotContain(folderA.ExternalId.Value);
    }

    [Fact]
    public async Task deletes_individual_files()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file1 = await UploadTextFile("a.txt", folder, workspace, owner);
        var file2 = await UploadTextFile("b.txt", folder, workspace, owner);
        var keptFile = await UploadTextFile("keep.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallToolExecuted(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["fileExternalIds"] = new[] { file1.ExternalId.Value, file2.ExternalId.Value }
        });

        //then
        result.GetProperty("deletedFileCount").GetInt32().Should().Be(2);

        var content = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalId"] = folder.ExternalId.Value
        });

        content.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().Equal(keptFile.ExternalId.Value);
    }

    [Fact]
    public async Task deletes_folders_and_files_together()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderToDelete = await CreateFolder(workspace, owner);
        await UploadTextFile("inside.txt", folderToDelete, workspace, owner);

        var otherFolder = await CreateFolder(workspace, owner);
        var fileToDelete = await UploadTextFile("file-to-delete.txt", otherFolder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallToolExecuted(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folderToDelete.ExternalId.Value },
            ["fileExternalIds"] = new[] { fileToDelete.ExternalId.Value }
        });

        //then
        result.GetProperty("deletedFileCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task requires_at_least_one_id()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "bulk_delete",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deleting_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "bulk_delete",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalIds"] = new[] { folder.ExternalId.Value }
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task deleting_writes_an_audit_log_entry()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        await CallToolExecuted(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folder.ExternalId.Value }
        });

        //then
        await AssertAuditLogContains(AuditLogEventTypes.Workspace.BulkDeleteRequested);
    }

    [Fact]
    public async Task a_workspace_override_can_require_approval_even_when_the_global_setting_does_not()
    {
        //given — globally bulk_delete runs immediately, but this workspace overrides it to need approval
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: false);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folder.ExternalId.Value }
        });

        //then — the workspace override wins; nothing is deleted yet
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
        await AssertFolderStillExists(mcp, workspace, folder);
    }

    [Fact]
    public async Task a_workspace_override_can_disable_bulk_delete_for_that_workspace()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: false);
        var folder = await CreateFolder(workspace, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "bulk_delete",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalIds"] = new[] { folder.ExternalId.Value }
            });

        //then — disabled for this workspace, so the call is rejected
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_deleting()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folder.ExternalId.Value }
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        var status = await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!);

        status.Should().Be("pending");

        await AssertFolderStillExists(mcp, workspace, folder);
    }

    [Fact]
    public async Task executing_before_approval_keeps_waiting_and_does_not_delete()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

        //when
        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then
        committed.GetProperty("status").GetString().Should().Be("waits_for_approval");
        await AssertFolderStillExists(mcp, workspace, folder);
    }

    [Fact]
    public async Task approving_then_executing_runs_the_deletion()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);
        await UploadTextFile("b.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

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
        committed.GetProperty("result").GetProperty("deletedFileCount").GetInt32().Should().Be(2);

        // Once executed it drops off the agent's approvals list.
        var executedStatus = await OperationStatus(mcp, approvalRequestId);

        executedStatus.Should().BeNull();

        var root = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        root.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().NotContain(folder.ExternalId.Value);
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_deleting_again()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

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
        first.GetProperty("result").GetProperty("deletedFileCount").GetInt32().Should().Be(1);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("deletedFileCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task denying_rejects_the_commit_and_does_not_delete()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

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

        await AssertFolderStillExists(mcp, workspace, folder);
    }

    [Fact]
    public async Task executing_an_unknown_operation_id_is_an_error()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var commit = await mcp.Client.CallToolAsync(
            toolName: "execute_operation",
            arguments: new Dictionary<string, object?>
            {
                ["approvalRequestId"] = "aop_does_not_exist"
            });

        //then
        commit.IsError.Should().Be(true);
    }

    [Fact]
    public async Task another_agent_cannot_see_a_foreign_operation()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

        var otherAgent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var otherMcp = await Api.Mcp.ConnectAsAgent(otherAgent.Token);

        //when
        var status = await OperationStatus(otherMcp, approvalRequestId);

        //then — the other agent's approvals list never shows a foreign operation
        status.Should().BeNull();
    }

    [Fact]
    public async Task check_approvals_lists_the_agents_outstanding_operations()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folderA = await CreateFolder(workspace, owner);
        var folderB = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folderA, workspace, owner);
        await UploadTextFile("b.txt", folderB, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var first = await SubmitForApproval(mcp, workspace, folderA);
        var second = await SubmitForApproval(mcp, workspace, folderB);

        //when
        var approvals = await CallTool(mcp, "check_approvals", new Dictionary<string, object?>());

        //then
        var listed = approvals.GetProperty("approvals").EnumerateArray()
            .Select(a => a.GetProperty("approvalRequestId").GetString())
            .ToList();

        listed.Should().Contain([first, second]);

        (await OperationStatus(mcp, first)).Should().Be("pending");
        (await OperationStatus(mcp, second)).Should().Be("pending");
    }

    [Fact]
    public async Task pending_operations_are_listed_for_the_owner_until_resolved()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

        //when
        var listed = await Api.Agents.GetPendingOperations(owner.Cookie);

        //then
        var item = listed.Items.Should().ContainSingle(i => i.ExternalId.Value == approvalRequestId).Subject;
        item.ToolName.Should().Be("bulk_delete");
        item.Agent.ExternalId.Should().Be(agent.ExternalId);
        item.Workspace!.ExternalId.Should().Be(workspace.ExternalId);
        item.Parameters.GetProperty("folderExternalIds").EnumerateArray()
            .Select(e => e.GetString())
            .Should().Contain(folder.ExternalId.Value);

        //and once resolved it drops off the inbox
        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var afterApprove = await Api.Agents.GetPendingOperations(owner.Cookie);
        afterApprove.Items.Should().NotContain(i => i.ExternalId.Value == approvalRequestId);
    }

    [Fact]
    public async Task operation_details_resolve_target_folder_and_file_names()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("budget.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folder.ExternalId.Value },
            ["fileExternalIds"] = new[] { file.ExternalId.Value }
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("bulk_delete");

        details.GetProperty("folders").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .Should().Contain(folder.Name);

        details.GetProperty("files").EnumerateArray()
            .Select(f => f.GetProperty("name").GetString())
            .Should().Contain("budget.txt");
    }

    [Fact]
    public async Task sweeper_expires_a_pending_operation_and_blocks_its_approval()
    {
        //given
        var t0 = Clock.UtcNow;

        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

        //when — the approval window elapses, then the sweeper runs
        Clock.CurrentTime(t0.AddHours(3));
        await RunOperationsSweep();

        //then
        var status = await OperationStatus(mcp, approvalRequestId);

        status.Should().Be("expired");

        var approve = async () => await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await approve.Should().ThrowAsync<TestApiCallException>();
    }

    [Fact]
    public async Task sweeper_purges_resolved_operations_past_retention()
    {
        //given
        var t0 = Clock.UtcNow;

        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(bulkDeleteRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var approvalRequestId = await SubmitForApproval(mcp, workspace, folder);

        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        OperationExistsInDb(approvalRequestId).Should().BeTrue();

        //when — the retention window elapses, then the sweeper runs
        Clock.CurrentTime(t0.AddHours(7));
        await RunOperationsSweep();

        //then — the resolved operation is purged from the ledger for good
        OperationExistsInDb(approvalRequestId).Should().BeFalse();
    }

    private bool OperationExistsInDb(string approvalRequestId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: "SELECT 1 FROM aop_agent_operations WHERE aop_external_id = $externalId",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", approvalRequestId)
            .Execute();

        return !result.IsEmpty;
    }

    private Task RunOperationsSweep()
    {
        var sweeper = HostFixture.App.Services
            .GetServices<IHostedService>()
            .OfType<AgentOperationsSweeperHostedService>()
            .Single();

        return sweeper.RunTick(CancellationToken.None);
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

    private async Task<string> SubmitForApproval(
        McpAgentSession mcp,
        AppWorkspace workspace,
        AppFolder folder)
    {
        var pending = await CallTool(mcp, "bulk_delete", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalIds"] = new[] { folder.ExternalId.Value }
        });

        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        return pending.GetProperty("approvalRequestId").GetString()!;
    }

    private static async Task AssertFolderStillExists(
        McpAgentSession mcp,
        AppWorkspace workspace,
        AppFolder folder)
    {
        var root = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        root.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().Contain(folder.ExternalId.Value);
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool bulkDeleteRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = bulkDeleteRequiresApproval
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

    private static async Task<JsonElement> CallToolExecuted(
        McpAgentSession mcp,
        string toolName,
        Dictionary<string, object?> arguments)
    {
        var json = await CallTool(mcp, toolName, arguments);
        json.GetProperty("status").GetString().Should().Be("executed");
        return json.GetProperty("result");
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
