using FluentAssertions;
using Microsoft.AspNetCore.Http;
using PlikShare.Agents.Create.Contracts;
using PlikShare.Agents.Id;
using PlikShare.Agents.UpdateSettings.Contracts;
using PlikShare.AuditLog;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Users.StorageAccess;
using PlikShare.Workspaces.Id;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Agents;

[Collection(IntegrationTestsCollection.Name)]
public class agents_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public agents_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(
            user: Users.AppOwner).Result;
    }

    [Fact]
    public async Task when_agent_is_created_it_is_visible_on_the_list_with_a_token()
    {
        //given
        var agentName = Random.Name("Agent");

        //when
        var response = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = agentName },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.Token.Should().StartWith("psh_agt_");
        response.TokenMasked.Should().StartWith("psh_agt_");

        var agents = await Api.Agents.Get(
            cookie: AppOwner.Cookie);

        agents.Items.Should().Contain(a =>
            a.ExternalId == response.ExternalId &&
            a.Name == agentName &&
            a.IsEnabled);
    }

    [Fact]
    public async Task when_agent_is_created_its_details_default_to_creator_owner_and_no_workspaces()
    {
        //given
        var agent = await CreateAgent();

        //when
        var details = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        details.Agent.Owner.ExternalId.Should().Be(AppOwner.ExternalId);
        details.Agent.Owner.Email.Should().Be(AppOwner.Email);

        details.Agent.IsEnabled.Should().BeTrue();
        details.Agent.TokenMasked.Should().StartWith("psh_agt_");
        details.Agent.TokenLastUsedAt.Should().BeNull();

        details.Agent.MaxWorkspaceNumber.Should().BeNull();
        details.Agent.DefaultMaxWorkspaceSizeInBytes.Should().BeNull();
        details.Agent.DefaultMaxWorkspaceTeamMembers.Should().BeNull();

        details.Agent.StorageAccess.Mode.Should().Be(UserStorageAccessMode.All);
        details.Agent.StorageAccess.StorageExternalIds.Should().BeEmpty();

        details.OwnedWorkspaces.Should().BeEmpty();
        details.SharedWorkspaces.Should().BeEmpty();
    }

    [Fact]
    public async Task when_agent_is_deleted_it_is_removed_from_the_list()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.Delete(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var agents = await Api.Agents.Get(
            cookie: AppOwner.Cookie);

        agents.Items.Should().NotContain(a => a.ExternalId == agent.ExternalId);
    }

    [Fact]
    public async Task when_token_is_rotated_the_masked_token_changes()
    {
        //given
        var agent = await CreateAgent();

        var before = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        //when
        var rotated = await Api.Agents.RotateToken(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        rotated.Token.Should().StartWith("psh_agt_");
        rotated.Token.Should().NotBe(agent.Token);

        var after = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        after.Agent.TokenMasked.Should().Be(rotated.TokenMasked);
        after.Agent.TokenMasked.Should().NotBe(before.Agent.TokenMasked);
    }

    [Fact]
    public async Task when_limits_are_updated_they_are_reflected_in_details()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.UpdateMaxWorkspaceNumber(
            externalId: agent.ExternalId,
            request: new UpdateAgentMaxWorkspaceNumberRequestDto { MaxWorkspaceNumber = 7 },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Agents.UpdateDefaultMaxWorkspaceSize(
            externalId: agent.ExternalId,
            request: new UpdateAgentDefaultMaxWorkspaceSizeRequestDto { MaxSizeInBytes = 1024 },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Agents.UpdateDefaultMaxWorkspaceTeamMembers(
            externalId: agent.ExternalId,
            request: new UpdateAgentDefaultMaxWorkspaceTeamMembersRequestDto { MaxTeamMembers = 3 },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        details.Agent.MaxWorkspaceNumber.Should().Be(7);
        details.Agent.DefaultMaxWorkspaceSizeInBytes.Should().Be(1024);
        details.Agent.DefaultMaxWorkspaceTeamMembers.Should().Be(3);
    }

    [Fact]
    public async Task when_storage_access_is_updated_it_is_reflected_in_details()
    {
        //given
        var agent = await CreateAgent();
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None);

        //when
        await Api.Agents.UpdateStorageAccess(
            externalId: agent.ExternalId,
            request: new UpdateAgentStorageAccessRequestDto
            {
                Mode = UserStorageAccessMode.AllowOnly,
                StorageExternalIds = [storage.ExternalId.Value]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        details.Agent.StorageAccess.Mode.Should().Be(UserStorageAccessMode.AllowOnly);
        details.Agent.StorageAccess.StorageExternalIds.Should().BeEquivalentTo([storage.ExternalId.Value]);
    }

    [Fact]
    public async Task updating_storage_access_with_unknown_storage_returns_bad_request()
    {
        //given
        var agent = await CreateAgent();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.UpdateStorageAccess(
                externalId: agent.ExternalId,
                request: new UpdateAgentStorageAccessRequestDto
                {
                    Mode = UserStorageAccessMode.AllowOnly,
                    StorageExternalIds = [StorageExtId.NewId().Value]
                },
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        apiError.HttpError.Should().NotBeNull();
        apiError.HttpError!.Code.Should().Be("unknown-storage-external-ids");
    }

    [Fact]
    public async Task when_workspace_access_is_granted_it_appears_in_shared_workspaces()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        //when
        await Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        details.SharedWorkspaces.Should().Contain(w => w.ExternalId == workspace.ExternalId);
    }

    [Fact]
    public async Task when_workspace_access_is_revoked_it_is_removed_from_shared_workspaces()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        await Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Agents.RevokeWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Agents.GetDetails(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie);

        details.SharedWorkspaces.Should().NotContain(w => w.ExternalId == workspace.ExternalId);
    }

    [Fact]
    public async Task granting_workspace_access_to_non_existent_agent_returns_not_found()
    {
        //given
        var workspace = await CreateWorkspace(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.GrantWorkspaceAccess(
                externalId: AgentExtId.NewId(),
                workspaceExternalId: workspace.ExternalId,
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        apiError.HttpError!.Code.Should().Be("agent-doesnt-exist");
    }

    [Fact]
    public async Task granting_non_existent_workspace_access_returns_not_found()
    {
        //given
        var agent = await CreateAgent();

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.GrantWorkspaceAccess(
                externalId: agent.ExternalId,
                workspaceExternalId: WorkspaceExtId.NewId(),
                cookie: AppOwner.Cookie,
                antiforgery: AppOwner.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task agent_appears_in_owner_user_details_owned_agents()
    {
        //given
        var agent = await CreateAgent();

        //when
        var userDetails = await Api.Users.GetDetails(
            userExternalId: AppOwner.ExternalId,
            cookie: AppOwner.Cookie);

        //then
        userDetails.OwnedAgents.Should().Contain(a =>
            a.ExternalId == agent.ExternalId);
    }

    [Fact]
    public async Task non_admin_user_cannot_create_agent()
    {
        //given
        var regularUser = await InviteAndRegisterUser(AppOwner);

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.Create(
                request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
                cookie: regularUser.Cookie,
                antiforgery: regularUser.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task creating_agent_produces_audit_log_entry()
    {
        //given
        var agentName = Random.Name("Agent");

        //when
        var response = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = agentName },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Agent.Created>(
            expectedEventType: AuditLogEventTypes.Agent.Created,
            assertDetails: details =>
            {
                details.Agent.ExternalId.Should().Be(response.ExternalId);
                details.Agent.Name.Should().Be(agentName);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deleting_agent_produces_audit_log_entry()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.Delete(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Agent.Deleted>(
            expectedEventType: AuditLogEventTypes.Agent.Deleted,
            assertDetails: details => details.Agent.ExternalId.Should().Be(agent.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task rotating_token_produces_audit_log_entry()
    {
        //given
        var agent = await CreateAgent();

        //when
        await Api.Agents.RotateToken(
            externalId: agent.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Agent.TokenRotated>(
            expectedEventType: AuditLogEventTypes.Agent.TokenRotated,
            assertDetails: details => details.Agent.ExternalId.Should().Be(agent.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task granting_workspace_access_produces_audit_log_entry()
    {
        //given
        var agent = await CreateAgent();
        var workspace = await CreateWorkspace(AppOwner);

        //when
        await Api.Agents.GrantWorkspaceAccess(
            externalId: agent.ExternalId,
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Agent.WorkspaceAccessGranted>(
            expectedEventType: AuditLogEventTypes.Agent.WorkspaceAccessGranted,
            assertDetails: details =>
            {
                details.Agent.ExternalId.Should().Be(agent.ExternalId);
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task admin_with_manage_agents_permission_can_create_agent()
    {
        //given
        var admin = await InviteAndRegisterUser(AppOwner);
        await GrantUserPermissions(admin, isAdmin: true, canManageAgents: true);
        var adminSignedIn = await SignIn(new User(admin.Email, admin.Password));

        //when
        var response = await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: adminSignedIn.Cookie,
            antiforgery: adminSignedIn.Antiforgery);

        //then
        response.ExternalId.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task admin_without_manage_agents_permission_cannot_create_agent()
    {
        //given
        var admin = await InviteAndRegisterUser(AppOwner);
        await GrantUserPermissions(admin, isAdmin: true, canManageAgents: false);
        var adminSignedIn = await SignIn(new User(admin.Email, admin.Password));

        //when
        var apiError = await Assert.ThrowsAsync<TestApiCallException>(
            async () => await Api.Agents.Create(
                request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
                cookie: adminSignedIn.Cookie,
                antiforgery: adminSignedIn.Antiforgery));

        //then
        apiError.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task granting_manage_agents_permission_is_reflected_in_account_details()
    {
        //given
        var admin = await InviteAndRegisterUser(AppOwner);
        await GrantUserPermissions(admin, isAdmin: true, canManageAgents: true);
        var adminSignedIn = await SignIn(new User(admin.Email, admin.Password));

        //when
        var details = await Api.Account.GetDetails(cookie: adminSignedIn.Cookie);

        //then
        details.Permissions.CanManageAgents.Should().BeTrue();
    }

    private async Task GrantUserPermissions(AppSignedInUser target, bool isAdmin, bool canManageAgents)
    {
        await Api.Users.UpdatePermissionsAndRoles(
            userExternalId: target.ExternalId,
            request: new UserPermissionsAndRolesDto
            {
                IsAdmin = isAdmin,
                CanAddWorkspace = false,
                CanManageGeneralSettings = false,
                CanManageUsers = false,
                CanManageStorages = false,
                CanManageEmailProviders = false,
                CanManageAuth = false,
                CanManageIntegrations = false,
                CanManageAuditLog = false,
                CanManageAgents = canManageAgents
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }

    private async Task<CreateAgentResponseDto> CreateAgent()
    {
        return await Api.Agents.Create(
            request: new CreateAgentRequestDto { Name = Random.Name("Agent") },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
    }
}
