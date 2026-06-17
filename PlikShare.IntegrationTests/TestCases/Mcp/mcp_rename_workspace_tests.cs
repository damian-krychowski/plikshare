using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Workspaces.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_rename_workspace_tests : TestFixture
{
    public mcp_rename_workspace_tests(
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

        tools.Select(t => t.Name).Should().Contain("rename_workspace");
    }

    [Fact]
    public async Task renames_a_workspace_the_agent_can_access()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "renamed workspace"
            });

        //then
        result.IsError.Should().NotBe(true);

        var workspaces = await CallTool(mcp, "list_workspaces", new Dictionary<string, object?>());

        var entry = workspaces.GetProperty("workspaces").EnumerateArray()
            .Single(w => w.GetProperty("workspaceExternalId").GetString() == workspace.ExternalId.Value);

        entry.GetProperty("name").GetString().Should().Be("renamed workspace");

        await AssertAuditLogContains(AuditLogEventTypes.Workspace.NameUpdated);
    }

    [Fact]
    public async Task renaming_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = "renamed"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task renaming_an_unknown_workspace_returns_an_error()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "rename_workspace",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = WorkspaceExtId.NewId().Value,
                ["name"] = "renamed"
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
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
