using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Details;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Dashboard.Content.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.ChangeOwner.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Get.Contracts;
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
            StorageEncryptionType = "none"
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
