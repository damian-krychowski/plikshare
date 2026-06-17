using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Agents;

[Collection(IntegrationTestsCollection.Name)]
public class agent_tool_config_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public agent_tool_config_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task returns_the_full_tool_catalog_with_sensible_defaults()
    {
        //given
        var agent = await CreateAgent();

        //when
        var result = await Api.Agents.GetTools(agent.ExternalId, AppOwner.Cookie);

        //then
        result.Tools.Should().HaveCount(21);

        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");
        bulkDelete.IsEnabled.Should().BeTrue();
        bulkDelete.RequiresApproval.Should().BeTrue();
        bulkDelete.IsDefault.Should().BeTrue();
        bulkDelete.IsAvailable.Should().BeTrue();
        bulkDelete.Scope.Should().Be("workspace");

        var createFile = result.Tools.Single(t => t.Name == "create_file");
        createFile.RequiresApproval.Should().BeFalse();

        var createWorkspace = result.Tools.Single(t => t.Name == "create_workspace");
        createWorkspace.RequiredPermission.Should().Be("add_workspace");
        createWorkspace.IsAvailable.Should().BeFalse();
        createWorkspace.Scope.Should().Be("instance");

        var listWorkspaces = result.Tools.Single(t => t.Name == "list_workspaces");
        listWorkspaces.IsAvailable.Should().BeTrue();
        listWorkspaces.RequiredPermission.Should().BeNull();
    }

    [Fact]
    public async Task disabling_a_tool_is_reflected_in_the_config()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentToolConfigRequestDto { IsEnabled = false, RequiresApproval = true },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetTools(agent.ExternalId, AppOwner.Cookie);
        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");

        bulkDelete.IsEnabled.Should().BeFalse();
        bulkDelete.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task changing_approval_is_reflected_in_the_config()
    {
        //given
        var agent = await CreateAgent();

        //when — turn on approval for a tool that defaults to no approval
        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "create_file",
            request: new UpdateAgentToolConfigRequestDto { IsEnabled = true, RequiresApproval = true },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetTools(agent.ExternalId, AppOwner.Cookie);
        var createFile = result.Tools.Single(t => t.Name == "create_file");

        createFile.RequiresApproval.Should().BeTrue();
        createFile.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task resetting_a_tool_restores_the_catalog_default()
    {
        //given
        var agent = await CreateAgent();

        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentToolConfigRequestDto { IsEnabled = false, RequiresApproval = false },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Agents.ResetToolConfig(
            externalId: agent.ExternalId,
            toolName: "bulk_delete",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetTools(agent.ExternalId, AppOwner.Cookie);
        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");

        bulkDelete.IsDefault.Should().BeTrue();
        bulkDelete.IsEnabled.Should().BeTrue();
        bulkDelete.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task granting_add_workspace_permission_makes_create_workspace_available()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.UpdatePermissionsAndRoles(
            externalId: agent.ExternalId,
            request: new UpdateAgentPermissionsAndRolesRequestDto
            {
                IsAdmin = false,
                CanAddWorkspace = true,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageAuditLog = false
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetTools(agent.ExternalId, AppOwner.Cookie);
        result.Tools.Single(t => t.Name == "create_workspace").IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task updating_an_unknown_tool_returns_bad_request()
    {
        //given
        var agent = await CreateAgent();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.UpdateToolConfig(
                externalId: agent.ExternalId,
                toolName: "not_a_real_tool",
                request: new UpdateAgentToolConfigRequestDto { IsEnabled = false, RequiresApproval = false },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("unknown-tool");
    }

    [Fact]
    public async Task updating_a_tool_config_writes_an_audit_log_entry()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.UpdateToolConfig(
            externalId: agent.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentToolConfigRequestDto { IsEnabled = false, RequiresApproval = true },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains(AuditLogEventTypes.Agent.ToolConfigUpdated);
    }

    private async Task<CreateAgentResponseDto> CreateAgent()
    {
        return await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }
}
