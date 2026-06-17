using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Workspaces.Id;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Agents;

[Collection(IntegrationTestsCollection.Name)]
public class agent_workspace_tool_override_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public agent_workspace_tool_override_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task returns_only_workspace_scoped_tools_inheriting_global_by_default()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        //when
        var result = await Api.Agents.GetWorkspaceTools(agent.ExternalId, workspace.ExternalId, AppOwner.Cookie);

        //then
        result.Tools.Should().HaveCount(14);
        result.Tools.Should().NotContain(t => t.Name == "list_workspaces");
        result.Tools.Should().Contain(t => t.Name == "bulk_delete");

        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");
        bulkDelete.OverrideIsEnabled.Should().BeNull();
        bulkDelete.OverrideRequiresApproval.Should().BeNull();
        bulkDelete.EffectiveIsEnabled.Should().Be(bulkDelete.GlobalIsEnabled);
        bulkDelete.EffectiveRequiresApproval.Should().Be(bulkDelete.GlobalRequiresApproval);
    }

    [Fact]
    public async Task overriding_one_dimension_keeps_the_other_inherited()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        //when — disable bulk_delete here, but leave approval inheriting the global default (true)
        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetWorkspaceTools(agent.ExternalId, workspace.ExternalId, AppOwner.Cookie);
        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");

        bulkDelete.OverrideIsEnabled.Should().BeFalse();
        bulkDelete.OverrideRequiresApproval.Should().BeNull();
        bulkDelete.EffectiveIsEnabled.Should().BeFalse();
        bulkDelete.EffectiveRequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task clearing_a_workspace_override_reverts_to_global()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = false },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Agents.ResetWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "bulk_delete",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetWorkspaceTools(agent.ExternalId, workspace.ExternalId, AppOwner.Cookie);
        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");

        bulkDelete.OverrideIsEnabled.Should().BeNull();
        bulkDelete.OverrideRequiresApproval.Should().BeNull();
        bulkDelete.EffectiveIsEnabled.Should().Be(bulkDelete.GlobalIsEnabled);
    }

    [Fact]
    public async Task overriding_an_instance_tool_returns_bad_request()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.UpdateWorkspaceToolOverride(
                externalId: agent.ExternalId,
                workspaceExternalId: workspace.ExternalId,
                toolName: "list_workspaces",
                request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("not-a-workspace-tool");
    }

    [Fact]
    public async Task overriding_in_an_unknown_workspace_returns_not_found()
    {
        //given
        var agent = await CreateAgent();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.UpdateWorkspaceToolOverride(
                externalId: agent.ExternalId,
                workspaceExternalId: WorkspaceExtId.NewId(),
                toolName: "bulk_delete",
                request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task updating_a_workspace_override_writes_an_audit_log_entry()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        //when
        await Api.Agents.UpdateWorkspaceToolOverride(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentWorkspaceToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains(AuditLogEventTypes.Agent.ToolWorkspaceOverrideUpdated);
    }

    private async Task<CreateAgentResponseDto> CreateAgent()
    {
        return await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }
}
