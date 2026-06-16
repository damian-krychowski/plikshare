using System.Text;
using System.Text.Json;
using FluentAssertions;
using ModelContextProtocol.Protocol;
using PlikShare.Agents.Create.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_list_workspaces_tests : TestFixture
{
    public mcp_list_workspaces_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task lists_accessible_workspace_with_its_current_size()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);
        var folder = await CreateFolder(workspace, owner);

        await UploadFile(
            content: Encoding.UTF8.GetBytes("hello world content for size"),
            fileName: "f.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await using var mcp = await Api.Mcp.ConnectAsAgent(agent.Token);

        //when
        var result = await mcp.Client.CallToolAsync(toolName: "list_workspaces");
        result.IsError.Should().NotBe(true);

        var text = result.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();

        text.Should().NotBeNullOrEmpty();

        using var document = JsonDocument.Parse(text!);
        var json = document.RootElement;

        //then
        var entry = json.GetProperty("workspaces").EnumerateArray()
            .Single(w => w.GetProperty("workspaceExternalId").GetString() == workspace.ExternalId.Value);

        entry.GetProperty("name").GetString().Should().Be(workspace.Name);
        entry.GetProperty("currentSizeInBytes").GetInt64().Should().BeGreaterThan(0);
    }
}
