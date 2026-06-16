using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_update_share_link_tests : TestFixture
{
    private const string FutureExpiry = "2099-12-31T23:59:59Z";

    public mcp_update_share_link_tests(
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
        tools.Select(t => t.Name).Should().Contain("update_share_link");
    }

    [Fact]
    public async Task updates_name_expiry_max_downloads_and_password_together()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkId = await CreateShareLink(mcp, workspace, "original", fileExternalIds: [file.ExternalId.Value]);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = linkId,
                ["name"] = "renamed",
                ["shouldUpdateExpiry"] = true,
                ["expiresAt"] = FutureExpiry,
                ["shouldUpdateMaxDownloads"] = true,
                ["maxDownloads"] = 5,
                ["shouldUpdatePassword"] = true,
                ["password"] = "s3cret"
            });

        //then
        result.IsError.Should().NotBe(true);

        var json = await GetShareLink(mcp, workspace, linkId);
        json.GetProperty("name").GetString().Should().Be("renamed");
        json.GetProperty("expiresAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        json.GetProperty("maxDownloads").GetInt32().Should().Be(5);
        json.GetProperty("hasPassword").GetBoolean().Should().BeTrue();

        await AssertAuditLogContains(AuditLogEventTypes.QuickShare.Updated);
    }

    [Fact]
    public async Task clears_expiry_max_downloads_and_password_when_flags_set_without_values()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkId = await CreateShareLink(
            mcp,
            workspace,
            "secured",
            fileExternalIds: [file.ExternalId.Value],
            expiresAt: FutureExpiry,
            maxDownloads: 7,
            password: "pw");

        var before = await GetShareLink(mcp, workspace, linkId);
        before.GetProperty("expiresAt").ValueKind.Should().NotBe(JsonValueKind.Null);
        before.GetProperty("maxDownloads").GetInt32().Should().Be(7);
        before.GetProperty("hasPassword").GetBoolean().Should().BeTrue();

        //when — flags true, values omitted => clear
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = linkId,
                ["shouldUpdateExpiry"] = true,
                ["shouldUpdateMaxDownloads"] = true,
                ["shouldUpdatePassword"] = true
            });

        //then
        result.IsError.Should().NotBe(true);

        var after = await GetShareLink(mcp, workspace, linkId);
        after.TryGetProperty("expiresAt", out _).Should().BeFalse("a cleared expiry is omitted from the response");
        after.TryGetProperty("maxDownloads", out _).Should().BeFalse("cleared max downloads is omitted from the response");
        after.GetProperty("hasPassword").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task updating_only_the_name_keeps_other_settings()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkId = await CreateShareLink(
            mcp,
            workspace,
            "before",
            fileExternalIds: [file.ExternalId.Value],
            maxDownloads: 3,
            password: "pw");

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = linkId,
                ["name"] = "after"
            });

        //then
        result.IsError.Should().NotBe(true);

        var json = await GetShareLink(mcp, workspace, linkId);
        json.GetProperty("name").GetString().Should().Be("after");
        json.GetProperty("maxDownloads").GetInt32().Should().Be(3);
        json.GetProperty("hasPassword").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task updating_with_no_fields_returns_an_error()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var file = await UploadTextFile("doc.txt", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var linkId = await CreateShareLink(mcp, workspace, "x", fileExternalIds: [file.ExternalId.Value]);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = linkId
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task updating_an_unknown_share_link_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = "qs_does_not_exist",
                ["name"] = "x"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task updating_in_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "update_share_link",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["shareLinkExternalId"] = "qs_whatever",
                ["name"] = "x"
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<string> CreateShareLink(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string name,
        string[]? fileExternalIds = null,
        string? expiresAt = null,
        int? maxDownloads = null,
        string? password = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["name"] = name
        };

        if (fileExternalIds is not null)
            arguments["fileExternalIds"] = fileExternalIds;

        if (expiresAt is not null)
            arguments["expiresAt"] = expiresAt;

        if (maxDownloads is not null)
            arguments["maxDownloads"] = maxDownloads;

        if (password is not null)
            arguments["password"] = password;

        var json = await CallTool(mcp, "create_share_link", arguments);

        return json.GetProperty("externalId").GetString()!;
    }

    private Task<JsonElement> GetShareLink(
        McpAgentSession mcp,
        AppWorkspace workspace,
        string shareLinkExternalId)
    {
        return CallTool(mcp, "get_share_link", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["shareLinkExternalId"] = shareLinkExternalId
        });
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
