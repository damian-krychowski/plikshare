using FluentAssertions;
using PlikShare.BulkDelete.Contracts;
using PlikShare.Boxes.Members.CreateInvitation.Contracts;
using PlikShare.Files.Metadata;
using PlikShare.Folders.List;
using PlikShare.Folders.UpdatePositions.Contracts;
using PlikShare.Integrations.Aws.Textract.TestConfiguration;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.TestAssets;
using PlikShare.Storages.Encryption;
using PlikShare.Workspaces.SearchFilesTree.Contracts;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Workspaces;

[Collection(IntegrationTestsCollection.Name)]
public class search_files_tree_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public search_files_tree_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    // --- Group A: Basic matching ---

    [Fact]
    public async Task search_returns_file_matching_full_name()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual-report");

        // then
        response.Files.Should().ContainSingle()
            .Which.ExternalId.Should().Be(file.ExternalId.Value);
    }

    [Fact]
    public async Task search_returns_file_matching_partial_name()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "epor");

        // then
        response.Files.Should().ContainSingle()
            .Which.ExternalId.Should().Be(file.ExternalId.Value);
    }

    [Fact]
    public async Task search_is_case_insensitive()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "AnnualReport.TXT",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when - lowercase phrase against mixed-case file name
        var response = await SearchWorkspace(workspace, phrase: "annualreport");

        // then
        response.Files.Should().ContainSingle()
            .Which.ExternalId.Should().Be(file.ExternalId.Value);
    }

    [Fact]
    public async Task search_with_no_matches_returns_empty_response()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "nonexistent-xyz123");

        // then - protobuf-net deserializes missing repeated fields as null, so accept either
        response.Files.Should().BeNullOrEmpty();
        response.Folders.Should().BeNullOrEmpty();
        response.FolderExternalIds.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task search_excludes_files_in_deleted_folders()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

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

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual");

        // then - file is hidden because its folder is being deleted
        response.Files.Should().BeNullOrEmpty();
    }

    // --- Group B: Tree structure encoding ---

    [Fact]
    public async Task search_returns_full_ancestor_chain_for_matching_file()
    {
        // given - structure A/B/C/file.txt
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(parent: folderA, workspace: workspace, user: AppOwner);
        var folderC = await CreateFolder(parent: folderB, workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folderC,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual-report");

        // then
        response.Files.Should().ContainSingle();
        response.Folders.Should().HaveCount(3);
        response.FolderExternalIds.Should().BeEquivalentTo(new[]
        {
            folderA.ExternalId.Value, folderB.ExternalId.Value, folderC.ExternalId.Value
        });
    }

    [Fact]
    public async Task parent_id_index_is_minus_one_for_top_level_folder()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folderA,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual");

        // then
        response.Folders.Should().ContainSingle()
            .Which.ParentIdIndex.Should().Be(-1);
    }

    [Fact]
    public async Task parent_id_index_correctly_references_parent_folder()
    {
        // given - structure A/B
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folderA = await CreateFolder(workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(parent: folderA, workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folderB,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual");

        // then - folderB.ParentIdIndex points to folderA's index in FolderExternalIds
        var folderAIndex = response.FolderExternalIds.IndexOf(folderA.ExternalId.Value);
        var folderBDto = response.Folders.Single(f => f.Name == folderB.Name);

        folderAIndex.Should().BeGreaterThanOrEqualTo(0);
        folderBDto.ParentIdIndex.Should().Be(folderAIndex);
    }

    [Fact]
    public async Task file_folder_id_index_references_its_immediate_parent()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual");

        // then
        var folderIndex = response.FolderExternalIds.IndexOf(folder.ExternalId.Value);
        folderIndex.Should().BeGreaterThanOrEqualTo(0);

        response.Files.Should().ContainSingle()
            .Which.FolderIdIndex.Should().Be(folderIndex);
    }

    [Fact]
    public async Task searching_by_folder_name_alone_does_not_return_folders()
    {
        // given - folder named "documents", no file matching that phrase
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when - search for the folder's name (which is a base62 random string, not "documents")
        var response = await SearchWorkspace(workspace, phrase: folder.Name);

        // then - search query matches files only, so even a folder named exactly the phrase
        //        is not returned unless one of its files also matches
        response.Files.Should().BeNullOrEmpty();
        response.Folders.Should().BeNullOrEmpty();
    }

    // --- Group C: Folder scoping ---

    [Fact]
    public async Task search_scoped_to_folder_returns_only_descendants()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var scopeRoot = await CreateFolder(workspace: workspace, user: AppOwner);
        var scopeChild = await CreateFolder(parent: scopeRoot, workspace: workspace, user: AppOwner);
        var siblingRoot = await CreateFolder(workspace: workspace, user: AppOwner);

        var insideFile = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-inside.txt",
            contentType: "text/plain",
            folder: scopeChild,
            workspace: workspace,
            user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-outside.txt",
            contentType: "text/plain",
            folder: siblingRoot,
            workspace: workspace,
            user: AppOwner);

        // when - search scoped to scopeRoot
        var response = await Api.Workspaces.SearchFilesTree(
            externalId: workspace.ExternalId,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = "match",
                FolderExternalId = scopeRoot.ExternalId
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // then - only the inside file is returned
        response.Files.Should().ContainSingle()
            .Which.ExternalId.Should().Be(insideFile.ExternalId.Value);
    }

    // --- Group D: Position & CreatedAt visibility (workspace) ---

    [Fact]
    public async Task workspace_search_exposes_position_and_created_at_for_files()
    {
        // given
        var now = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(now);

        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // pin the file's position so search returns the stored value (otherwise NULL → 0)
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = folder.ExternalId,
                Folders = [],
                Files = [
                    new UpdatePositionItemDto
                        { ExternalId = file.ExternalId.Value, Position = 1 * ItemPosition.Step }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual");

        // then
        var dto = response.Files.Should().ContainSingle().Which;
        dto.ExternalId.Should().Be(file.ExternalId.Value);
        dto.Position.Should().Be(1 * ItemPosition.Step);
        dto.CreatedAt.Should().Be(now.DateTime);
    }

    [Fact]
    public async Task workspace_search_exposes_position_and_created_at_for_folders()
    {
        // given
        var now = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(now);

        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // pin the folder's position so search returns the stored value (otherwise NULL → 0)
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = null,
                Folders = [
                    new UpdatePositionItemDto
                        { ExternalId = folder.ExternalId.Value, Position = 1 * ItemPosition.Step }
                ],
                Files = []
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // when
        var response = await SearchWorkspace(workspace, phrase: "annual");

        // then
        var dto = response.Folders.Should().ContainSingle().Which;
        dto.Name.Should().Be(folder.Name);
        dto.Position.Should().Be(1 * ItemPosition.Step);
        dto.CreatedAt.Should().Be(now.DateTime);
    }

    [Fact]
    public async Task workspace_search_returns_stored_positions_after_reorder()
    {
        // given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var fileA = await UploadFile(
            content: "alpha"u8.ToArray(),
            fileName: "match-alpha.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        var fileB = await UploadFile(
            content: "beta"u8.ToArray(),
            fileName: "match-beta.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = folder.ExternalId,
                Folders = [],
                Files = [
                    new UpdatePositionItemDto
                        { ExternalId = fileA.ExternalId.Value, Position = 5 * ItemPosition.Step },
                    new UpdatePositionItemDto
                        { ExternalId = fileB.ExternalId.Value, Position = 7 * ItemPosition.Step }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        // when
        var response = await SearchWorkspace(workspace, phrase: "match");

        // then
        response.Files.Should().HaveCount(2);
        response.Files.Single(f => f.ExternalId == fileA.ExternalId.Value)
            .Position.Should().Be(5 * ItemPosition.Step);
        response.Files.Single(f => f.ExternalId == fileB.ExternalId.Value)
            .Position.Should().Be(7 * ItemPosition.Step);
    }

    // --- Group E: Box context — restricted CreatedAt visibility ---

    [Fact]
    public async Task box_link_search_returns_position_for_files_but_null_created_at()
    {
        // given
        var now = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(now);

        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: boxFolder,
            workspace: workspace,
            user: AppOwner);

        // pin the file's position so search exposes a meaningful value
        await Api.Folders.UpdatePositions(
            workspaceExternalId: workspace.ExternalId,
            request: new UpdatePositionsRequestDto
            {
                ParentFolderExternalId = boxFolder.ExternalId,
                Folders = [],
                Files = [
                    new UpdatePositionItemDto
                        { ExternalId = file.ExternalId.Value, Position = 1 * ItemPosition.Step }
                ]
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(AllowList: true));

        var session = await StartBoxLinkSession();

        // when
        var response = await Api.AccessCodesApi.SearchFilesTree(
            accessCode: boxLink.AccessCode,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = "annual",
                FolderExternalId = null
            },
            boxLinkToken: session.Token);

        // then
        var fileDto = response.Files.Should().ContainSingle().Which;
        fileDto.Position.Should().Be(1 * ItemPosition.Step);
        fileDto.CreatedAt.Should().BeNull();
    }

    [Fact]
    public async Task box_link_search_returns_created_at_only_for_folders_user_created()
    {
        // given - owner sets up box, anonymous user creates a subfolder + uploads a file
        var ownerCreatedAt = new DateTimeOffset(2024, 11, 10, 12, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(ownerCreatedAt);

        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);
        var ownerSubfolder = await CreateFolder(parent: boxFolder, workspace: workspace, user: AppOwner);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(
                AllowList: true,
                AllowCreateFolder: true,
                AllowUpload: true));

        var anonymousCreatedAt = new DateTimeOffset(2024, 11, 11, 9, 0, 0, TimeSpan.Zero);
        Clock.CurrentTime(anonymousCreatedAt);

        var session = await StartBoxLinkSession();

        // anonymous user creates a folder
        var anonFolderExternalId = PlikShare.Folders.Id.FolderExtId.NewId();
        await Api.AccessCodesApi.CreateFolder(
            accessCode: boxLink.AccessCode,
            request: new PlikShare.Folders.Create.Contracts.CreateFolderRequestDto
            {
                ExternalId = anonFolderExternalId,
                ParentExternalId = null,
                Name = "anon-match-folder"
            },
            boxLinkToken: session.Token);

        // owner uploads a file inside the anon folder so search has something to match
        // (search phrase needs to live as a file name)
        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-target.txt",
            contentType: "text/plain",
            folder: new AppFolder(anonFolderExternalId, workspace.ExternalId, "anon-match-folder"),
            workspace: workspace,
            user: AppOwner);

        // and another match in the owner-created subfolder (so the owner subfolder appears as ancestor too)
        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-other.txt",
            contentType: "text/plain",
            folder: ownerSubfolder,
            workspace: workspace,
            user: AppOwner);

        // when
        var response = await Api.AccessCodesApi.SearchFilesTree(
            accessCode: boxLink.AccessCode,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = "match",
                FolderExternalId = null
            },
            boxLinkToken: session.Token);

        // then - the anonymous-created folder has CreatedAt populated; the owner-created one does not
        var anonFolderDto = response.Folders.Single(f => f.Name == "anon-match-folder");
        anonFolderDto.WasCreatedByUser.Should().BeTrue();
        anonFolderDto.CreatedAt.Should().Be(anonymousCreatedAt.DateTime);

        var ownerFolderDto = response.Folders.Single(f => f.Name == ownerSubfolder.Name);
        ownerFolderDto.WasCreatedByUser.Should().BeFalse();
        ownerFolderDto.CreatedAt.Should().BeNull();
    }

    // --- Group G: Box folder scoping ---

    [Fact]
    public async Task box_authenticated_search_only_returns_files_within_box_folder_subtree()
    {
        // given - workspace has files inside and outside the box folder subtree
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);
        var boxChildFolder = await CreateFolder(parent: boxFolder, workspace: workspace, user: AppOwner);
        var siblingFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var insideFile = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-inside.txt",
            contentType: "text/plain",
            folder: boxChildFolder,
            workspace: workspace,
            user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-outside.txt",
            contentType: "text/plain",
            folder: siblingFolder,
            workspace: workspace,
            user: AppOwner);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);

        // invite a member who will use the box authenticated endpoint
        var member = await InviteAndRegisterUser(user: AppOwner);

        await Api.Boxes.InviteMember(
            workspaceExternalId: workspace.ExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxInvitationRequestDto(MemberEmails: [member.Email]),
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.BoxExternalAccess.AcceptInvitation(
            boxExternalId: box.ExternalId,
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        // when
        var response = await Api.BoxExternalAccess.SearchFilesTree(
            boxExternalId: box.ExternalId,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = "match",
                FolderExternalId = null
            },
            cookie: member.Cookie,
            antiforgery: member.Antiforgery);

        // then - only files inside the box folder subtree are visible
        response.Files.Should().ContainSingle()
            .Which.ExternalId.Should().Be(insideFile.ExternalId.Value);
    }

    [Fact]
    public async Task box_link_search_excludes_files_above_box_folder()
    {
        // given - workspace has files inside and outside the box folder subtree
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var boxFolder = await CreateFolder(workspace: workspace, user: AppOwner);
        var boxChildFolder = await CreateFolder(parent: boxFolder, workspace: workspace, user: AppOwner);
        var siblingFolder = await CreateFolder(workspace: workspace, user: AppOwner);

        var insideFile = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-inside.txt",
            contentType: "text/plain",
            folder: boxChildFolder,
            workspace: workspace,
            user: AppOwner);

        await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "match-outside.txt",
            contentType: "text/plain",
            folder: siblingFolder,
            workspace: workspace,
            user: AppOwner);

        var box = await CreateBox(folder: boxFolder, user: AppOwner);
        var boxLink = await CreateBoxLink(
            box: box,
            user: AppOwner,
            permissions: new AppBoxLinkPermissions(AllowList: true));

        var session = await StartBoxLinkSession();

        // when
        var response = await Api.AccessCodesApi.SearchFilesTree(
            accessCode: boxLink.AccessCode,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = "match",
                FolderExternalId = null
            },
            boxLinkToken: session.Token);

        // then - only files inside the box folder subtree are visible
        response.Files.Should().ContainSingle()
            .Which.ExternalId.Should().Be(insideFile.ExternalId.Value);
    }

    // --- Helpers ---

    [Fact]
    public async Task search_in_full_encryption_workspace_matches_and_returns_decrypted_names()
    {
        // given — a FULL-encryption workspace: fi_name is stored as a pse: envelope, so a plaintext
        // LIKE can never match. Search must decrypt inline via app_decrypt_metadata, which needs the
        // workspace encryption session.
        var fullStorage = await CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.Full);

        var workspace = await CreateWorkspace(storage: fullStorage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var file = await UploadFile(
            content: "x"u8.ToArray(),
            fileName: "annual-report.txt",
            contentType: "text/plain",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        // when — partial-name search with the workspace encryption session
        var response = await Api.Workspaces.SearchFilesTree(
            externalId: workspace.ExternalId,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = "epor",
                FolderExternalId = null
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        // then — the encrypted file is found and its name comes back decrypted (not ciphertext)
        var match = response.Files.Should().ContainSingle().Subject;
        match.ExternalId.Should().Be(file.ExternalId.Value);
        match.Name.Should().Be("annual-report");
        match.Name.Should().NotContain("pse:");
    }

    [Fact]
    public async Task search_returns_mini_thumbnail_etag_for_a_file_with_a_generated_thumbnail()
    {
        // given — an image with a generated Mini thumbnail
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(workspace: workspace, user: AppOwner);

        var image = await UploadFile(
            content: TextractTestImage.GetBytes(),
            fileName: "vacation-photo.png",
            contentType: "image/png",
            folder: folder,
            workspace: workspace,
            user: AppOwner);

        await WaitForFileUnlocked(image.ExternalId, AppOwner);

        var batchId = await Api.MediaProcessing.GenerateFileThumbnails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: image.ExternalId,
            variants: [ThumbnailVariant.Mini],
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        await Api.MediaProcessing.WaitForBatchDone(
            workspaceExternalId: workspace.ExternalId,
            batchId: batchId,
            cookie: AppOwner.Cookie);

        // when
        var response = await SearchWorkspace(workspace, phrase: "vacation-photo");

        // then — the search result carries the Mini thumbnail etag
        response.Files.Should().ContainSingle()
            .Which.Metadata.Thumbnail.MiniEtag.Should().NotBeNullOrEmpty();
    }

    private Task<SearchFilesTreeResponseDto> SearchWorkspace(AppWorkspace workspace, string phrase) =>
        Api.Workspaces.SearchFilesTree(
            externalId: workspace.ExternalId,
            request: new SearchFilesTreeRequestDto
            {
                Phrase = phrase,
                FolderExternalId = null
            },
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);
}
