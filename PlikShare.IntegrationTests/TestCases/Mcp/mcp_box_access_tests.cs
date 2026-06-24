using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

/// <summary>
/// Box-access tools: the agent acts as a consumer inside a box it was granted direct access to
/// (ba_box_agents), independently of any workspace membership. These cover the discovery entry point
/// (list_boxes) and reading a box's details (get_box_details).
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class mcp_box_access_tests : TestFixture
{
    public mcp_box_access_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task tools_are_discoverable()
    {
        var (_, _, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var tools = await mcp.Client.ListToolsAsync();

        tools.Select(t => t.Name).Should().Contain(
        [
            "list_boxes", "get_box_details", "list_box_content", "read_box_file",
            "get_box_file_download_link", "get_box_bulk_download_link", "search_box",
            "create_box_folder", "create_box_file", "rename_box_file", "rename_box_folder",
            "move_box_items", "delete_box_items"
        ]);
    }

    [Fact]
    public async Task lists_the_boxes_shared_with_the_agent()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var boxes = await ListBoxes(mcp);

        //then
        var listed = boxes.Should().ContainSingle(
            b => b.GetProperty("externalId").GetString() == box.ExternalId.Value).Subject;
        listed.GetProperty("name").GetString().Should().Be(box.Name);
        listed.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        listed.GetProperty("workspaceName").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task does_not_list_boxes_the_agent_was_not_granted()
    {
        //given — a box the owner owns but never shares with the agent
        var owner = await SignIn(Users.AppOwner);
        var otherBox = await CreateBox(owner);

        var agent = await CreateAgentWithoutAccess(owner);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var boxes = await ListBoxes(mcp);

        //then
        boxes.Should().NotContain(b => b.GetProperty("externalId").GetString() == otherBox.ExternalId.Value);
    }

    [Fact]
    public async Task reads_a_box_details()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await CallTool(mcp, "get_box_details", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var details = result.GetProperty("result");
        details.GetProperty("externalId").GetString().Should().Be(box.ExternalId.Value);
        details.GetProperty("name").GetString().Should().Be(box.Name);
        details.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        details.GetProperty("rootFolderExternalId").GetString().Should().Be(box.FolderExternalId.Value);
    }

    [Fact]
    public async Task reading_a_box_the_agent_cannot_access_returns_an_error()
    {
        //given — the agent is granted one box, then asks about a different, unshared one
        var (owner, _, agent) = await CreateOwnerBoxAgent();
        var otherBox = await CreateBox(owner);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "get_box_details",
            arguments: new Dictionary<string, object?>
            {
                ["boxExternalId"] = otherBox.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    [Fact]
    public async Task get_box_details_waits_for_approval_when_the_box_override_requires_it()
    {
        //given
        var (owner, box, agent) = await CreateOwnerBoxAgent();
        await Api.Agents.UpdateBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "get_box_details",
            request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var pending = await CallTool(mcp, "get_box_details", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        await Api.Agents.ApproveOperation(
            externalId: agent.ExternalId,
            operationExternalId: approvalRequestId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        var committed = await CallTool(mcp, "execute_operation", new Dictionary<string, object?>
        {
            ["approvalRequestId"] = approvalRequestId
        });

        committed.GetProperty("status").GetString().Should().Be("executed");
        committed.GetProperty("result").GetProperty("externalId").GetString().Should().Be(box.ExternalId.Value);
    }

    [Fact]
    public async Task get_box_details_operation_details_resolve_the_box_id()
    {
        //given
        var (owner, box, agent) = await CreateOwnerBoxAgent();
        await Api.Agents.UpdateBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "get_box_details",
            request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = null, RequiresApproval = true },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        var pending = await CallTool(mcp, "get_box_details", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value
        });
        var approvalRequestId = pending.GetProperty("approvalRequestId").GetString()!;

        //when
        var details = await Api.Agents.GetOperationDetails(approvalRequestId, owner.Cookie);

        //then
        details.GetProperty("$type").GetString().Should().Be("get_box_details");
        details.GetProperty("boxExternalId").GetString().Should().Be(box.ExternalId.Value);
    }

    [Fact]
    public async Task creates_a_folder_inside_the_box_and_lists_it()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var folderExternalId = await CreateBoxFolderViaTool(mcp, box.ExternalId.Value, "Reports");

        //then
        folderExternalId.Should().StartWith("fo_");
        var (folders, _) = await ListBoxContent(mcp, box.ExternalId.Value);
        folders.Should().ContainSingle(f =>
            f.GetProperty("externalId").GetString() == folderExternalId
            && f.GetProperty("name").GetString() == "Reports");
    }

    [Fact]
    public async Task creates_a_file_inside_the_box_and_reads_it_back()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "notes.txt", "hello box");

        //then
        fileExternalId.Should().StartWith("fi_");

        var (_, files) = await ListBoxContent(mcp, box.ExternalId.Value);
        files.Should().ContainSingle(f => f.GetProperty("externalId").GetString() == fileExternalId);

        var read = await CallTool(mcp, "read_box_file", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["fileExternalId"] = fileExternalId
        });
        read.GetProperty("status").GetString().Should().Be("executed");
        read.GetProperty("result").GetProperty("content").GetString().Should().Be("hello box");
        read.GetProperty("result").GetProperty("hasMore").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task renames_a_file_inside_the_box()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "before.txt", "x");

        //when
        var result = await CallTool(mcp, "rename_box_file", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["fileExternalId"] = fileExternalId,
            ["name"] = "after"
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var (_, files) = await ListBoxContent(mcp, box.ExternalId.Value);
        var file = files.Single(f => f.GetProperty("externalId").GetString() == fileExternalId);
        file.GetProperty("name").GetString().Should().Be("after");
        file.GetProperty("extension").GetString().Should().Be(".txt");
    }

    [Fact]
    public async Task renames_a_folder_inside_the_box()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var folderExternalId = await CreateBoxFolderViaTool(mcp, box.ExternalId.Value, "Before");

        //when
        var result = await CallTool(mcp, "rename_box_folder", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["folderExternalId"] = folderExternalId,
            ["name"] = "After"
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var (folders, _) = await ListBoxContent(mcp, box.ExternalId.Value);
        folders.Single(f => f.GetProperty("externalId").GetString() == folderExternalId)
            .GetProperty("name").GetString().Should().Be("After");
    }

    [Fact]
    public async Task moves_a_file_into_a_subfolder()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var folderExternalId = await CreateBoxFolderViaTool(mcp, box.ExternalId.Value, "Archive");
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "moving.txt", "x");

        //when
        var result = await CallTool(mcp, "move_box_items", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["folderExternalIds"] = Array.Empty<string>(),
            ["fileExternalIds"] = new[] { fileExternalId },
            ["destinationFolderExternalId"] = folderExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("movedFileCount").GetInt32().Should().Be(1);

        var (_, rootFiles) = await ListBoxContent(mcp, box.ExternalId.Value);
        rootFiles.Should().NotContain(f => f.GetProperty("externalId").GetString() == fileExternalId);

        var (_, folderFiles) = await ListBoxContent(mcp, box.ExternalId.Value, folderExternalId);
        folderFiles.Should().ContainSingle(f => f.GetProperty("externalId").GetString() == fileExternalId);
    }

    [Fact]
    public async Task creates_a_download_link_for_a_box_file()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "report.txt", "data");

        //when
        var result = await CallTool(mcp, "get_box_file_download_link", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["fileExternalId"] = fileExternalId
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("url").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task creates_a_bulk_download_link_for_box_items()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "a.txt", "data");

        //when
        var result = await CallTool(mcp, "get_box_bulk_download_link", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["fileExternalIds"] = new[] { fileExternalId },
            ["folderExternalIds"] = Array.Empty<string>()
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("url").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task searches_for_a_file_inside_the_box()
    {
        //given
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "quarterly-report.txt", "x");

        //when
        var result = await CallTool(mcp, "search_box", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["phrase"] = "quarterly"
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        var payload = result.GetProperty("result");
        payload.GetProperty("tooManyResults").GetBoolean().Should().BeFalse();
        payload.GetProperty("files").EnumerateArray()
            .Should().ContainSingle(f => f.GetProperty("externalId").GetString() == fileExternalId);
    }

    [Fact]
    public async Task deletes_items_inside_the_box()
    {
        //given — delete is destructive, so turn off its per-box approval to exercise the direct path
        var (owner, box, agent) = await CreateOwnerBoxAgent();
        await Api.Agents.UpdateBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "delete_box_items",
            request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = null, RequiresApproval = false },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "doomed.txt", "x");

        //when
        var result = await CallTool(mcp, "delete_box_items", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["fileExternalIds"] = new[] { fileExternalId },
            ["folderExternalIds"] = Array.Empty<string>()
        });

        //then
        result.GetProperty("status").GetString().Should().Be("executed");
        result.GetProperty("result").GetProperty("deletedFileCount").GetInt32().Should().Be(1);

        var (_, files) = await ListBoxContent(mcp, box.ExternalId.Value);
        files.Should().NotContain(f => f.GetProperty("externalId").GetString() == fileExternalId);
    }

    [Fact]
    public async Task delete_box_items_waits_for_approval_by_default()
    {
        //given — delete_box_items is destructive, so it requires approval out of the box
        var (_, box, agent) = await CreateOwnerBoxAgent();
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);
        var fileExternalId = await CreateBoxFileViaTool(mcp, box.ExternalId.Value, "keep.txt", "x");

        //when
        var pending = await CallTool(mcp, "delete_box_items", new Dictionary<string, object?>
        {
            ["boxExternalId"] = box.ExternalId.Value,
            ["fileExternalIds"] = new[] { fileExternalId },
            ["folderExternalIds"] = Array.Empty<string>()
        });

        //then
        pending.GetProperty("status").GetString().Should().Be("waits_for_approval");
        var (_, files) = await ListBoxContent(mcp, box.ExternalId.Value);
        files.Should().ContainSingle(f => f.GetProperty("externalId").GetString() == fileExternalId);
    }

    [Fact]
    public async Task listing_content_of_a_box_without_access_returns_an_error()
    {
        //given
        var (owner, _, agent) = await CreateOwnerBoxAgent();
        var otherBox = await CreateBox(owner);
        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(
            toolName: "list_box_content",
            arguments: new Dictionary<string, object?>
            {
                ["boxExternalId"] = otherBox.ExternalId.Value
            });

        //then
        result.IsError.Should().Be(true);
    }

    private async Task<string> CreateBoxFolderViaTool(
        McpAgentSession mcp,
        string boxExternalId,
        string name,
        string? parentFolderExternalId = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["boxExternalId"] = boxExternalId,
            ["name"] = name
        };

        if (parentFolderExternalId is not null)
            arguments["parentFolderExternalId"] = parentFolderExternalId;

        var result = await CallTool(mcp, "create_box_folder", arguments);

        result.GetProperty("status").GetString().Should().Be("executed");
        return result.GetProperty("result").GetProperty("folderExternalId").GetString()!;
    }

    private async Task<string> CreateBoxFileViaTool(
        McpAgentSession mcp,
        string boxExternalId,
        string name,
        string content,
        string? folderExternalId = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["boxExternalId"] = boxExternalId,
            ["name"] = name,
            ["content"] = content
        };

        if (folderExternalId is not null)
            arguments["folderExternalId"] = folderExternalId;

        var result = await CallTool(mcp, "create_box_file", arguments);

        result.GetProperty("status").GetString().Should().Be("executed");
        return result.GetProperty("result").GetProperty("fileExternalId").GetString()!;
    }

    private static async Task<(List<JsonElement> Folders, List<JsonElement> Files)> ListBoxContent(
        McpAgentSession mcp,
        string boxExternalId,
        string? folderExternalId = null)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["boxExternalId"] = boxExternalId
        };

        if (folderExternalId is not null)
            arguments["folderExternalId"] = folderExternalId;

        var result = await CallTool(mcp, "list_box_content", arguments);

        result.GetProperty("status").GetString().Should().Be("executed");
        var payload = result.GetProperty("result");
        return (
            payload.GetProperty("folders").EnumerateArray().ToList(),
            payload.GetProperty("files").EnumerateArray().ToList());
    }

    private async Task<(AppSignedInUser Owner, AppBox Box, CreateAgentResponseDto Agent)> CreateOwnerBoxAgent()
    {
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        await WaitForBucketReady(workspace, owner);
        var folder = await CreateFolder(workspace, owner);
        var box = await CreateBox(folder, owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.GrantBoxAccess(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        return (owner, box, agent);
    }

    private async Task<CreateAgentResponseDto> CreateAgentWithoutAccess(AppSignedInUser owner)
    {
        return await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);
    }

    private static async Task<List<JsonElement>> ListBoxes(McpAgentSession mcp)
    {
        var result = await CallTool(mcp, "list_boxes", new Dictionary<string, object?>());

        result.GetProperty("status").GetString().Should().Be("executed");
        return result.GetProperty("result").GetProperty("boxes").EnumerateArray().ToList();
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
