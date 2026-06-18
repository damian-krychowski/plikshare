using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.StorageAccess;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_create_workspace_tests : TestFixture
{
    public mcp_create_workspace_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tool_is_discoverable()
    {
        var (_, _, agent) = await CreateConfiguredAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain(
            ["create_workspace", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task creates_a_workspace_owned_by_the_agent()
    {
        //given
        var (_, storage, agent) = await CreateConfiguredAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var created = await CallTool(mcp, "create_workspace", new Dictionary<string, object?>
        {
            ["name"] = "agent workspace",
            ["storageExternalId"] = storage.ExternalId.Value
        });

        //then
        created.GetProperty("status").GetString().Should().Be("executed");
        var workspaceExternalId = created.GetProperty("result").GetProperty("workspaceExternalId").GetString();
        workspaceExternalId.Should().NotBeNullOrEmpty();

        var workspaces = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        workspaces.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Select(w => w.GetProperty("workspaceExternalId").GetString())
            .Should().Contain(workspaceExternalId);

        await AssertAuditLogContains(AuditLogEventTypes.Workspace.Created);
    }

    [Fact]
    public async Task the_created_workspace_can_be_used_by_the_agent_immediately()
    {
        //given
        var (_, storage, agent) = await CreateConfiguredAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var created = await CallTool(mcp, "create_workspace", new Dictionary<string, object?>
        {
            ["name"] = "agent workspace",
            ["storageExternalId"] = storage.ExternalId.Value
        });

        var workspaceExternalId = created.GetProperty("result").GetProperty("workspaceExternalId").GetString();

        //when — the agent owns it, so it can create a folder in it right away
        var folder = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["name"] = "first folder"
        });

        //then
        folder.GetProperty("result").GetProperty("folderExternalId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task without_the_add_workspace_permission_returns_an_error()
    {
        //given
        var (_, storage, agent) = await CreateConfiguredAgent(canAddWorkspace: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "agent workspace",
                ["storageExternalId"] = storage.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task without_access_to_the_storage_returns_an_error()
    {
        //given
        var (_, storage, agent) = await CreateConfiguredAgent(grantStorageAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "agent workspace",
                ["storageExternalId"] = storage.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task on_a_full_encryption_storage_returns_an_error()
    {
        //given
        var (_, storage, agent) = await CreateConfiguredAgent(
            encryptionType: StorageEncryptionType.Full);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "agent workspace",
                ["storageExternalId"] = storage.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task on_an_unknown_storage_returns_an_error()
    {
        //given
        var (_, _, agent) = await CreateConfiguredAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "agent workspace",
                ["storageExternalId"] = StorageExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task reaching_the_workspace_limit_returns_an_error()
    {
        //given
        var (_, storage, agent) = await CreateConfiguredAgent(maxWorkspaceNumber: 1);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        await CallTool(mcp, "create_workspace", new Dictionary<string, object?>
        {
            ["name"] = "first",
            ["storageExternalId"] = storage.ExternalId.Value
        });

        //when — the second one exceeds the agent's allowance
        var result = await mcp.Client.CallToolAsync(
            toolName: "create_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["name"] = "second",
                ["storageExternalId"] = storage.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_creating()
    {
        //given
        var (owner, storage, agent) = await CreateConfiguredAgent(createWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "create_workspace", new Dictionary<string, object?>
        {
            ["name"] = "needs approval",
            ["storageExternalId"] = storage.ExternalId.Value
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");

        (await WorkspaceExists(mcp, "needs approval")).Should().BeFalse();
    }

    [Fact]
    public async Task approving_then_executing_creates_the_workspace()
    {
        //given
        var (owner, storage, agent) = await CreateConfiguredAgent(createWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, storage, "approved workspace");

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
        committed.GetProperty("result").GetProperty("workspaceExternalId").GetString().Should().NotBeNullOrEmpty();

        (await OperationStatus(mcp, approvalRequestId)).Should().BeNull();
        (await WorkspaceExists(mcp, "approved workspace")).Should().BeTrue();
    }

    [Fact]
    public async Task executing_twice_returns_the_stored_result_without_creating_again()
    {
        //given
        var (owner, storage, agent) = await CreateConfiguredAgent(createWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, storage, "approved workspace");

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

        //then — the same workspace id comes back both times; the workspace is created exactly once
        var workspaceExternalId = first.GetProperty("result").GetProperty("workspaceExternalId").GetString();

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("workspaceExternalId").GetString().Should().Be(workspaceExternalId);
    }

    [Fact]
    public async Task denying_rejects_the_execution_and_does_not_create()
    {
        //given
        var (owner, storage, agent) = await CreateConfiguredAgent(createWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, storage, "denied workspace");

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

        (await WorkspaceExists(mcp, "denied workspace")).Should().BeFalse();
    }

    [Fact]
    public async Task operation_details_resolve_the_new_workspace_name_and_storage()
    {
        //given
        var (owner, storage, agent) = await CreateConfiguredAgent(createWorkspaceRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitCreateForApproval(mcp, storage, "quarterly workspace");

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("create_workspace");
        details.GetProperty("name").GetString().Should().Be("quarterly workspace");
        details.GetProperty("storageExternalId").GetString().Should().Be(storage.ExternalId.Value);
        details.GetProperty("storageName").GetString().Should().Be(storage.Name);
    }

    private async Task<string> SubmitCreateForApproval(
        McpAgentSession mcp,
        AppStorage storage,
        string name)
    {
        var pending = await CallTool(mcp, "create_workspace", new Dictionary<string, object?>
        {
            ["name"] = name,
            ["storageExternalId"] = storage.ExternalId.Value
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

    private static async Task<bool> WorkspaceExists(McpAgentSession mcp, string name)
    {
        var workspaces = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        return workspaces.GetProperty("result").GetProperty("workspaces").EnumerateArray()
            .Any(w => w.GetProperty("name").GetString() == name);
    }

    private async Task<(AppSignedInUser Owner, AppStorage Storage, CreateAgentResponseDto Agent)> CreateConfiguredAgent(
        bool canAddWorkspace = true,
        bool grantStorageAccess = true,
        StorageEncryptionType encryptionType = StorageEncryptionType.Managed,
        int? maxWorkspaceNumber = null,
        bool createWorkspaceRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(
            user: owner,
            encryptionType: encryptionType);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdatePermissionsAndRoles(
            externalId: agent.ExternalId,
            request: new UpdateAgentPermissionsAndRolesRequestDto
            {
                IsAdmin = false,
                CanAddWorkspace = canAddWorkspace,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageAuditLog = false
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "create_workspace",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = createWorkspaceRequiresApproval
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateStorageAccess(
            externalId: agent.ExternalId,
            request: new UpdateAgentStorageAccessRequestDto
            {
                Mode = UserStorageAccessMode.AllowOnly,
                StorageExternalIds = grantStorageAccess
                    ? [storage.ExternalId.Value]
                    : []
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        if (maxWorkspaceNumber.HasValue)
            await Api.Agents.UpdateMaxWorkspaceNumber(
                externalId: agent.ExternalId,
                request: new UpdateAgentMaxWorkspaceNumberRequestDto { MaxWorkspaceNumber = maxWorkspaceNumber.Value },
                cookie: owner.Cookie,
                antiforgery: owner.Antiforgery);

        return (owner, storage, agent);
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
