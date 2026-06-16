using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
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
        tools.Select(t => t.Name).Should().Contain("get_file_download_link");
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
        json.GetProperty("fileName").GetString().Should().Be("report.txt");
        var url = json.GetProperty("url").GetString();
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
        var url = json.GetProperty("url").GetString();
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

    private static DateTimeOffset ParseExpiry(JsonElement json)
    {
        return DateTimeOffset.Parse(json.GetProperty("expiresAt").GetString()!);
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
