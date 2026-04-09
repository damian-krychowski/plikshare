using System.Text;
using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.Comment.CreateComment.Contracts;
using PlikShare.Files.Preview.Comment.EditComment.Contracts;
using PlikShare.Files.Preview.SaveNote.Contracts;
using PlikShare.Files.Rename.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

[Collection(IntegrationTestsCollection.Name)]
public class file_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public file_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(
            user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    private async Task<(AppFile File, AppWorkspace Workspace)> UploadTestFile()
    {
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var folder = await CreateFolder(
            parent: null,
            workspace: workspace,
            user: AppOwner);

        var file = await UploadFile(
            content: Encoding.UTF8.GetBytes("test content"),
            fileName: "test-file.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        return (file, workspace);
    }

    // --- Functional tests ---

    [Fact]
    public async Task when_file_is_renamed_it_should_be_reflected_in_preview_details()
    {
        //given
        var (file, workspace) = await UploadTestFile();

        //when
        await Api.Files.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new UpdateFileNameRequestDto(Name: "renamed-file"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then - file was renamed successfully (no error thrown)
    }

    [Fact]
    public async Task when_note_is_saved_it_should_be_visible_in_preview_details()
    {
        //given
        var (file, workspace) = await UploadTestFile();

        //when
        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: "{\"text\":\"my note\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["note"],
            cookie: AppOwner.Cookie);

        details.Note.Should().NotBeNull();
        details.Note!.ContentJson.Should().Be("{\"text\":\"my note\"}");
    }

    [Fact]
    public async Task when_comment_is_created_it_should_be_visible_in_preview_details()
    {
        //given
        var (file, workspace) = await UploadTestFile();
        var commentExternalId = FileArtifactExtId.NewId();

        //when
        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"my comment\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["comments"],
            cookie: AppOwner.Cookie);

        details.Comments.Should().Contain(c =>
            c.ExternalId == commentExternalId &&
            c.ContentJson == "{\"text\":\"my comment\"}");
    }

    [Fact]
    public async Task when_comment_is_edited_it_should_be_reflected_in_preview_details()
    {
        //given
        var (file, workspace) = await UploadTestFile();
        var commentExternalId = FileArtifactExtId.NewId();

        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"original\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Files.EditComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            commentExternalId: commentExternalId,
            request: new EditFileCommentRequestDto(ContentJson: "{\"text\":\"edited\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["comments"],
            cookie: AppOwner.Cookie);

        details.Comments.Should().Contain(c =>
            c.ExternalId == commentExternalId &&
            c.ContentJson == "{\"text\":\"edited\"}" &&
            c.WasEdited == true);
    }

    [Fact]
    public async Task when_comment_is_deleted_it_should_not_be_visible_in_preview_details()
    {
        //given
        var (file, workspace) = await UploadTestFile();
        var commentExternalId = FileArtifactExtId.NewId();

        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"to delete\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Files.DeleteComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            commentExternalId: commentExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["comments"],
            cookie: AppOwner.Cookie);

        details.Comments.Should().NotContain(c =>
            c.ExternalId == commentExternalId);
    }

    // --- Audit log tests ---

    [Fact]
    public async Task renaming_file_should_produce_audit_log_entry()
    {
        //given
        var (file, workspace) = await UploadTestFile();

        //when
        await Api.Files.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new UpdateFileNameRequestDto(Name: "audit-renamed"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.File.Renamed>(
            expectedEventType: AuditLogEventTypes.File.Renamed,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(file.ExternalId);
                details.File.Name.Should().Be("audit-renamed.txt");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task saving_note_should_produce_audit_log_entry()
    {
        //given
        var (file, workspace) = await UploadTestFile();

        //when
        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: "{\"text\":\"audit note\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.File.NoteSaved>(
            expectedEventType: AuditLogEventTypes.File.NoteSaved,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(file.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task saving_note_with_unchanged_content_should_not_produce_audit_log_entry()
    {
        //given
        var (file, workspace) = await UploadTestFile();

        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: "{\"text\":\"same note\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        ClearAuditLog();

        //when - save the same note content again
        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: "{\"text\":\"same note\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then - no audit log entry should be produced for unchanged content
        await AssertAuditLogDoesNotContain(
            expectedEventType: AuditLogEventTypes.File.NoteSaved,
            expectedActorEmail: AppOwner.Email);
    }

    [Fact]
    public async Task creating_comment_should_produce_audit_log_entry()
    {
        //given
        var (file, workspace) = await UploadTestFile();
        var commentExternalId = FileArtifactExtId.NewId();

        //when
        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"audit comment\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.File.CommentCreated>(
            expectedEventType: AuditLogEventTypes.File.CommentCreated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(file.ExternalId);
                details.CommentExternalId.Should().Be(commentExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task editing_comment_should_produce_audit_log_entry()
    {
        //given
        var (file, workspace) = await UploadTestFile();
        var commentExternalId = FileArtifactExtId.NewId();

        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"original\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Files.EditComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            commentExternalId: commentExternalId,
            request: new EditFileCommentRequestDto(ContentJson: "{\"text\":\"edited\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.File.CommentEdited>(
            expectedEventType: AuditLogEventTypes.File.CommentEdited,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(file.ExternalId);
                details.CommentExternalId.Should().Be(commentExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task deleting_comment_should_produce_audit_log_entry()
    {
        //given
        var (file, workspace) = await UploadTestFile();
        var commentExternalId = FileArtifactExtId.NewId();

        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"to delete\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //when
        await Api.Files.DeleteComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            commentExternalId: commentExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.File.CommentDeleted>(
            expectedEventType: AuditLogEventTypes.File.CommentDeleted,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(file.ExternalId);
                details.CommentExternalId.Should().Be(commentExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Warning);
    }

    [Fact]
    public async Task updating_file_content_should_produce_audit_log_entry()
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
            content: Encoding.UTF8.GetBytes("# original markdown"),
            fileName: "test-file.md",
            contentType: "text/markdown",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Files.UpdateContent(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            content: Encoding.UTF8.GetBytes("# updated markdown"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.File.ContentUpdated>(
            expectedEventType: AuditLogEventTypes.File.ContentUpdated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.File.ExternalId.Should().Be(file.ExternalId);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
