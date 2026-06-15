using System.Net.Http.Headers;
using FluentAssertions;
using ModelContextProtocol.Client;
using PlikShare.Agents.Create.Contracts;
using PlikShare.AuditLog;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Mcp;

[Collection(IntegrationTestsCollection.Name)]
public class mcp_create_folder_tests : TestFixture
{
    public mcp_create_folder_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Fact]
    public async Task agent_connects_over_mcp_and_creates_a_folder()
    {
        //given
        var owner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(owner);

        var agent = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        await Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: owner.Cookie,
            antiforgery: owner.Antiforgery);

        using var httpHandler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        using var httpClient = new HttpClient(httpHandler);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", agent.Token);

        await using var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri($"{AppUrl}/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp
            },
            httpClient);

        //when
        await using var mcpClient = await McpClient.CreateAsync(transport);

        var tools = await mcpClient.ListToolsAsync();

        //then
        tools.Select(t => t.Name).Should().Contain("list_workspaces");
        tools.Select(t => t.Name).Should().Contain("create_folder");

        //and when - agent discovers its workspaces (this is how it learns the workspaceExternalId)
        var listResult = await mcpClient.CallToolAsync(
            toolName: "list_workspaces");

        //then
        listResult.IsError.Should().NotBe(true);

        await AssertAuditLogContains(AuditLogEventTypes.Agent.WorkspacesListed);

        //and when
        const string folderName = "agent-created-folder";

        var result = await mcpClient.CallToolAsync(
            toolName: "create_folder",
            arguments: new Dictionary<string, object?>
            {
                ["workspaceExternalId"] = workspace.ExternalId.Value,
                ["name"] = folderName
            });

        //then
        result.IsError.Should().NotBe(true);

        var creator = GetFolderCreator(workspace.ExternalId.Value, folderName);

        creator.Should().NotBeNull();
        creator!.Value.IdentityType.Should().Be(AgentIdentity.Type);
        creator.Value.Identity.Should().Be(agent.ExternalId.Value);
    }

    private (string IdentityType, string Identity)? GetFolderCreator(
        string workspaceExternalId,
        string folderName)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT fo_creator_identity_type, fo_creator_identity
                     FROM fo_folders
                     INNER JOIN w_workspaces ON w_id = fo_workspace_id
                     WHERE w_external_id = $workspaceExternalId
                         AND fo_name = $folderName
                     LIMIT 1
                     """,
                readRowFunc: reader => (
                    IdentityType: reader.GetString(0),
                    Identity: reader.GetString(1)))
            .WithParameter("$workspaceExternalId", workspaceExternalId)
            .WithParameter("$folderName", folderName)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
}
