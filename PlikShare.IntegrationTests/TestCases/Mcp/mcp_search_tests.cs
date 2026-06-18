using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_search_tests : TestFixture
{
    public mcp_search_tests(
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

        tools.Select(t => t.Name).Should().Contain("search");
    }

    [Fact]
    public async Task finds_files_by_name()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("alpha.txt", "a", "text/markdown", folder, workspace, owner);
        await UploadTextFile("beta.txt", "b", "text/markdown", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["nameContains"] = new[] { "alpha" }
        });

        //then
        Names(json).Should().BeEquivalentTo("alpha");
        await AssertAuditLogContains(AuditLogEventTypes.Agent.SearchPerformed);
    }

    [Fact]
    public async Task extensions_filter_is_an_or_within_the_list()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("a.txt", "x", "text/plain", folder, workspace, owner);
        await UploadTextFile("b.md", "x", "text/markdown", folder, workspace, owner);
        await UploadTextFile("c.pdf", "x", "application/pdf", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["extensions"] = new[] { "txt", ".md" }
        });

        //then — txt OR md, the dot is optional
        Names(json).Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public async Task content_type_prefix_matches_a_family()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("pic.png", "x", "image/png", folder, workspace, owner);
        await UploadTextFile("photo.jpg", "x", "image/jpeg", folder, workspace, owner);
        await UploadTextFile("doc.txt", "x", "text/plain", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var byPrefix = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["contentTypes"] = new[] { "image/*" }
        });

        var byExact = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["contentTypes"] = new[] { "image/png" }
        });

        //then
        Names(byPrefix).Should().BeEquivalentTo("pic", "photo");
        Names(byExact).Should().BeEquivalentTo("pic");
    }

    [Fact]
    public async Task different_filters_are_anded_together()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("invoice.pdf", "x", "application/pdf", folder, workspace, owner);
        await UploadTextFile("invoice.txt", "x", "text/plain", folder, workspace, owner);
        await UploadTextFile("receipt.pdf", "x", "application/pdf", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — name contains invoice AND extension pdf
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["nameContains"] = new[] { "invoice" },
            ["extensions"] = new[] { "pdf" }
        });

        //then
        json.GetProperty("result").GetProperty("entries").EnumerateArray().Should().HaveCount(1);
        Names(json).Should().BeEquivalentTo("invoice");
    }

    [Fact]
    public async Task type_filter_selects_files_or_folders()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder("documents", workspace, owner);
        await UploadTextFile("note.txt", "x", "text/plain", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var onlyFolders = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "folder" }
        });

        var onlyFiles = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" }
        });

        //then
        Types(onlyFolders).Should().OnlyContain(t => t == "folder");
        Names(onlyFolders).Should().Contain("documents");

        Types(onlyFiles).Should().OnlyContain(t => t == "file");
        Names(onlyFiles).Should().Contain("note");
    }

    [Fact]
    public async Task folder_scope_searches_the_subtree_only()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var outside = await CreateFolder(workspace, owner);
        var scope = await CreateFolder(workspace, owner);
        var nested = await CreateFolder(parent: scope, workspace: workspace, user: owner);

        await UploadTextFile("outside.txt", "x", "text/plain", outside, workspace, owner);
        await UploadTextFile("inside.txt", "x", "text/plain", scope, workspace, owner);
        await UploadTextFile("deep.txt", "x", "text/plain", nested, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["folderIds"] = new[] { scope.ExternalId.Value }
        });

        //then — direct child and nested descendant, but not the file outside the subtree
        Names(json).Should().BeEquivalentTo("inside", "deep");
    }

    [Fact]
    public async Task size_range_filters_files()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("small.txt", "ab", "text/plain", folder, workspace, owner);          // 2 bytes
        await UploadTextFile("big.txt", new string('x', 500), "text/plain", folder, workspace, owner); // 500 bytes

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["sizeMin"] = 100
        });

        //then
        Names(json).Should().BeEquivalentTo("big");
    }

    [Fact]
    public async Task created_range_filters_by_date()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("recent.txt", "x", "text/plain", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var inFuture = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["createdAfter"] = "2999-01-01T00:00:00Z"
        });

        var inPast = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["createdAfter"] = "2000-01-01T00:00:00Z"
        });

        //then
        inFuture.GetProperty("result").GetProperty("entries").EnumerateArray().Should().BeEmpty();
        Names(inPast).Should().Contain("recent");
    }

    [Fact]
    public async Task paginates_with_a_cursor()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var folder = await CreateFolder(workspace, owner);
        for (var i = 0; i < 5; i++)
            await UploadTextFile($"file-{i}.txt", "x", "text/plain", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var collected = await SearchAllNames(mcp, new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["extensions"] = new[] { "txt" },
            ["limit"] = 2
        });

        //then
        collected.Should().BeEquivalentTo("file-0", "file-1", "file-2", "file-3", "file-4");
    }

    [Fact]
    public async Task searches_across_workspaces_and_can_be_scoped_to_one()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var workspaceA = await CreateWorkspace(owner);
        var workspaceB = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await GrantAccess(owner, agent, workspaceA);
        await GrantAccess(owner, agent, workspaceB);

        var folderA = await CreateFolder(workspaceA, owner);
        var folderB = await CreateFolder(workspaceB, owner);
        await UploadTextFile("in-a.txt", "x", "text/plain", folderA, workspaceA, owner);
        await UploadTextFile("in-b.txt", "x", "text/plain", folderB, workspaceB, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var everywhere = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["extensions"] = new[] { "txt" }
        });

        var onlyA = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["extensions"] = new[] { "txt" },
            ["workspaceIds"] = new[] { workspaceA.ExternalId.Value }
        });

        //then
        Names(everywhere).Should().Contain(new[] { "in-a", "in-b" });
        Names(onlyA).Should().Contain("in-a");
        Names(onlyA).Should().NotContain("in-b");
    }

    [Fact]
    public async Task items_from_workspaces_without_access_are_silently_excluded()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var granted = await CreateWorkspace(owner);
        var notGranted = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await GrantAccess(owner, agent, granted);

        var grantedFolder = await CreateFolder(granted, owner);
        var hiddenFolder = await CreateFolder(notGranted, owner);
        await UploadTextFile("visible.txt", "x", "text/plain", grantedFolder, granted, owner);
        await UploadTextFile("hidden.txt", "x", "text/plain", hiddenFolder, notGranted, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when — even explicitly asking for the inaccessible workspace yields no error and no leak
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["extensions"] = new[] { "txt" },
            ["workspaceIds"] = new[] { granted.ExternalId.Value, notGranted.ExternalId.Value }
        });

        //then
        var names = Names(json);
        names.Should().Contain("visible");
        names.Should().NotContain("hidden");
    }

    [Fact]
    public async Task exclude_folder_carves_out_its_subtree_for_files()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var keep = await CreateFolder(workspace, owner);
        var archive = await CreateFolder("archive", workspace, owner);
        var archiveOld = await CreateFolder(parent: archive, workspace: workspace, user: owner);

        await UploadTextFile("keep.txt", "x", "text/plain", keep, workspace, owner);
        await UploadTextFile("arch.txt", "x", "text/plain", archive, workspace, owner);
        await UploadTextFile("old.txt", "x", "text/plain", archiveOld, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["excludeFolderIds"] = new[] { archive.ExternalId.Value }
        });

        //then — the file outside survives, the file in the excluded folder and its descendant are gone
        var names = Names(json);
        names.Should().Contain("keep");
        names.Should().NotContain("arch");
        names.Should().NotContain("old");
    }

    [Fact]
    public async Task exclude_folder_removes_the_folder_and_its_descendants_from_folder_results()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();
        var keep = await CreateFolder("keep-me", workspace, owner);
        var archive = await CreateFolder("archive", workspace, owner);
        var nested = await CreateFolder(parent: archive, workspace: workspace, user: owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "folder" },
            ["excludeFolderIds"] = new[] { archive.ExternalId.Value }
        });

        //then
        var ids = ExternalIds(json);
        ids.Should().Contain(keep.ExternalId.Value);
        ids.Should().NotContain(archive.ExternalId.Value);
        ids.Should().NotContain(nested.ExternalId.Value);
    }

    [Fact]
    public async Task exclude_workspace_removes_its_items()
    {
        //given
        var owner = await SignIn(Users.AppOwner);

        var workspaceA = await CreateWorkspace(owner);
        var workspaceB = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await GrantAccess(owner, agent, workspaceA);
        await GrantAccess(owner, agent, workspaceB);

        var folderA = await CreateFolder(workspaceA, owner);
        var folderB = await CreateFolder(workspaceB, owner);
        await UploadTextFile("in-a.txt", "x", "text/plain", folderA, workspaceA, owner);
        await UploadTextFile("in-b.txt", "x", "text/plain", folderB, workspaceB, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["extensions"] = new[] { "txt" },
            ["excludeWorkspaceIds"] = new[] { workspaceB.ExternalId.Value }
        });

        //then
        var names = Names(json);
        names.Should().Contain("in-a");
        names.Should().NotContain("in-b");
    }

    [Fact]
    public async Task folder_filter_on_a_folder_type_search_is_rejected()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "search",
            arguments: new Dictionary<string, object?>
            {
                ["types"] = new[] { "folder" },
                ["contentTypes"] = new[] { "image/png" }
            });

        //then
        result.IsError.Should().Be(true);

        var message = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();

        message.Should().Contain("files only");
    }

    [Fact]
    public async Task an_invalid_cursor_returns_an_error()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "search",
            arguments: new Dictionary<string, object?>
            {
                ["cursor"] = "not-a-real-cursor!!"
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task when_approval_required_the_call_waits_instead_of_searching()
    {
        //given
        var (_, _, agent) = await CreateOwnerWorkspaceAgent(searchRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["nameContains"] = new[] { "alpha" }
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        pending.GetProperty("approvalRequestId").GetString().Should().StartWith("aop_");

        (await OperationStatus(mcp, pending.GetProperty("approvalRequestId").GetString()!))
            .Should().Be("pending");
    }

    [Fact]
    public async Task approving_then_executing_returns_the_results()
    {
        //given
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent(searchRequiresApproval: true);
        var folder = await CreateFolder(workspace, owner);
        await UploadTextFile("alpha.txt", "a", "text/plain", folder, workspace, owner);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["nameContains"] = new[] { "alpha" }
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
        Names(committed).Should().Contain("alpha");
    }

    [Fact]
    public async Task operation_details_surface_the_key_filters()
    {
        //given
        var (owner, _, agent) = await CreateOwnerWorkspaceAgent(searchRequiresApproval: true);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["nameContains"] = new[] { "invoice" },
            ["extensions"] = new[] { "pdf" }
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("search");
        details.GetProperty("nameContains").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("invoice");
        details.GetProperty("extensions").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("pdf");
        details.GetProperty("types").EnumerateArray()
            .Select(e => e.GetString()).Should().Contain("file");
    }

    [Fact]
    public async Task search_disabled_in_a_workspace_excludes_it_from_the_scope()
    {
        //given — two accessible workspaces, search disabled in one of them via a per-workspace override
        var owner = await SignIn(Users.AppOwner);

        var workspaceA = await CreateWorkspace(owner);
        var workspaceB = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await GrantAccess(owner, agent, workspaceA);
        await GrantAccess(owner, agent, workspaceB);

        var folderA = await CreateFolder(workspaceA, owner);
        var folderB = await CreateFolder(workspaceB, owner);
        await UploadTextFile("in-a.txt", "x", "text/plain", folderA, workspaceA, owner);
        await UploadTextFile("in-b.txt", "x", "text/plain", folderB, workspaceB, owner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspaceA.ExternalId,
            toolName: "search",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var json = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" },
            ["extensions"] = new[] { "txt" }
        });

        //then — workspace A dropped out of the scope, only workspace B's file shows up
        var names = Names(json);
        names.Should().Contain("in-b");
        names.Should().NotContain("in-a");
    }

    [Fact]
    public async Task search_requires_approval_if_any_accessible_workspace_requires_it()
    {
        //given — approval is required for search in just one of the agent's accessible workspaces
        var owner = await SignIn(Users.AppOwner);

        var workspaceA = await CreateWorkspace(owner);
        var workspaceB = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await GrantAccess(owner, agent, workspaceA);
        await GrantAccess(owner, agent, workspaceB);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspaceA.ExternalId,
            toolName: "search",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "search", new Dictionary<string, object?>
        {
            ["types"] = new[] { "file" }
        });

        //then — one workspace requiring approval gates the whole search
        result.GetProperty("status").GetString().Should().Be("waits_for_approval");
    }

    [Fact]
    public async Task search_disabled_in_every_accessible_workspace_is_not_usable()
    {
        //given — the agent's only workspace has search disabled
        var (owner, workspace, agent) = await CreateOwnerWorkspaceAgent();

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "search",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "search",
            arguments: new Dictionary<string, object?>
            {
                ["types"] = new[] { "file" }
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<List<string>> SearchAllNames(
        McpAgentSession mcp,
        Dictionary<string, object?> arguments)
    {
        var names = new List<string>();
        string? cursor = null;

        for (var page = 0; page < 100; page++)
        {
            if (cursor is not null)
                arguments["cursor"] = cursor;

            var json = await CallTool(mcp, "search", arguments);

            names.AddRange(Names(json));

            if (!json.GetProperty("result").GetProperty("hasMore").GetBoolean())
                return names;

            cursor = json.GetProperty("result").GetProperty("nextCursor").GetString();
        }

        throw new InvalidOperationException("search did not finish paging within the page limit");
    }

    private static List<string> Names(JsonElement json)
    {
        return json.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("name").GetString()!)
            .ToList();
    }

    private static List<string> Types(JsonElement json)
    {
        return json.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString()!)
            .ToList();
    }

    private static List<string> ExternalIds(JsonElement json)
    {
        return json.GetProperty("result").GetProperty("entries").EnumerateArray()
            .Select(e => e.GetProperty("externalId").GetString()!)
            .ToList();
    }

    private Task GrantAccess(AppSignedInUser owner, CreateAgentResponseDto agent, AppWorkspace workspace)
    {
        return Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);
    }

    private Task<AppFile> UploadTextFile(
        string fileName,
        string content,
        string contentType,
        AppFolder folder,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        return UploadFile(
            content: Encoding.UTF8.GetBytes(content),
            fileName: fileName,
            contentType: contentType,
            folder: folder,
            workspace: workspace,
            user: user);
    }

    private async Task<(AppSignedInUser Owner, AppWorkspace Workspace, CreateAgentResponseDto Agent)> CreateOwnerWorkspaceAgent(
        bool grantWorkspaceAccess = true,
        bool searchRequiresApproval = false)
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "search",
            request: new UpdateAgentToolConfigRequestDto
            {
                IsEnabled = true,
                RequiresApproval = searchRequiresApproval
            },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        if (grantWorkspaceAccess)
            await GrantAccess(owner, agent, workspace);

        return (owner, workspace, agent);
    }

    private static async Task<string?> OperationStatus(McpAgentSession mcp, string approvalRequestId)
    {
        var approvals = await CallTool(mcp, "check_approvals", new Dictionary<string, object?>());

        return approvals.GetProperty("approvals").EnumerateArray()
            .Where(a => a.GetProperty("approvalRequestId").GetString() == approvalRequestId)
            .Select(a => a.GetProperty("status").GetString())
            .FirstOrDefault();
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
