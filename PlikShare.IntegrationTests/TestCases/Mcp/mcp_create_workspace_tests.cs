using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
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

        tools.Select(t => t.Name).Should().Contain("create_workspace");
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
        var workspaceExternalId = created.GetProperty("workspaceExternalId").GetString();
        workspaceExternalId.Should().NotBeNullOrEmpty();

        var workspaces = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        workspaces.GetProperty("workspaces").EnumerateArray()
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

        var workspaceExternalId = created.GetProperty("workspaceExternalId").GetString();

        //when — the agent owns it, so it can create a folder in it right away
        var folder = await CallTool(mcp, "create_folder", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId,
            ["name"] = "first folder"
        });

        //then
        folder.GetProperty("folderExternalId").GetString().Should().NotBeNullOrEmpty();
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

    private async Task<(AppSignedInUser Owner, AppStorage Storage, CreateAgentResponseDto Agent)> CreateConfiguredAgent(
        bool canAddWorkspace = true,
        bool grantStorageAccess = true,
        StorageEncryptionType encryptionType = StorageEncryptionType.Managed,
        int? maxWorkspaceNumber = null)
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
