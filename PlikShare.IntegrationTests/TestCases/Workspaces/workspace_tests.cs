using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlikShare.AuditLog;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Dashboard.Content.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Integrations.Create.Contracts;
using PlikShare.Integrations.Id;
using PlikShare.QuickShares;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.List.Contracts;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Delete.QueueJob;
using PlikShare.Workspaces.Get.Contracts;
using PlikShare.Workspaces.Id;
using PlikShare.Workspaces.Members.CreateInvitation.Contracts;
using PlikShare.Workspaces.Members.UpdatePermissions.Contracts;
using PlikShare.Workspaces.Permissions;
using PlikShare.Workspaces.UpdateMaxSize.Contracts;
using PlikShare.Workspaces.UpdateMaxTeamMembers.Contracts;
using PlikShare.Workspaces.UpdateName.Contracts;
using Xunit.Abstractions;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class workspace_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public workspace_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(
            user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    // --- Functional tests ---

    [Fact]
    public async Task when_workspace_is_created_it_should_be_visible_on_the_dashboard()
    {
        //given
        var workspaceName = Random.Name("Workspace");

        //when
        var workspaceResponse = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: Storage.ExternalId,
                Name: workspaceName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var dashboard = await Api.Dashboard.Get(
            cookie: AppOwner.Cookie);

        dashboard.Workspaces.Should().ContainEquivalentOf(new GetDashboardContentResponseDto.WorkspaceDetails
        {
            ExternalId = workspaceResponse.ExternalId.Value,
            CurrentSizeInBytes = 0,
            Name = workspaceName,
            Owner = new GetDashboardContentResponseDto.User
            {
                ExternalId = AppOwner.ExternalId.Value,
                Email = AppOwner.Email,
            },
            Permissions = new GetDashboardContentResponseDto.WorkspacePermissions
            {
                AllowShare = true,
            },
            StorageName = Storage.Name,
            StorageExternalId = Storage.ExternalId.Value,
            StorageEncryptionType = "none",
            IsBucketCreated = false,
            IsUsedByIntegration = false,
            IsPendingKeyGrant = false,
            MaxSizeInBytes = -1,
        });
    }

    [Fact]
    public async Task when_workspace_is_created_its_details_should_be_available()
    {
        //given
        var workspaceName = Random.Name("Workspace");

        //when
        var workspaceResponse = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: Storage.ExternalId,
                Name: workspaceName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Workspaces.GetDetails(
            externalId: workspaceResponse.ExternalId,
            cookie: AppOwner.Cookie);

        details.Should().BeEquivalentTo(new GetWorkspaceDetailsResponseDto
        {
            ExternalId = workspaceResponse.ExternalId,
            Name = workspaceName,
            CurrentSizeInBytes = 0,
            CurrentBoxesTeamMembersCount = 0,
            CurrentTeamMembersCount = 0,
            Owner = new WorkspaceOwnerDto
            {
                ExternalId = AppOwner.ExternalId,
                Email = AppOwner.Email
            },
            PendingUploadsCount = 0,
            Permissions = new WorkspacePermissions(
                AllowShare: true),
            Integrations = new WorkspaceIntegrationsDto
            {
                ChatGpt = [],
                Textract = null
            },
            IsBucketCreated = false,
            MaxSizeInBytes = null,
            MaxTeamMembers = null,
            StorageEncryptionType = "none",
            TrashPolicy = TrashPolicyDto.Disabled
        });
    }

    [Fact]
    public async Task when_workspace_is_deleted_it_should_not_be_on_the_dashboard()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        await Api.Workspaces.Delete(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var dashboard = await Api.Dashboard.Get(
            cookie: AppOwner.Cookie);

        (dashboard.Workspaces ?? []).Should().NotContain(w =>
            w.ExternalId == workspace.ExternalId.Value);
    }

    // qsh_quick_shares.qsh_workspace_id is a NOT NULL FK to w_workspaces with NO CASCADE,
    // and DeleteWorkspaceWithDependenciesQuery never deletes those rows. Because the query
    // runs with PRAGMA defer_foreign_keys = ON, the FK violation only fires at COMMIT time;
    // the transaction is then rolled back and the workspace stays in w_workspaces despite
    // the API having reported a successful delete schedule.
    [Fact]
    public async Task when_workspace_with_quick_share_is_deleted_it_should_be_removed_from_db()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("hello"),
            fileName: "hello.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await Api.QuickShares.Create(
            workspaceExternalId: workspace.ExternalId,
            name: "share",
            customSlug: null,
            selectedFiles: [file.ExternalId],
            selectedFolders: [],
            excludedFiles: [],
            excludedFolders: [],
            mode: QuickShareMode.Browser,
            allowIndividualFileDownload: true,
            expiresAt: null,
            password: null,
            maxDownloads: null,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.Delete(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await WaitFor(() =>
            WorkspaceRowExists(workspace.ExternalId).Should().BeFalse(
                "the delete-workspace job must succeed in removing the workspace from w_workspaces; " +
                "with qsh_quick_shares left dangling the deferred FK check fires at COMMIT, " +
                "the whole transaction rolls back, and the workspace stays in place"));
    }

    // i_integrations.i_workspace_id is a NULLable FK to w_workspaces with NO CASCADE/SET NULL,
    // and DeleteWorkspaceWithDependenciesQuery never deletes or nulls those rows. The
    // user-facing API blocks this path via ScheduleWorkspaceDeleteQuery.IsWorkspaceUsedByIntegration,
    // so this test bypasses the schedule layer and invokes DeleteWorkspaceWithDependenciesQuery
    // directly via DI to expose the underlying defense-in-depth gap.
    [Fact]
    public async Task delete_workspace_query_should_handle_workspace_bound_to_integration()
    {
        //given
        var integration = await Api.Integrations.Create(
            request: new CreateAwsTextractIntegrationRequestDto
            {
                Name = Random.Name("Textract"),
                StorageExternalId = Storage.ExternalId,
                AccessKey = Random.ClientId(),
                SecretAccessKey = Random.ClientSecret(),
                Region = "us-east-1"
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var (integrationWorkspaceInternalId, integrationWorkspaceExternalId) =
            GetIntegrationWorkspace(integration.ExternalId);

        var dbWriteQueue = HostFixture.App.Services.GetRequiredService<DbWriteQueue>();
        var deleteQuery = HostFixture.App.Services.GetRequiredService<DeleteWorkspaceWithDependenciesQuery>();

        //when
        Func<Task> runDeleteQuery = () => dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                deleteQuery.Execute(
                    workspaceId: integrationWorkspaceInternalId,
                    deletedAt: Clock.UtcNow,
                    correlationId: Guid.NewGuid(),
                    dbWriteContext: context,
                    transaction: transaction);

                transaction.Commit();
            },
            cancellationToken: CancellationToken.None);

        //then
        await runDeleteQuery.Should().NotThrowAsync(
            "DeleteWorkspaceWithDependenciesQuery must clean up i_integrations rows referencing " +
            "the workspace; leaving them in place produces a FOREIGN KEY violation on COMMIT");

        WorkspaceRowExists(integrationWorkspaceExternalId).Should().BeFalse(
            "the workspace bound to the integration must be physically removed from w_workspaces");
    }

    private bool WorkspaceRowExists(WorkspaceExtId externalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .Cmd(
                sql: "SELECT 1 FROM w_workspaces WHERE w_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", externalId.Value)
            .Execute()
            .Count > 0;
    }

    private (int InternalId, WorkspaceExtId ExternalId) GetIntegrationWorkspace(
        IntegrationExtId integrationExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT w.w_id, w.w_external_id
                     FROM w_workspaces w
                     JOIN i_integrations i ON i.i_workspace_id = w.w_id
                     WHERE i.i_external_id = $integrationExternalId
                     LIMIT 1
                     """,
                readRowFunc: reader => (reader.GetInt32(0), reader.GetExtId<WorkspaceExtId>(1)))
            .WithParameter("$integrationExternalId", integrationExternalId.Value)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"No workspace bound to integration '{integrationExternalId}' was found.");

        return rows[0];
    }

    [Fact]
    public async Task when_workspace_name_is_updated_it_should_be_reflected_in_details()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var newName = Random.Name("RenamedWorkspace");

        //when
        await Api.Workspaces.UpdateName(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceNameRequestDto(
                Name: newName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Workspaces.GetDetails(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        details.Name.Should().Be(newName);
    }

    [Fact]
    public async Task when_member_is_invited_it_should_be_visible_on_members_list()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        //when
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var members = await Api.Workspaces.GetMembers(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        members.Items.Should().Contain(m =>
            m.MemberEmail == member.Email);
    }

    [Fact]
    public async Task when_member_is_revoked_it_should_not_be_on_members_list()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.RevokeMember(
            externalId: workspace.ExternalId,
            memberExternalId: member.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var members = await Api.Workspaces.GetMembers(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        members.Items.Should().NotContain(m =>
            m.MemberEmail == member.Email);
    }

    // --- Audit log tests ---

    [Fact]
    public async Task creating_workspace_should_produce_audit_log_entry()
    {
        //given
        var workspaceName = Random.Name("Workspace");

        //when
        var response = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: Storage.ExternalId,
                Name: workspaceName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.Created>(
            expectedEventType: AuditLogEventTypes.Workspace.Created,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(response.ExternalId);
                details.Workspace.Name.Should().Be(workspaceName);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deleting_workspace_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        await Api.Workspaces.Delete(
            externalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.Deleted>(
            expectedEventType: AuditLogEventTypes.Workspace.Deleted,
            assertDetails: details =>
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId),
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Critical);
    }

    [Fact]
    public async Task updating_workspace_name_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var newName = Random.Name("RenamedWorkspace");

        //when
        await Api.Workspaces.UpdateName(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceNameRequestDto(
                Name: newName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.NameUpdated>(
            expectedEventType: AuditLogEventTypes.Workspace.NameUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Workspace.Name.Should().Be(newName);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task changing_workspace_owner_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var newOwner = await InviteAndRegisterUser(
            user: AppOwner);

        //when
        await Api.WorkspacesAdmin.UpdateOwner(
            externalId: workspace.ExternalId,
            request: new ChangeWorkspaceOwnerRequestDto(
                NewOwnerExternalId: newOwner.ExternalId),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.OwnerChanged>(
            expectedEventType: AuditLogEventTypes.Workspace.OwnerChanged,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.NewOwner.Email.Should().Be(newOwner.Email);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_workspace_max_size_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        await Api.WorkspacesAdmin.UpdateMaxSize(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceMaxSizeDto
            {
                MaxSizeInBytes = 1024 * 1024 * 100
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.MaxSizeUpdated>(
            expectedEventType: AuditLogEventTypes.Workspace.MaxSizeUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Value.Should().Be(1024 * 1024 * 100);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_workspace_max_team_members_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        await Api.WorkspacesAdmin.UpdateMaxTeamMembers(
            externalId: workspace.ExternalId,
            request: new UpdateWorkspaceMaxTeamMembersRequestDto
            {
                MaxTeamMembers = 10
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.MaxTeamMembersUpdated>(
            expectedEventType: AuditLogEventTypes.Workspace.MaxTeamMembersUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Value.Should().Be(10);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task inviting_member_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        //when
        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.MemberInvited>(
            expectedEventType: AuditLogEventTypes.Workspace.MemberInvited,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Members.Should().Contain(m => m.Email == member.Email);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task revoking_member_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.RevokeMember(
            externalId: workspace.ExternalId,
            memberExternalId: member.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.MemberRevoked>(
            expectedEventType: AuditLogEventTypes.Workspace.MemberRevoked,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Member.Email.Should().Be(member.Email);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_member_permissions_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.UpdateMemberPermissions(
            externalId: workspace.ExternalId,
            memberExternalId: member.ExternalId,
            request: new UpdateWorkspaceMemberPermissionsRequestDto
            {
                AllowShare = true
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.MemberPermissionsUpdated>(
            expectedEventType: AuditLogEventTypes.Workspace.MemberPermissionsUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Member.Email.Should().Be(member.Email);
                details.AllowShare.Should().BeTrue();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task accepting_invitation_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.InvitationResponse>(
            expectedEventType: AuditLogEventTypes.Workspace.InvitationAccepted,
            assertDetails: details =>
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId),
            expectedActorEmail: member.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task rejecting_invitation_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Workspaces.RejectInvitation(
            externalId: workspace.ExternalId,
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.InvitationResponse>(
            expectedEventType: AuditLogEventTypes.Workspace.InvitationRejected,
            assertDetails: details =>
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId),
            expectedActorEmail: member.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task leaving_workspace_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var member = await InviteAndRegisterUser(
            user: AppOwner);

        await Api.Workspaces.InviteMember(
            externalId: workspace.ExternalId,
            request: new CreateWorkspaceMemberInvitationRequestDto(
                MemberEmails: [member.Email],
                AllowShare: false,
                EphemeralDekLifetimeHours: null),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.Workspaces.AcceptInvitation(
            externalId: workspace.ExternalId,
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        //when
        await Api.Workspaces.LeaveSharedWorkspace(
            externalId: workspace.ExternalId,
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.MemberLeft>(
            expectedEventType: AuditLogEventTypes.Workspace.MemberLeft,
            assertDetails: details =>
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId),
            expectedActorEmail: member.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_delete_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Workspaces.BulkDelete(
            externalId: workspace.ExternalId,
            request: new BulkDeleteRequestDto
            {
                FileExternalIds = [],
                FolderExternalIds = [folder.ExternalId],
                FileUploadExternalIds = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<Audit.Workspace.BulkDeleteRequested>(
            expectedEventType: AuditLogEventTypes.Workspace.BulkDeleteRequested,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Folders.Should().Contain(f => f.ExternalId == folder.ExternalId);
                details.Files.Should().BeEmpty();
                details.FileUploads.Should().BeEmpty();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Critical);
    }
}
