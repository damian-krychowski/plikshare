using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Preview.Comment.CreateComment.Contracts;
using PlikShare.Files.Preview.Comment.EditComment.Contracts;
using PlikShare.Files.Preview.SaveNote.Contracts;
using PlikShare.Files.Rename.Contracts;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.Rename.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

/// <summary>
/// Per-field at-rest assertions for full-encryption workspaces. Every column we encrypt
/// gets paired tests:
///   - Full storage: persisted bytes carry the <c>pse:</c> prefix and never leak the plaintext.
///   - None storage:  persisted bytes are plaintext (regression guard against accidentally
///     encrypting in modes that should not encrypt).
/// API round-trip is also asserted so we know the decode path still produces the original
/// plaintext for the caller — encryption is invisible above the SQL boundary.
/// </summary>
[Collection(IntegrationTestsCollection.Name)]
public class full_encryption_metadata_at_rest_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }

    public full_encryption_metadata_at_rest_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        hostFixture.ResetUserEncryption().AsTask().Wait();
        AppOwner = SignIn(user: Users.AppOwner).Result;
    }

    private async Task<AppWorkspace> SetupWorkspace(StorageEncryptionType encryptionType)
    {
        var storage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: encryptionType);

        return await CreateWorkspace(
            storage: storage,
            user: AppOwner);
    }

    // --- Folder names ---

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task folder_name_persistence_matches_encryption_type(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var folderName = $"top-folder-{Guid.NewGuid().ToBase62()}";

        //when
        var folder = await CreateFolder(
            name: folderName,
            workspace: workspace,
            user: AppOwner);

        //then — at-rest form depends on storage type
        var persistedName = GetFolderPersistedName(folder.ExternalId);
        if (encryptionType == StorageEncryptionType.Full)
        {
            persistedName.Should().StartWith(EncryptedMetadataPrefix,
                "fo_name must be encrypted on full-encryption storage");
            persistedName.Should().NotContain(folderName,
                "encrypted column must not leak plaintext");
        }
        else
        {
            persistedName.Should().Be(folderName,
                "fo_name must be plaintext on non-encrypted storage");
        }

        //and — API round-trips plaintext regardless of mode
        var topListing = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        topListing.Subfolders.Should().Contain(s =>
            s.ExternalId == folder.ExternalId.Value && s.Name == folderName);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task subfolder_name_persistence_matches_encryption_type(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var parent = await CreateFolder(workspace: workspace, user: AppOwner);
        var childName = $"child-folder-{Guid.NewGuid().ToBase62()}";

        //when
        var child = await CreateFolder(
            parent: parent,
            workspace: workspace,
            user: AppOwner);

        await Api.Folders.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: child.ExternalId,
            request: new UpdateFolderNameRequestDto(Name: childName),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var persistedName = GetFolderPersistedName(child.ExternalId);
        if (encryptionType == StorageEncryptionType.Full)
        {
            persistedName.Should().StartWith(EncryptedMetadataPrefix);
            persistedName.Should().NotContain(childName);
        }
        else
        {
            persistedName.Should().Be(childName);
        }

        //and
        var listing = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parent.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        listing.Subfolders.Should().Contain(s =>
            s.ExternalId == child.ExternalId.Value && s.Name == childName);
    }

    [Fact]
    public async Task folder_rename_under_full_encryption_produces_fresh_ciphertext()
    {
        // AES-GCM is non-deterministic by design — every encode picks a fresh nonce.
        // After rename the new ciphertext MUST differ from the prior one even if the user
        // typed the same name (which would be a regression: leaked plaintext via repeated
        // ciphertext is a textbook AEAD failure mode).
        //given
        var workspace = await SetupWorkspace(StorageEncryptionType.Full);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var beforeRename = GetFolderPersistedName(folder.ExternalId);

        //when
        await Api.Folders.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            request: new UpdateFolderNameRequestDto(Name: $"renamed-{Guid.NewGuid().ToBase62()}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var afterRename = GetFolderPersistedName(folder.ExternalId);
        afterRename.Should().StartWith(EncryptedMetadataPrefix);
        afterRename.Should().NotBe(beforeRename,
            "rename must produce a fresh ciphertext");
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task bulk_create_folders_persistence_matches_encryption_type(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var nameOne = $"bulk-one-{Guid.NewGuid().ToBase62()}";
        var nameTwo = $"bulk-two-{Guid.NewGuid().ToBase62()}";
        var nestedName = $"bulk-nested-{Guid.NewGuid().ToBase62()}";

        //when — bulk creates a tree with one nested subfolder
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = nameOne,
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = nestedName,
                                Subfolders = null
                            }
                        ]
                    },
                    new FolderTreeDto
                    {
                        TemporaryId = 3,
                        Name = nameTwo,
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then — every created row is persisted in the mode appropriate for the storage
        foreach (var item in response.Items)
        {
            var extId = FolderExtId.Parse(item.ExternalId);
            var persisted = GetFolderPersistedName(extId);

            if (encryptionType == StorageEncryptionType.Full)
            {
                persisted.Should().StartWith(EncryptedMetadataPrefix);
            }
            else
            {
                // Bulk path may add suffixes when EnsureUniqueNames is set; off here so
                // the stored name should be one of the originals verbatim.
                new[] { nameOne, nameTwo, nestedName }
                    .Should().Contain(persisted);
            }
        }
    }

    // --- File names / extension / content type / metadata ---

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_name_extension_content_type_persistence_matches_encryption_type(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var fileName = $"upload-{Guid.NewGuid().ToBase62()}.bin";
        const string contentType = "application/octet-stream";

        //when
        var file = await UploadFile(
            content: Random.Bytes(64),
            fileName: fileName,
            contentType: contentType,
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //then — production splits the filename in FileNames.ToNameAndExtension, which
        // keeps the leading dot on the extension (e.g. "name.bin" → ("name", ".bin")).
        // Mirror that exactly so DB and API assertions align with the persisted form.
        var nameParts = PlikShare.Core.Utils.FileNames.ToNameAndExtension(fileName);
        var expectedName = nameParts.Name;
        var expectedExtension = nameParts.Extension;
        var persisted = GetFilePersistedRow(file.ExternalId);

        if (encryptionType == StorageEncryptionType.Full)
        {
            persisted.Name.Should().StartWith(EncryptedMetadataPrefix);
            persisted.Extension.Should().StartWith(EncryptedMetadataPrefix);
            persisted.ContentType.Should().StartWith(EncryptedMetadataPrefix);

            persisted.Name.Should().NotContain(expectedName);
            persisted.Extension.Should().NotContain(expectedExtension);
            persisted.ContentType.Should().NotContain(contentType);
        }
        else
        {
            persisted.Name.Should().Be(expectedName);
            persisted.Extension.Should().Be(expectedExtension);
            persisted.ContentType.Should().Be(contentType);
        }

        //and — API round-trips plaintext via folder listing
        var listing = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folder.ExternalId,
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        listing.Files.Should().Contain(f =>
            f.ExternalId == file.ExternalId.Value
            && f.Name == expectedName
            && f.Extension == expectedExtension);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task file_metadata_column_is_null_for_user_uploaded_file(
        StorageEncryptionType encryptionType)
    {
        // fi_metadata only carries data for Textract-derived files. Textract is blocked on
        // full-encryption storage, and the regular upload path always writes NULL — this
        // test pins that contract so we don't regress NULL handling after the encryption
        // refactor (NULL must stay NULL, not become an empty BLOB or encoded "null").
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        //when
        var file = await UploadFile(
            content: Random.Bytes(32),
            fileName: $"meta-{Guid.NewGuid().ToBase62()}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //then
        var persisted = GetFilePersistedRow(file.ExternalId);
        persisted.Metadata.Should().BeNull(
            "regular uploads must not write fi_metadata regardless of encryption");
    }

    [Fact]
    public async Task file_rename_under_full_encryption_produces_fresh_ciphertext()
    {
        //given
        var workspace = await SetupWorkspace(StorageEncryptionType.Full);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadFile(
            content: Random.Bytes(32),
            fileName: $"original-{Guid.NewGuid().ToBase62()}.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var beforeRename = GetFilePersistedRow(file.ExternalId);

        //when
        await Api.Files.UpdateName(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new UpdateFileNameRequestDto(Name: $"renamed-{Guid.NewGuid().ToBase62()}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var afterRename = GetFilePersistedRow(file.ExternalId);
        afterRename.Name.Should().StartWith(EncryptedMetadataPrefix);
        afterRename.Name.Should().NotBe(beforeRename.Name);
        afterRename.Extension.Should().Be(beforeRename.Extension,
            "rename only updates fi_name; the extension stays as it was uploaded");
        afterRename.ContentType.Should().Be(beforeRename.ContentType);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task multi_file_bulk_upload_encrypts_every_row(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var files = new List<(byte[] Content, string FileName, string ContentType)>
        {
            (Random.Bytes(16), $"bulk-a-{Guid.NewGuid().ToBase62()}.txt", "text/plain"),
            (Random.Bytes(16), $"bulk-b-{Guid.NewGuid().ToBase62()}.json", "application/json"),
            (Random.Bytes(16), $"bulk-c-{Guid.NewGuid().ToBase62()}.bin", "application/octet-stream")
        };

        //when
        var uploaded = await UploadFiles(
            files: files,
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        //then
        for (var i = 0; i < uploaded.Count; i++)
        {
            var persisted = GetFilePersistedRow(uploaded[i].ExternalId);

            if (encryptionType == StorageEncryptionType.Full)
            {
                persisted.Name.Should().StartWith(EncryptedMetadataPrefix);
                persisted.Extension.Should().StartWith(EncryptedMetadataPrefix);
                persisted.ContentType.Should().StartWith(EncryptedMetadataPrefix);
            }
            else
            {
                var parts = PlikShare.Core.Utils.FileNames.ToNameAndExtension(files[i].FileName);
                persisted.Name.Should().Be(parts.Name);
                persisted.Extension.Should().Be(parts.Extension);
                persisted.ContentType.Should().Be(files[i].ContentType);
            }

            persisted.Metadata.Should().BeNull();
        }
    }

    // --- File comments ---

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task comment_content_persistence_matches_encryption_type(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadFile(
            content: Random.Bytes(16),
            fileName: $"commented-{Guid.NewGuid().ToBase62()}.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var commentExternalId = FileArtifactExtId.NewId();
        const string commentJson = "{\"text\":\"my secret comment\"}";

        //when
        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: commentJson),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var persisted = GetFileArtifactPersistedRow(commentExternalId);
        var contentAsString = Encoding.UTF8.GetString(persisted.Content);

        if (encryptionType == StorageEncryptionType.Full)
        {
            contentAsString.Should().StartWith(EncryptedMetadataPrefix);
            contentAsString.Should().NotContain("my secret comment");
        }
        else
        {
            contentAsString.Should().Contain("my secret comment");
        }

        // Hash always covers the wrapped payload regardless of encryption — proving the
        // hash binding does NOT depend on the workspace encryption session.
        persisted.ContentHash.Should().NotBeEquivalentTo(new byte[32],
            "hash must be filled by CreateFileCommentQuery, not the migration default zeroblob");

        //and
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["comments"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        details.Comments.Should().Contain(c =>
            c.ExternalId == commentExternalId && c.ContentJson == commentJson);
    }

    [Fact]
    public async Task comment_edit_under_full_encryption_produces_fresh_ciphertext_and_hash()
    {
        //given
        var workspace = await SetupWorkspace(StorageEncryptionType.Full);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadFile(
            content: Random.Bytes(16),
            fileName: $"edited-{Guid.NewGuid().ToBase62()}.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var commentExternalId = FileArtifactExtId.NewId();

        await Api.Files.CreateComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new CreateFileCommentRequestDto(
                ExternalId: commentExternalId,
                ContentJson: "{\"text\":\"first version\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        var beforeEdit = GetFileArtifactPersistedRow(commentExternalId);

        //when
        await Api.Files.EditComment(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            commentExternalId: commentExternalId,
            request: new EditFileCommentRequestDto(ContentJson: "{\"text\":\"second version\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var afterEdit = GetFileArtifactPersistedRow(commentExternalId);

        Encoding.UTF8.GetString(afterEdit.Content).Should().StartWith(EncryptedMetadataPrefix);
        afterEdit.Content.Should().NotEqual(beforeEdit.Content,
            "edit must produce fresh ciphertext (new nonce on every encode)");
        afterEdit.ContentHash.Should().NotEqual(beforeEdit.ContentHash,
            "different plaintext must hash differently");
    }

    // --- File notes ---

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task note_content_persistence_matches_encryption_type(
        StorageEncryptionType encryptionType)
    {
        //given
        var workspace = await SetupWorkspace(encryptionType);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadFile(
            content: Random.Bytes(16),
            fileName: $"noted-{Guid.NewGuid().ToBase62()}.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        const string noteJson = "{\"text\":\"my secret note\"}";

        //when
        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: noteJson),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var noteArtifactExternalId = GetSingleNoteArtifactExternalId(file.ExternalId);
        var persisted = GetFileArtifactPersistedRow(noteArtifactExternalId);
        var contentAsString = Encoding.UTF8.GetString(persisted.Content);

        if (encryptionType == StorageEncryptionType.Full)
        {
            contentAsString.Should().StartWith(EncryptedMetadataPrefix);
            contentAsString.Should().NotContain("my secret note");
        }
        else
        {
            contentAsString.Should().Contain("my secret note");
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(noteJson));
        persisted.ContentHash.Should().Equal(expectedHash,
            "note save must hash the plaintext (pre-encryption) so change-detection works under full encryption");

        //and
        var details = await Api.Files.GetPreviewDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            fields: ["note"],
            cookie: AppOwner.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        details.Note.Should().NotBeNull();
        details.Note!.ContentJson.Should().Be(noteJson);
    }

    [Fact]
    public async Task note_save_with_same_content_under_full_encryption_is_idempotent_at_rest()
    {
        // AES-GCM ciphertext is non-deterministic, so a naive "is this row different?"
        // check on the encrypted column would fire on every save and waste a re-encode.
        // The fa_content_hash column carries the plaintext SHA-256 and the ON CONFLICT
        // clause uses it as a stable equality predicate — the second save must be a no-op.
        //given
        var workspace = await SetupWorkspace(StorageEncryptionType.Full);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadFile(
            content: Random.Bytes(16),
            fileName: $"idempotent-{Guid.NewGuid().ToBase62()}.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        const string noteJson = "{\"text\":\"identical body\"}";

        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: noteJson),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        var noteArtifactExternalId = GetSingleNoteArtifactExternalId(file.ExternalId);
        var firstWrite = GetFileArtifactPersistedRow(noteArtifactExternalId);

        //when — second save with identical content
        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: noteJson),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then — content_hash drives the no-op so the encrypted bytes stay byte-identical
        var secondWrite = GetFileArtifactPersistedRow(noteArtifactExternalId);
        secondWrite.Content.Should().Equal(firstWrite.Content,
            "repeat save with same content must be a no-op via content_hash short-circuit");
        secondWrite.ContentHash.Should().Equal(firstWrite.ContentHash);
    }

    [Fact]
    public async Task note_save_with_different_content_under_full_encryption_re_encrypts()
    {
        //given
        var workspace = await SetupWorkspace(StorageEncryptionType.Full);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);
        var file = await UploadFile(
            content: Random.Bytes(16),
            fileName: $"changing-{Guid.NewGuid().ToBase62()}.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: "{\"text\":\"v1\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        var noteArtifactExternalId = GetSingleNoteArtifactExternalId(file.ExternalId);
        var firstWrite = GetFileArtifactPersistedRow(noteArtifactExternalId);

        //when
        await Api.Files.UpdateNote(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: file.ExternalId,
            request: new SaveFileNoteRequestDto(ContentJson: "{\"text\":\"v2\"}"),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var secondWrite = GetFileArtifactPersistedRow(noteArtifactExternalId);
        Encoding.UTF8.GetString(secondWrite.Content).Should().StartWith(EncryptedMetadataPrefix);
        secondWrite.Content.Should().NotEqual(firstWrite.Content);
        secondWrite.ContentHash.Should().NotEqual(firstWrite.ContentHash);
    }

    private FileArtifactExtId GetSingleNoteArtifactExternalId(FileExtId fileExternalId)
    {
        // SaveFileNoteQuery generates the artifact's external id internally on first save
        // and the preview details endpoint does not return it. Pull it straight from
        // fa_file_artifacts. fa_type 'note' is the kebab-case of FileArtifactType.Note
        // (see WithEnumParameter / EnumUtils.ToKebabCase).
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT fa_external_id
                     FROM fa_file_artifacts
                     INNER JOIN fi_files ON fi_id = fa_file_id
                     WHERE fi_external_id = $fileExternalId
                       AND fa_type = 'note'
                     LIMIT 1
                     """,
                readRowFunc: reader => FileArtifactExtId.Parse(reader.GetString(0)))
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"No note artifact found for file '{fileExternalId}'.");

        return rows[0];
    }
}
