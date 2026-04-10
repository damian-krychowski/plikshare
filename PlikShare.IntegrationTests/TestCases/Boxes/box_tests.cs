using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Boxes.Get.Contracts;
using PlikShare.Boxes.List.Contracts;
using PlikShare.Boxes.UpdateFolder.Contracts;
using PlikShare.Boxes.UpdateIsEnabled.Contracts;
using PlikShare.Boxes.UpdateName.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Boxes;

[Collection(IntegrationTestsCollection.Name)]
public class box_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public box_tests(
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
    public async Task when_box_name_is_updated_it_should_be_reflected_in_box_details()
    {
        //given
        var box = await CreateBox(user: AppOwner);

        //when
        await Api.Boxes.UpdateName(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxNameRequestDto(Name: "renamed box"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxContent = await Api.Boxes.Get(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie);

        boxContent.Details.Name.Should().Be("renamed box");
    }

    [Fact]
    public async Task when_box_is_disabled_it_should_be_reflected_in_boxes_list()
    {
        //given
        var box = await CreateBox(user: AppOwner);

        //when
        await Api.Boxes.UpdateIsEnabled(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxIsEnabledRequestDto(IsEnabled: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxes = await Api.Boxes.GetList(
            workspaceExternalId: box.WorkspaceExternalId,
            cookie: AppOwner.Cookie);

        boxes.Items.Should().Contain(b =>
            b.ExternalId == box.ExternalId && b.IsEnabled == false);
    }

    [Fact]
    public async Task when_box_is_moved_to_another_folder_it_should_be_reflected_in_boxes_list()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var box = await CreateBox(
            folder: folder,
            user: AppOwner);

        var newFolder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Boxes.UpdateFolder(
            workspaceExternalId: workspace.ExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFolderRequestDto(
                FolderExternalId: newFolder.ExternalId),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxes = await Api.Boxes.GetList(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        var updatedBox = boxes.Items.Should().Contain(b =>
            b.ExternalId == box.ExternalId).Which;

        updatedBox.FolderPath.Should().Contain(f =>
            f.ExternalId == newFolder.ExternalId.Value);
    }

    [Fact]
    public async Task when_box_is_deleted_it_should_not_be_visible_in_boxes_list()
    {
        //given
        var box = await CreateBox(user: AppOwner);

        //when
        await Api.Boxes.Delete(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var boxes = await Api.Boxes.GetList(
            workspaceExternalId: box.WorkspaceExternalId,
            cookie: AppOwner.Cookie);

        boxes.Items.Should().NotContain(b =>
            b.ExternalId == box.ExternalId);
    }

    // --- Audit log tests ---

    [Fact]
    public async Task updating_box_name_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);

        //when
        await Api.Boxes.UpdateName(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxNameRequestDto(Name: "new-name"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.NameUpdated>(
            expectedEventType: AuditLogEventTypes.Box.NameUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.Box.Name.Should().Be("new-name");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_box_is_enabled_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);

        //when
        await Api.Boxes.UpdateIsEnabled(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxIsEnabledRequestDto(IsEnabled: false),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.IsEnabledUpdated>(
            expectedEventType: AuditLogEventTypes.Box.IsEnabledUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.IsEnabled.Should().BeFalse();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task updating_box_folder_should_produce_audit_log_entry()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var box = await CreateBox(
            folder: folder,
            user: AppOwner);

        var newFolder = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Boxes.UpdateFolder(
            workspaceExternalId: workspace.ExternalId,
            boxExternalId: box.ExternalId,
            request: new UpdateBoxFolderRequestDto(
                FolderExternalId: newFolder.ExternalId),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.FolderUpdated>(
            expectedEventType: AuditLogEventTypes.Box.FolderUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
                details.NewFolder.ExternalId.Should().Be(newFolder.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deleting_box_should_produce_audit_log_entry()
    {
        //given
        var box = await CreateBox(user: AppOwner);

        //when
        await Api.Boxes.Delete(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Box.Deleted>(
            expectedEventType: AuditLogEventTypes.Box.Deleted,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(box.WorkspaceExternalId);
                details.Box.ExternalId.Should().Be(box.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }
}
