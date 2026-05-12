using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Policy;
using PlikShare.AuditLog.Policy.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.UpdateName.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.AuditLog;

/// <summary>
/// Covers the <c>/api/audit-log/policy</c> endpoint family and the runtime effect of policies on
/// the audit log writer: disabled event types are skipped, severity overrides are applied
/// pre-channel, workspace-defaults are snapshotted at workspace creation (no retroactive change).
/// <para/>
/// Each test mutates the global app-policy and/or workspace-defaults, so <see cref="Dispose"/>
/// restores both to <see cref="PlikShare.AuditLog.Policy.AuditLogPolicy.Empty"/> via
/// <see cref="HostFixture.ResetAuditLogPolicy"/> — same pattern <c>general_settings_tests</c> uses
/// for its globals.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class audit_log_policy_tests : TestFixture, IDisposable
{
    private readonly HostFixture8081 _hostFixture;

    public audit_log_policy_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
        _hostFixture = hostFixture;
    }

    public void Dispose()
    {
        _hostFixture.ResetAuditLogPolicy();
    }

    [Fact]
    public async Task catalog_should_return_event_types_for_both_scopes()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when
        var catalog = await Api.AuditLogPolicy.GetCatalog(cookie: appOwner.Cookie);

        //then
        catalog.Events.Should().NotBeEmpty();
        catalog.Events.Should().Contain(e =>
            e.EventType == AuditLogEventTypes.Auth.SignedIn &&
            e.Scope == AuditLogEventScope.Application);
        catalog.Events.Should().Contain(e =>
            e.EventType == AuditLogEventTypes.File.Downloaded &&
            e.Scope == AuditLogEventScope.Workspace);
    }

    [Fact]
    public async Task app_policy_round_trips_disabled_events()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when
        await Api.AuditLogPolicy.SetAppPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [AuditLogEventTypes.Auth.SignedIn]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        var policy = await Api.AuditLogPolicy.GetAppPolicy(cookie: appOwner.Cookie);

        policy.DisabledEventTypes.Should().BeEquivalentTo([AuditLogEventTypes.Auth.SignedIn]);
        policy.SeverityOverrides.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task app_policy_round_trips_severity_overrides()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when
        await Api.AuditLogPolicy.SetAppPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [],
                SeverityOverrides = new Dictionary<string, string>
                {
                    [AuditLogEventTypes.Auth.SignedIn] = AuditLogSeverities.Critical
                }
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        var policy = await Api.AuditLogPolicy.GetAppPolicy(cookie: appOwner.Cookie);

        policy.DisabledEventTypes.Should().BeEmpty();
        policy.SeverityOverrides.Should().NotBeNull();
        policy.SeverityOverrides!.Should().ContainKey(AuditLogEventTypes.Auth.SignedIn);
        policy.SeverityOverrides![AuditLogEventTypes.Auth.SignedIn].Should().Be(AuditLogSeverities.Critical);
    }

    [Fact]
    public async Task app_policy_with_invalid_severity_returns_400()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when / then
        var act = async () => await Api.AuditLogPolicy.SetAppPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [],
                SeverityOverrides = new Dictionary<string, string>
                {
                    [AuditLogEventTypes.Auth.SignedIn] = "panic"
                }
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        await act.Should().ThrowAsync<TestApiCallException>()
            .Where(e => e.StatusCode == 400);
    }

    [Fact]
    public async Task disabled_app_event_is_not_logged()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        await Api.AuditLogPolicy.SetAppPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [AuditLogEventTypes.Auth.SignedIn]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        // Drain prior sign-in's audit write and wipe — we want to measure ONLY events that fire
        // after the policy is in effect.
        await Task.Delay(500);
        ClearAuditLog();

        //when
        await SignIn(Users.AppOwner);

        //then
        await AssertAuditLogDoesNotContain(
            expectedEventType: AuditLogEventTypes.Auth.SignedIn);
    }

    [Fact]
    public async Task severity_override_changes_recorded_severity()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        await Api.AuditLogPolicy.SetAppPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [],
                SeverityOverrides = new Dictionary<string, string>
                {
                    [AuditLogEventTypes.Auth.SignedIn] = AuditLogSeverities.Critical
                }
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        // Drain prior sign-in's audit write and wipe so the assertion below matches only the
        // entry produced AFTER the override is in effect.
        await Task.Delay(500);
        ClearAuditLog();

        //when
        await SignIn(Users.AppOwner);

        //then
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Auth.SignedIn,
            expectedSeverity: AuditLogSeverities.Critical);
    }

    [Fact]
    public async Task new_workspace_inherits_workspace_default_policy()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        await Api.AuditLogPolicy.SetWorkspaceDefaultPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [AuditLogEventTypes.Workspace.NameUpdated],
                SeverityOverrides = new Dictionary<string, string>
                {
                    [AuditLogEventTypes.File.Downloaded] = AuditLogSeverities.Warning
                }
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //when
        var workspace = await CreateWorkspace(appOwner);

        //then
        var policy = await Api.AuditLogPolicy.GetWorkspacePolicy(
            workspaceExternalId: workspace.ExternalId,
            cookie: appOwner.Cookie);

        policy.DisabledEventTypes.Should().Contain(AuditLogEventTypes.Workspace.NameUpdated);
        policy.SeverityOverrides.Should().NotBeNull();
        policy.SeverityOverrides![AuditLogEventTypes.File.Downloaded].Should().Be(AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task editing_workspace_defaults_does_not_affect_existing_workspaces()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        await Api.AuditLogPolicy.SetWorkspaceDefaultPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [AuditLogEventTypes.File.Downloaded]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        var workspace = await CreateWorkspace(appOwner);

        //when — change defaults to a different event after the workspace already exists
        await Api.AuditLogPolicy.SetWorkspaceDefaultPolicy(
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [AuditLogEventTypes.File.UploadInitiated]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then — existing workspace keeps its snapshot taken at creation time
        var policy = await Api.AuditLogPolicy.GetWorkspacePolicy(
            workspaceExternalId: workspace.ExternalId,
            cookie: appOwner.Cookie);

        policy.DisabledEventTypes.Should().Contain(AuditLogEventTypes.File.Downloaded);
        policy.DisabledEventTypes.Should().NotContain(AuditLogEventTypes.File.UploadInitiated);
    }

    [Fact]
    public async Task workspace_policy_isolates_logging_per_workspace()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        var workspaceA = await CreateWorkspace(appOwner);
        var workspaceB = await CreateWorkspace(appOwner);

        // Disable workspace.name-updated for workspace A only.
        await Api.AuditLogPolicy.SetWorkspacePolicy(
            workspaceExternalId: workspaceA.ExternalId,
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [AuditLogEventTypes.Workspace.NameUpdated]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        // Drain pending writes from CreateWorkspace + policy PUT, wipe the log so only the
        // name-updated events below show up.
        await Task.Delay(500);
        ClearAuditLog();

        //when
        await Api.Workspaces.UpdateName(
            externalId: workspaceA.ExternalId,
            request: new UpdateWorkspaceNameRequestDto(Name: "workspace-a-renamed"),
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        await Api.Workspaces.UpdateName(
            externalId: workspaceB.ExternalId,
            request: new UpdateWorkspaceNameRequestDto(Name: "workspace-b-renamed"),
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        // B's rename produces an entry; A's was filtered out at the policy evaluator.
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Workspace.NameUpdated,
            expectedActorEmail: appOwner.Email);

        var entries = await Api.AuditLog.GetLogs(
            request: new PlikShare.AuditLog.Contracts.GetAuditLogRequestDto
            {
                PageSize = 50,
                EventTypes = [AuditLogEventTypes.Workspace.NameUpdated]
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        entries.Items.Should().AllSatisfy(item =>
            item.EventType.Should().Be(AuditLogEventTypes.Workspace.NameUpdated));
        entries.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task workspace_severity_override_changes_recorded_severity()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(appOwner);

        await Api.AuditLogPolicy.SetWorkspacePolicy(
            workspaceExternalId: workspace.ExternalId,
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes = [],
                SeverityOverrides = new Dictionary<string, string>
                {
                    [AuditLogEventTypes.Workspace.NameUpdated] = AuditLogSeverities.Critical
                }
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        await Task.Delay(500);
        ClearAuditLog();

        //when
        await Api.Workspaces.UpdateName(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceNameRequestDto(Name: "renamed-for-severity-test"),
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //then
        await AssertAuditLogContains(
            expectedEventType: AuditLogEventTypes.Workspace.NameUpdated,
            expectedSeverity: AuditLogSeverities.Critical);
    }

    [Fact]
    public async Task list_workspaces_includes_customization_summary()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(appOwner);

        await Api.AuditLogPolicy.SetWorkspacePolicy(
            workspaceExternalId: workspace.ExternalId,
            request: new AuditLogPolicyDto
            {
                DisabledEventTypes =
                [
                    AuditLogEventTypes.File.Downloaded,
                    AuditLogEventTypes.File.UploadInitiated
                ],
                SeverityOverrides = new Dictionary<string, string>
                {
                    [AuditLogEventTypes.Workspace.NameUpdated] = AuditLogSeverities.Warning
                }
            },
            cookie: appOwner.Cookie,
            antiforgery: appOwner.Antiforgery);

        //when
        var listing = await Api.AuditLogPolicy.ListWorkspaces(cookie: appOwner.Cookie);

        //then
        listing.Workspaces.Should().Contain(w =>
            w.ExternalId == workspace.ExternalId.Value &&
            w.Name == workspace.Name &&
            w.OwnerEmail == appOwner.Email &&
            w.DisabledCount == 2 &&
            w.SeverityOverrideCount == 1);
    }

    [Fact]
    public async Task workspace_policy_get_returns_workspace_name()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        var workspace = await CreateWorkspace(appOwner);

        //when
        var policy = await Api.AuditLogPolicy.GetWorkspacePolicy(
            workspaceExternalId: workspace.ExternalId,
            cookie: appOwner.Cookie);

        //then
        policy.WorkspaceExternalId.Should().Be(workspace.ExternalId.Value);
        policy.WorkspaceName.Should().Be(workspace.Name);
    }

    [Fact]
    public async Task get_policy_for_non_existent_workspace_returns_404()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);

        //when / then
        var act = async () => await Api.AuditLogPolicy.GetWorkspacePolicy(
            workspaceExternalId: WorkspaceExtId.NewId(),
            cookie: appOwner.Cookie);

        await act.Should().ThrowAsync<TestApiCallException>()
            .Where(e => e.StatusCode == 404);
    }

    [Fact]
    public async Task non_admin_cannot_access_policy_endpoints()
    {
        //given
        var appOwner = await SignIn(Users.AppOwner);
        var regularUser = await InviteAndRegisterUser(appOwner);

        //when / then
        var act = async () => await Api.AuditLogPolicy.GetCatalog(cookie: regularUser.Cookie);

        await act.Should().ThrowAsync<TestApiCallException>()
            .Where(e => e.StatusCode == 403);
    }
}
