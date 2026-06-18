using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_list_workspace_content_tests : TestFixture
{
    public mcp_list_workspace_content_tests(
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
        tools.Select(t => t.Name).Should().Contain("list_workspace_content");
    }

    [Fact]
    public async Task lists_top_level_folders_at_the_root()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderA = await CreateFolder(workspace, owner);
        var folderB = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await ListContent(mcp, workspace.ExternalId.Value);

        //then
        ArrayOf(json, "path").Should().BeEmpty();
        json.GetProperty("hasMore").GetBoolean().Should().BeFalse();

        var entries = ArrayOf(json, "entries");
        entries.Select(e => e.GetProperty("type").GetString()).Should().AllBe("folder");
        entries.Select(e => e.GetProperty("externalId").GetString()!)
            .Should().BeEquivalentTo(folderA.ExternalId.Value, folderB.ExternalId.Value);
    }

    [Fact]
    public async Task lists_folder_content_with_breadcrumb_and_files()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folderA = await CreateFolder(workspace, owner);
        var folderB = await CreateFolder(parent: folderA, workspace: workspace, user: owner);
        var subfolderC = await CreateFolder(parent: folderB, workspace: workspace, user: owner);
        var file1 = await UploadTextFile("note-1.md", folderB, workspace, owner);
        var file2 = await UploadTextFile("note-2.md", folderB, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await ListContent(mcp, workspace.ExternalId.Value, folderExternalId: folderB.ExternalId.Value);

        //then
        ArrayOf(json, "path").Select(p => p.GetProperty("externalId").GetString()!)
            .Should().Equal(folderA.ExternalId.Value, folderB.ExternalId.Value);

        var entries = ArrayOf(json, "entries");
        entries.Select(e => e.GetProperty("externalId").GetString()!)
            .Should().BeEquivalentTo(
                subfolderC.ExternalId.Value,
                file1.ExternalId.Value,
                file2.ExternalId.Value);

        var firstFileIndex = entries.FindIndex(e => e.GetProperty("type").GetString() == "file");
        var lastFolderIndex = entries.FindLastIndex(e => e.GetProperty("type").GetString() == "folder");
        lastFolderIndex.Should().BeLessThan(firstFileIndex);

        var fileEntry = entries.First(e => e.GetProperty("externalId").GetString() == file1.ExternalId.Value);
        fileEntry.GetProperty("contentType").GetString().Should().Be("text/markdown");
        fileEntry.GetProperty("extension").GetString().Should().Contain("md");
    }

    [Fact]
    public async Task type_filter_returns_only_the_requested_kind()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var container = await CreateFolder(workspace, owner);
        var subfolder = await CreateFolder(parent: container, workspace: workspace, user: owner);
        var file = await UploadTextFile("data.txt", container, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var onlyFolders = await ListContent(mcp, workspace.ExternalId.Value, folderExternalId: container.ExternalId.Value, type: "folder");
        var onlyFiles = await ListContent(mcp, workspace.ExternalId.Value, folderExternalId: container.ExternalId.Value, type: "file");

        //then
        ArrayOf(onlyFolders, "entries").Select(e => e.GetProperty("externalId").GetString()!)
            .Should().Equal(subfolder.ExternalId.Value);

        ArrayOf(onlyFiles, "entries").Select(e => e.GetProperty("externalId").GetString()!)
            .Should().Equal(file.ExternalId.Value);
    }

    [Fact]
    public async Task paginates_with_cursor_across_folders_and_files()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var container = await CreateFolder(workspace, owner);

        for (var i = 0; i < 3; i++)
            await CreateFolder(parent: container, workspace: workspace, user: owner);

        await UploadTextFile("file-a.txt", container, workspace, owner);
        await UploadTextFile("file-b.txt", container, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var collected = new List<(string Type, string ExternalId)>();
        string? cursor = null;
        var pages = 0;
        bool hasMore;

        do
        {
            var json = await ListContent(
                mcp,
                workspace.ExternalId.Value,
                folderExternalId: container.ExternalId.Value,
                cursor: cursor,
                limit: 2);

            foreach (var entry in ArrayOf(json, "entries"))
                collected.Add((
                    entry.GetProperty("type").GetString()!,
                    entry.GetProperty("externalId").GetString()!));

            hasMore = json.GetProperty("hasMore").GetBoolean();
            cursor = GetStringOrNull(json, "nextCursor");

            if (++pages > 10)
                throw new Exception("Pagination did not terminate.");
        }
        while (hasMore);

        //then
        collected.Select(x => x.ExternalId).Should().OnlyHaveUniqueItems();
        collected.Count(x => x.Type == "folder").Should().Be(3);
        collected.Count(x => x.Type == "file").Should().Be(2);

        var firstFileIndex = collected.FindIndex(x => x.Type == "file");
        var lastFolderIndex = collected.FindLastIndex(x => x.Type == "folder");
        lastFolderIndex.Should().BeLessThan(firstFileIndex);
    }

    [Fact]
    public async Task deleted_files_are_not_listed()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        var keptFile = await UploadTextFile("keep.txt", folder, workspace, owner);
        var deletedFile = await UploadTextFile("remove.txt", folder, workspace, owner);

        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [deletedFile.ExternalId],
                FolderExternalIds = [],
                FileUploadExternalIds = []
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when / then
        await WaitFor(() =>
        {
            var json = ListContent(
                    mcp,
                    workspace.ExternalId.Value,
                    folderExternalId: folder.ExternalId.Value,
                    type: "file")
                .GetAwaiter()
                .GetResult();

            ArrayOf(json, "entries")
                .Select(e => e.GetProperty("externalId").GetString()!)
                .Should().Equal(keptFile.ExternalId.Value);
        });
    }

    [Fact]
    public async Task listing_a_non_existent_folder_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "list_workspace_content",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["folderExternalId"] = FolderExtId.NewId().Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task listing_a_workspace_without_access_returns_an_error()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(grantWorkspaceAccess: false);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "list_workspace_content",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task listing_writes_an_audit_log_entry()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        await ListContent(mcp, workspace.ExternalId.Value);

        //then
        await AssertAuditLogContains(AuditLogEventTypes.Agent.WorkspaceContentListed);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_listing()
    {
        //given
        var (_, workspace, agent) = await CreateOwnerWorkspaceAgent(listWorkspaceContentRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
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
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listWorkspaceContentRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        //then
        committed.GetProperty("status").GetString().Should().Be("executed");
        committed.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString())
            .Should().Contain(folder.ExternalId.Value);
    }

    [Fact]
    public async Task operation_details_resolve_the_folder_being_listed()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listWorkspaceContentRequiresApproval: true);
        var folder = await CreateFolder("reports", workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value,
            ["folderExternalId"] = folder.ExternalId.Value
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("list_workspace_content");
        details.GetProperty("folderExternalId").GetString().Should().Be(folder.ExternalId.Value);
        details.GetProperty("folderName").GetString().Should().Be("reports");
    }

    [Fact]
    public async Task workspace_override_can_require_approval()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(listWorkspaceContentRequiresApproval: false);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "list_workspace_content",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "list_workspace_content", new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspace.ExternalId.Value
        });

        //then — the workspace override wins
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool listWorkspaceContentRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "list_workspace_content",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = listWorkspaceContentRequiresApproval
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

    private static async Task<JsonElement> ListContent(
        McpAgentSession mcp,
        string workspaceExternalId,
        string? folderExternalId = null,
        string? type = null,
        string? cursor = null,
        int? limit = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["workspaceExternalId"] = workspaceExternalId
        };

        if (folderExternalId is not null)
            arguments["folderExternalId"] = folderExternalId;

        if (type is not null)
            arguments["type"] = type;

        if (cursor is not null)
            arguments["cursor"] = cursor;

        if (limit is not null)
            arguments["limit"] = limit;

        var result = await mcp.Client.CallToolAsync(
            toolName: "list_workspace_content",
            arguments: arguments);

        result.IsError.Should().NotBe(true);

        var text = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();

        text.Should().NotBeNullOrEmpty("list_workspace_content should return its result as JSON content");

        using var document = JsonDocument.Parse(text!);
        var envelope = document.RootElement.Clone();

        envelope.GetProperty("status").GetString().Should().Be("executed");
        return envelope.GetProperty("result");
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

    private static async Task<string?> OperationStatus(McpAgentSession mcp, string approvalRequestId)
    {
        var approvals = await CallTool(mcp, "check_approvals", new Dictionary<string, object?>());

        return approvals.GetProperty("approvals").EnumerateArray()
            .Where(a => a.GetProperty("approvalRequestId").GetString() == approvalRequestId)
            .Select(a => a.GetProperty("status").GetString())
            .FirstOrDefault();
    }

    private static List<JsonElement> ArrayOf(JsonElement json, string property)
    {
        return json.GetProperty(property).EnumerateArray().ToList();
    }

    private static string? GetStringOrNull(JsonElement json, string property)
    {
        return json.TryGetProperty(property, out var element)
            && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
    }
}
