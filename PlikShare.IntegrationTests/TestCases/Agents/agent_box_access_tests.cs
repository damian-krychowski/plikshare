using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Tools.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Agents;

[Collection(IntegrationTestsCollection.Name)]
public class agent_box_access_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public agent_box_access_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task inviting_agent_to_a_box_makes_it_appear_in_shared_boxes()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        //when
        await Api.Agents.GrantBoxAccess(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(agent.ExternalId, AppOwner.Cookie);

        var sharedBox = details.SharedBoxes.Should().ContainSingle(b => b.BoxExternalId == box.ExternalId).Which;
        sharedBox.OverriddenToolsCount.Should().Be(0);
    }

    [Fact]
    public async Task revoking_box_access_removes_it_from_shared_boxes()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        await Api.Agents.GrantBoxAccess(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Agents.RevokeBoxAccess(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(agent.ExternalId, AppOwner.Cookie);
        details.SharedBoxes.Should().NotContain(b => b.BoxExternalId == box.ExternalId);
    }

    [Fact]
    public async Task inviting_to_a_non_existent_box_returns_not_found()
    {
        //given
        var agent = await CreateAgent();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.GrantBoxAccess(
                externalId: agent.ExternalId,
                boxExternalId: BoxExtId.NewId(),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task list_workspace_boxes_returns_the_workspace_boxes()
    {
        //given
        var box = await CreateBox(AppOwner);

        //when
        var response = await Api.Agents.ListWorkspaceBoxes(
            workspaceExternalId: box.WorkspaceExternalId,
            cookie: AppOwner.Cookie);

        //then
        response.Items.Should().Contain(b => b.ExternalId == box.ExternalId);
    }

    [Fact]
    public async Task box_tools_return_only_overridable_tools_inheriting_global_by_default()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        //when
        var result = await Api.Agents.GetBoxTools(agent.ExternalId, box.ExternalId, AppOwner.Cookie);

        //then
        result.Tools.Should().HaveCount(18);
        result.Tools.Should().NotContain(t => t.Name == "list_workspaces");

        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");
        bulkDelete.OverrideIsEnabled.Should().BeNull();
        bulkDelete.OverrideRequiresApproval.Should().BeNull();
        bulkDelete.EffectiveIsEnabled.Should().Be(bulkDelete.GlobalIsEnabled);
        bulkDelete.EffectiveRequiresApproval.Should().Be(bulkDelete.GlobalRequiresApproval);
    }

    [Fact]
    public async Task box_override_overrides_one_dimension_and_inherits_the_other()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        //when — disable bulk_delete in this box, leave approval inheriting the global default (true)
        await Api.Agents.UpdateBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetBoxTools(agent.ExternalId, box.ExternalId, AppOwner.Cookie);
        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");

        bulkDelete.OverrideIsEnabled.Should().BeFalse();
        bulkDelete.OverrideRequiresApproval.Should().BeNull();
        bulkDelete.EffectiveIsEnabled.Should().BeFalse();
        bulkDelete.EffectiveRequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task resetting_a_box_override_reverts_to_global()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        await Api.Agents.UpdateBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = false, RequiresApproval = false },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Agents.ResetBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "bulk_delete",
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var result = await Api.Agents.GetBoxTools(agent.ExternalId, box.ExternalId, AppOwner.Cookie);
        var bulkDelete = result.Tools.Single(t => t.Name == "bulk_delete");

        bulkDelete.OverrideIsEnabled.Should().BeNull();
        bulkDelete.OverrideRequiresApproval.Should().BeNull();
        bulkDelete.EffectiveIsEnabled.Should().Be(bulkDelete.GlobalIsEnabled);
    }

    [Fact]
    public async Task box_override_is_reflected_in_the_overridden_tools_count()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        await Api.Agents.GrantBoxAccess(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Agents.UpdateBoxToolOverride(
            externalId: agent.ExternalId,
            boxExternalId: box.ExternalId,
            toolName: "bulk_delete",
            request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(agent.ExternalId, AppOwner.Cookie);
        var sharedBox = details.SharedBoxes.Single(b => b.BoxExternalId == box.ExternalId);
        sharedBox.OverriddenToolsCount.Should().Be(1);
    }

    [Fact]
    public async Task overriding_an_instance_tool_per_box_returns_bad_request()
    {
        //given
        var agent = await CreateAgent();
        var box = await CreateBox(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.UpdateBoxToolOverride(
                externalId: agent.ExternalId,
                boxExternalId: box.ExternalId,
                toolName: "list_workspaces",
                request: new UpdateAgentBoxToolOverrideRequestDto { IsEnabled = false, RequiresApproval = null },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError!.Code.Should().Be("not-a-box-tool");
    }

    private async Task<CreateAgentResponseDto> CreateAgent()
    {
        return await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }
}
