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
        tools.Select(t => t.Name).Should().Contain("read_file");
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
        json.GetProperty("content").GetString().Should().Be(text);
        json.GetProperty("totalSizeInBytes").GetInt64().Should().Be(Encoding.UTF8.GetByteCount(text));
        json.GetProperty("nextOffset").GetInt64().Should().Be(Encoding.UTF8.GetByteCount(text));
        json.GetProperty("hasMore").GetBoolean().Should().BeFalse();

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
        json.GetProperty("content").GetString().Should().Be(text);
        json.GetProperty("hasMore").GetBoolean().Should().BeFalse();
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

            builder.Append(json.GetProperty("content").GetString());

            if (!json.GetProperty("hasMore").GetBoolean())
                return builder.ToString();

            var nextOffset = json.GetProperty("nextOffset").GetInt64();
            nextOffset.Should().BeGreaterThan(offset, "each page must make progress");
            offset = nextOffset;
        }

        throw new InvalidOperationException("read_file did not finish paging within the page limit");
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
