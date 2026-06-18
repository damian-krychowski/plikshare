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
using PlikShare.Users.StorageAccess;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_list_storages_tests : TestFixture
{
    public mcp_list_storages_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tool_is_discoverable()
    {
        var owner = await SignIn(Users.AppOwner);
        var agent = await CreateAgent(owner);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain(
            ["list_storages", "execute_operation", "check_approvals"]);
    }

    [Fact]
    public async Task lists_only_the_storages_the_agent_can_access()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var accessible = await CreateHardDriveStorage(user: owner, encryptionType: StorageEncryptionType.Managed);
        var inaccessible = await CreateHardDriveStorage(user: owner, encryptionType: StorageEncryptionType.Managed);

        var agent = await CreateAgent(owner);
        await GrantStorageAccess(owner, agent, UserStorageAccessMode.AllowOnly, accessible.ExternalId.Value);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

        //then
        var ids = result.GetProperty("result").GetProperty("storages").EnumerateArray()
            .Select(s => s.GetProperty("storageExternalId").GetString())
            .ToList();

        ids.Should().Contain(accessible.ExternalId.Value);
        ids.Should().NotContain(inaccessible.ExternalId.Value);
    }

    [Fact]
    public async Task includes_the_name_and_encryption_type()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user: owner, encryptionType: StorageEncryptionType.Managed);

        var agent = await CreateAgent(owner);
        await GrantStorageAccess(owner, agent, UserStorageAccessMode.AllowOnly, storage.ExternalId.Value);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

        //then
        var entry = result.GetProperty("result").GetProperty("storages").EnumerateArray()
            .Single(s => s.GetProperty("storageExternalId").GetString() == storage.ExternalId.Value);

        entry.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
        entry.GetProperty("encryptionType").GetString().Should().Be("managed");
    }

    [Fact]
    public async Task omits_full_encryption_storages_even_when_accessible()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var fullStorage = await CreateHardDriveStorage(user: owner, encryptionType: StorageEncryptionType.Full);

        var agent = await CreateAgent(owner);
        await GrantStorageAccess(owner, agent, UserStorageAccessMode.AllowOnly, fullStorage.ExternalId.Value);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

        //then
        result.GetProperty("result").GetProperty("storages").EnumerateArray()
            .Select(s => s.GetProperty("storageExternalId").GetString())
            .Should().NotContain(fullStorage.ExternalId.Value);
    }

    [Fact]
    public async Task returns_no_storages_when_the_agent_has_no_access()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var agent = await CreateAgent(owner);
        await GrantStorageAccess(owner, agent, UserStorageAccessMode.AllowOnly);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

        //then
        result.GetProperty("result").GetProperty("storages").EnumerateArray().Should().BeEmpty();
    }

    [Fact]
    public async Task writes_an_audit_log_entry()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var agent = await CreateAgent(owner);
        await GrantStorageAccess(owner, agent, UserStorageAccessMode.AllowOnly);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

        //then
        await AssertAuditLogContains(AuditLogEventTypes.Agent.StoragesListed);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_listing()
    {
        //given
        var (_, _, agent) = await CreateAgentWithStorage(listStoragesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");
    }

    [Fact]
    public async Task approving_then_executing_returns_the_storages()
    {
        //given
        var (owner, storage, agent) = await CreateAgentWithStorage(listStoragesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

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

        committed.GetProperty("result").GetProperty("storages").EnumerateArray()
            .Select(s => s.GetProperty("storageExternalId").GetString())
            .Should().Contain(storage.ExternalId.Value);
    }

    [Fact]
    public async Task executing_twice_re_lists()
    {
        //given — an idempotent read is not persisted, so each execute re-lists
        var (owner, storage, agent) = await CreateAgentWithStorage(listStoragesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

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
        first.GetProperty("result").GetProperty("storages").EnumerateArray()
            .Select(s => s.GetProperty("storageExternalId").GetString())
            .Should().Contain(storage.ExternalId.Value);

        second.GetProperty("status").GetString().Should().Be("executed");
        second.GetProperty("result").GetProperty("storages").EnumerateArray()
            .Select(s => s.GetProperty("storageExternalId").GetString())
            .Should().Contain(storage.ExternalId.Value);
    }

    [Fact]
    public async Task denying_rejects_the_execution()
    {
        //given
        var (owner, _, agent) = await CreateAgentWithStorage(listStoragesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

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
    public async Task operation_details_carry_the_discriminator()
    {
        //given
        var (owner, _, agent) = await CreateAgentWithStorage(listStoragesRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var approvalRequestId = await SubmitListForApproval(mcp);

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("list_storages");
    }

    private async Task<string> SubmitListForApproval(McpAgentSession mcp)
    {
        var pending = await CallTool(mcp, "list_storages", new Dictionary<string, object?>());

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

    private async Task<(AppSignedInUser Owner, AppStorage Storage, CreateAgentResponseDto Agent)> CreateAgentWithStorage(
        bool listStoragesRequiresApproval)
    {
        var owner = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user: owner, encryptionType: StorageEncryptionType.Managed);

        var agent = await CreateAgent(owner);
        await GrantStorageAccess(owner, agent, UserStorageAccessMode.AllowOnly, storage.ExternalId.Value);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "list_storages",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = listStoragesRequiresApproval
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        return (owner, storage, agent);
    }

    private async Task<CreateAgentResponseDto> CreateAgent(AppSignedInUser owner)
    {
        return await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);
    }

    private async Task GrantStorageAccess(
        AppSignedInUser owner,
        CreateAgentResponseDto agent,
        UserStorageAccessMode mode,
        params string[] storageExternalIds)
    {
        await Api.Agents.UpdateStorageAccess(
            externalId: agent.ExternalId,
            request: new UpdateAgentStorageAccessRequestDto
            {
                Mode = mode,
                StorageExternalIds = storageExternalIds.ToList()
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);
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
