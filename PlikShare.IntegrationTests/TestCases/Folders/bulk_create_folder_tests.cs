using FluentAssertions;
using PlikShare.AuditLog;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Folders;

[Collection(IntegrationTestsCollection.Name)]
public class bulk_create_folder_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public bulk_create_folder_tests(
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
    public async Task bulk_creating_nested_tree_under_existing_parent_should_create_correct_hierarchy()
    {
        //given
        Clock.SetToNow();

        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var parent = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = parent.ExternalId.Value,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Child",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "Grandchild",
                                Subfolders = null
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var folderIdMap = response.Items.ToDictionary(x => x.TemporaryId, x => x.ExternalId);

        var parentContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parent.ExternalId,
            cookie: AppOwner.Cookie);

        parentContent.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("Child");

        var childContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[1]),
            cookie: AppOwner.Cookie);

        childContent.Folder.Ancestors.Should().ContainSingle()
            .Which.Name.Should().Be(parent.Name);

        childContent.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("Grandchild");

        var grandchildContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[2]),
            cookie: AppOwner.Cookie);

        grandchildContent.Folder.Ancestors.Should().HaveCount(2);
        grandchildContent.Folder.Ancestors![0].Name.Should().Be(parent.Name);
        grandchildContent.Folder.Ancestors![1].Name.Should().Be("Child");
    }

    [Fact]
    public async Task bulk_creating_nested_tree_under_existing_parent_should_produce_audit_log_with_correct_paths()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var parent = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = parent.ExternalId.Value,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Child",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "Grandchild",
                                Subfolders = null
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Workspace.ExternalId.Should().Be(workspace.ExternalId);
                details.Folders.Should().HaveCount(2);
                details.Folders.Should().Contain(f => f.Name == "Child" && f.FolderPath == parent.Name);
                details.Folders.Should().Contain(f => f.Name == "Grandchild" && f.FolderPath == parent.Name + "/Child");
                details.Box.Should().BeNull();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_creating_under_deeply_nested_parent_should_preserve_full_ancestor_chain()
    {
        //given
        Clock.SetToNow();

        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var grandparent = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var parent = await CreateFolder(
            parent: grandparent,
            workspace: workspace,
            user: AppOwner);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = parent.ExternalId.Value,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Leaf",
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        var leafContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(response.Items[0].ExternalId),
            cookie: AppOwner.Cookie);

        leafContent.Folder.Ancestors.Should().HaveCount(2);
        leafContent.Folder.Ancestors![0].Name.Should().Be(grandparent.Name);
        leafContent.Folder.Ancestors![1].Name.Should().Be(parent.Name);
    }

    [Fact]
    public async Task bulk_creating_under_deeply_nested_parent_should_produce_audit_log_with_full_ancestor_path()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var grandparent = await CreateFolder(
            workspace: workspace,
            user: AppOwner);

        var parent = await CreateFolder(
            parent: grandparent,
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = parent.ExternalId.Value,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Leaf",
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Folders.Should().ContainSingle();
                details.Folders[0].Name.Should().Be("Leaf");
                details.Folders[0].FolderPath.Should().Be(grandparent.Name + "/" + parent.Name);
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_creating_with_ensure_unique_names_should_reuse_existing_and_create_missing()
    {
        //given
        Clock.SetToNow();

        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var existingFolder = await CreateFolder(
            name: "Existing",
            workspace: workspace,
            user: AppOwner);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Existing",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "NewChild",
                                Subfolders = null
                            }
                        ]
                    },
                    new FolderTreeDto
                    {
                        TemporaryId = 3,
                        Name = "BrandNew",
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.Items.Should().HaveCount(3);
        response.Items.Should().Contain(i => i.TemporaryId == 1 && i.ExternalId == existingFolder.ExternalId.Value);

        var existingContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: existingFolder.ExternalId,
            cookie: AppOwner.Cookie);

        existingContent.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("NewChild");

        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        topFolders.Subfolders.Should().Contain(s => s.Name == "Existing");
        topFolders.Subfolders.Should().Contain(s => s.Name == "BrandNew");
    }

    [Fact]
    public async Task bulk_creating_with_ensure_unique_names_should_only_audit_log_newly_created_folders()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        await CreateFolder(
            name: "Existing",
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Existing",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "NewChild",
                                Subfolders = null
                            }
                        ]
                    },
                    new FolderTreeDto
                    {
                        TemporaryId = 3,
                        Name = "BrandNew",
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Folders.Should().HaveCount(2);
                details.Folders.Should().Contain(f => f.Name == "NewChild" && f.FolderPath == "Existing");
                details.Folders.Should().Contain(f => f.Name == "BrandNew" && f.FolderPath == null);
                details.Folders.Should().NotContain(f => f.Name == "Existing");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_creating_when_all_folders_already_exist_should_return_existing_ids_and_create_nothing()
    {
        //given
        Clock.SetToNow();

        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var existing = await CreateFolder(
            name: "AlreadyHere",
            workspace: workspace,
            user: AppOwner);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "AlreadyHere",
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.Items.Should().ContainSingle()
            .Which.ExternalId.Should().Be(existing.ExternalId.Value);

        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        topFolders.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("AlreadyHere");
    }

    [Fact]
    public async Task bulk_creating_when_all_folders_already_exist_should_produce_audit_log_with_empty_folders()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        await CreateFolder(
            name: "AlreadyHere",
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "AlreadyHere",
                        Subfolders = null
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Folders.Should().BeEmpty();
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_creating_empty_tree_should_create_nothing()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = false,
                FolderTrees = []
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        (response.Items ?? []).Should().BeEmpty();

        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        topFolders.Subfolders.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task bulk_creating_multiple_sibling_trees_should_create_all_branches()
    {
        //given
        Clock.SetToNow();

        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
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
                        Name = "TreeA",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "TreeA_Child",
                                Subfolders = null
                            }
                        ]
                    },
                    new FolderTreeDto
                    {
                        TemporaryId = 3,
                        Name = "TreeB",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 4,
                                Name = "TreeB_Child",
                                Subfolders = null
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.Items.Should().HaveCount(4);

        var folderIdMap = response.Items.ToDictionary(x => x.TemporaryId, x => x.ExternalId);

        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie);

        topFolders.Subfolders.Should().HaveCount(2);
        topFolders.Subfolders.Should().Contain(s => s.Name == "TreeA");
        topFolders.Subfolders.Should().Contain(s => s.Name == "TreeB");

        var treeAContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[1]),
            cookie: AppOwner.Cookie);

        treeAContent.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("TreeA_Child");

        var treeBContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[3]),
            cookie: AppOwner.Cookie);

        treeBContent.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("TreeB_Child");
    }

    [Fact]
    public async Task bulk_creating_multiple_sibling_trees_should_produce_audit_log_with_correct_paths()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = false,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "TreeA",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "TreeA_Child",
                                Subfolders = null
                            }
                        ]
                    },
                    new FolderTreeDto
                    {
                        TemporaryId = 3,
                        Name = "TreeB",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 4,
                                Name = "TreeB_Child",
                                Subfolders = null
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Folders.Should().HaveCount(4);
                details.Folders.Should().Contain(f => f.Name == "TreeA" && f.FolderPath == null);
                details.Folders.Should().Contain(f => f.Name == "TreeA_Child" && f.FolderPath == "TreeA");
                details.Folders.Should().Contain(f => f.Name == "TreeB" && f.FolderPath == null);
                details.Folders.Should().Contain(f => f.Name == "TreeB_Child" && f.FolderPath == "TreeB");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }

    [Fact]
    public async Task bulk_creating_with_ensure_unique_names_should_create_new_subfolder_under_existing_parent_in_tree()
    {
        //given
        Clock.SetToNow();

        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        var existingParent = await CreateFolder(
            name: "Photos",
            workspace: workspace,
            user: AppOwner);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Photos",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "2024",
                                Subfolders =
                                [
                                    new FolderTreeDto
                                    {
                                        TemporaryId = 3,
                                        Name = "January",
                                        Subfolders = null
                                    }
                                ]
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        response.Items.Should().Contain(i => i.TemporaryId == 1 && i.ExternalId == existingParent.ExternalId.Value);

        var photosContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: existingParent.ExternalId,
            cookie: AppOwner.Cookie);

        photosContent.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("2024");

        var folderIdMap = response.Items.ToDictionary(x => x.TemporaryId, x => x.ExternalId);

        var folder2024Content = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[2]),
            cookie: AppOwner.Cookie);

        folder2024Content.Subfolders.Should().ContainSingle()
            .Which.Name.Should().Be("January");
    }

    [Fact]
    public async Task bulk_creating_with_ensure_unique_names_nested_existing_should_audit_log_only_new_branches()
    {
        //given
        var workspace = await CreateWorkspace(
            storage: Storage,
            user: AppOwner);

        await CreateFolder(
            name: "Photos",
            workspace: workspace,
            user: AppOwner);

        //when
        await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Photos",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 2,
                                Name = "2024",
                                Subfolders =
                                [
                                    new FolderTreeDto
                                    {
                                        TemporaryId = 3,
                                        Name = "January",
                                        Subfolders = null
                                    }
                                ]
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery);

        //then
        await AssertAuditLogContains<AuditLogDetails.Folder.BulkCreated>(
            expectedEventType: AuditLogEventTypes.Folder.BulkCreated,
            assertDetails: details =>
            {
                details.Folders.Should().HaveCount(2);
                details.Folders.Should().Contain(f => f.Name == "2024" && f.FolderPath == "Photos");
                details.Folders.Should().Contain(f => f.Name == "January" && f.FolderPath == "Photos/2024");
                details.Folders.Should().NotContain(f => f.Name == "Photos");
            },
            expectedActorEmail: AppOwner.Email,
            expectedSeverity: AuditLogSeverities.Info);
    }
}
