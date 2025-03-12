using FluentAssertions;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.

namespace PlikShare.IntegrationTests.TestCases.Folders;

[Collection(IntegrationTestsCollection.Name)]
public class create_folder_tests: TestFixture
{
    [Fact]
    public async Task when_folder_is_created_it_should_be_visible_in_the_workspace()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        //when
        var folderResponse = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my first folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        topFolders.Should().BeEquivalentTo(new GetTopFolderContentResponseDto
        {
            Subfolders =
            [
                new SubfolderDto()
                {
                    ExternalId = folderResponse.ExternalId.Value,
                    Name = "my first folder",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],

            Files = null,
            Uploads = null
        });
    }
    
    [Fact]
    public async Task can_create_many_folders_directly_in_the_workspace()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        //when
        var folder1Response = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my first folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        var folder2Response = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my second folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);
        
        var folder3Response = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my third folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);
        
        //then
        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        topFolders.Should().BeEquivalentTo(new GetTopFolderContentResponseDto
        {
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folder1Response.ExternalId.Value,
                    Name = "my first folder",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folder2Response.ExternalId.Value,
                    Name = "my second folder",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folder3Response.ExternalId.Value,
                    Name = "my third folder",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],

            Files = null,
            Uploads = null
        });
    }
    
    [Fact]
    public async Task when_folder_is_created_its_content_should_be_empty()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        //when
        var folderResponse = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my first folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var folderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: folderResponse.ExternalId,
            cookie: user.Cookie);

        folderContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = folderResponse.ExternalId.Value,
                Name = "my first folder",
                Ancestors = null 
            },
            Subfolders = null,
            Uploads = null,
            Files = null
        });
    }
    
    [Fact]
    public async Task when_subfolder_is_created_it_is_visible_in_its_parent_content()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        var parentFolder = await CreateFolder(
            workspace: workspace,
            cookie: user.Cookie);
        
        //when
        var subfolder = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = parentFolder.ExternalId,
                Name = "my subfolder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var parentContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parentFolder.ExternalId,
            cookie: user.Cookie);

        parentContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = parentFolder.ExternalId.Value,
                Name = parentFolder.Name,
                Ancestors = null 
            },
            Subfolders = [new SubfolderDto
            {
                ExternalId = subfolder.ExternalId.Value,
                Name = "my subfolder",
                WasCreatedByUser = true,
                CreatedAt = Clock.UtcNow.DateTime
            }],
            Uploads = null,
            Files = null
        });
    }
    
    [Fact]
    public async Task when_subfolder_is_created_its_content_should_be_empty()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        var parentFolder = await CreateFolder(
            workspace: workspace,
            cookie: user.Cookie);
        
        //when
        var subfolder = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = parentFolder.ExternalId,
                Name = "my subfolder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var subfolderContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: subfolder.ExternalId,
            cookie: user.Cookie);

        subfolderContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = subfolder.ExternalId.Value,
                Name = "my subfolder",
                Ancestors = [new AncestorFolderDto
                {
                    ExternalId = parentFolder.ExternalId.Value,
                    Name = parentFolder.Name
                }] 
            },
            Subfolders = null,
            Uploads = null,
            Files = null
        });
    }

    [Fact]
    public async Task when_folder_with_unique_name_is_created_for_the_first_time_it_should_be_visible_in_workspace()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        //when
        var folderResponse = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my first folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        topFolders.Should().BeEquivalentTo(new GetTopFolderContentResponseDto
        {
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folderResponse.ExternalId.Value,
                    Name = "my first folder",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],

            Files = null,
            Uploads = null
        });
    }

    [Fact]
    public async Task when_folder_with_given_name_already_exists_and_uniqueness_is_required_external_id_of_existing_folder_should_be_returned_and_no_new_folder_should_be_created()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);
        
        var originalFolderResponse = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = null,
                Name = "my first folder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //when
        var folderResponse = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                EnsureUniqueNames = true,
                ParentExternalId = null,
                FolderTrees = [
                    new FolderTreeDto
                    {
                        Name = "my first folder",
                        Subfolders = null,
                        TemporaryId = 1
                    }
                    ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        folderResponse.Items.Should().BeEquivalentTo([
            new BulkCreateFolderItemDto
            {
                ExternalId = originalFolderResponse.ExternalId.Value,
                TemporaryId = 1
            }
        ]);

        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        topFolders.Should().BeEquivalentTo(new GetTopFolderContentResponseDto
        {
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = originalFolderResponse.ExternalId.Value,
                    Name = "my first folder",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],

            Files = null,
            Uploads = null
        });
    }

    [Fact]
    public async Task when_subfolder_with_unique_name_is_created_for_the_first_time_it_is_visible_in_its_parent_content()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        var parentFolder = await CreateFolder(
            workspace: workspace,
            cookie: user.Cookie);

        //when
        var subfolder = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = parentFolder.ExternalId,
                Name = "my subfolder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var parentContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parentFolder.ExternalId,
            cookie: user.Cookie);

        parentContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = parentFolder.ExternalId.Value,
                Name = parentFolder.Name,
                Ancestors = null
            },
            Subfolders = [new SubfolderDto
            {
                ExternalId = subfolder.ExternalId.Value,
                Name = "my subfolder",
                WasCreatedByUser = true,
                CreatedAt = Clock.UtcNow.DateTime
            }],
            Uploads = null,
            Files = null
        });
    }

    [Fact]
    public async Task when_subfolder_with_given_name_already_exists_and_uniqueness_is_required_external_id_of_existing_subfolder_should_be_returned_and_no_new_folder_should_be_created()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        var parentFolder = await CreateFolder(
            workspace: workspace,
            cookie: user.Cookie);
        
        var originalSubfolder = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = parentFolder.ExternalId,
                Name = "my subfolder"
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //when
        var response = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                EnsureUniqueNames = true,
                ParentExternalId = parentFolder.ExternalId.Value,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        Name = "my subfolder",
                        Subfolders = null,
                        TemporaryId = 1
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        response.Items.Should().BeEquivalentTo([
            new BulkCreateFolderItemDto
            {
                ExternalId = originalSubfolder.ExternalId.Value,
                TemporaryId = 1
            }
        ]);

        var parentContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: parentFolder.ExternalId,
            cookie: user.Cookie);

        parentContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = parentFolder.ExternalId.Value,
                Name = parentFolder.Name,
                Ancestors = null
            },
            Subfolders = [new SubfolderDto
            {
                ExternalId = originalSubfolder.ExternalId.Value,
                Name = "my subfolder",
                WasCreatedByUser = true,
                CreatedAt = Clock.UtcNow.DateTime
            }],
            Uploads = null,
            Files = null
        });
    }

    [Fact]
    public async Task can_create_folders_in_bulk()
    {
        //given
        Clock.SetToNow();

        var user = await SignIn(
            user: Users.AppOwner);

        var hardDrive = await CreateHardDriveStorage(
            cookie: user.Cookie);

        var workspace = await CreateWorkspace(
            storage: hardDrive,
            cookie: user.Cookie);

        //when
        var bulkResponse = await Api.Folders.BulkCreate(
            request: new BulkCreateFolderRequestDto
            {
                ParentExternalId = null,
                EnsureUniqueNames = true,
                FolderTrees =
                [
                    new FolderTreeDto
                    {
                        TemporaryId = 1,
                        Name = "Folder A",
                        Subfolders =
                        [
                            new FolderTreeDto()
                            {
                                TemporaryId = 2,
                                Name = "Folder A_A",
                                Subfolders = null
                            },

                            new FolderTreeDto
                            {
                                TemporaryId = 3,
                                Name = "Folder A_B",
                                Subfolders =
                                [
                                    new FolderTreeDto
                                    {
                                        TemporaryId = 4,
                                        Name = "Folder A_B_A",
                                        Subfolders = null
                                    },

                                    new FolderTreeDto
                                    {
                                        TemporaryId = 5,
                                        Name = "Folder A_B_B",
                                        Subfolders = null
                                    },

                                    new FolderTreeDto
                                    {
                                        TemporaryId = 6,
                                        Name = "Folder A_B_C",
                                        Subfolders = null
                                    }
                                ]
                            },

                            new FolderTreeDto
                            {
                                TemporaryId = 7,
                                Name = "Folder A_C",
                                Subfolders = null
                            }
                        ]
                    },

                    new FolderTreeDto
                    {
                        TemporaryId = 8,
                        Name = "Folder B",
                        Subfolders =
                        [
                            new FolderTreeDto
                            {
                                TemporaryId = 9,
                                Name = "Folder B_A",
                                Subfolders = null
                            },

                            new FolderTreeDto
                            {
                                TemporaryId = 10,
                                Name = "Folder B_B",
                                Subfolders = null
                            },

                            new FolderTreeDto
                            {
                                TemporaryId = 11,
                                Name = "Folder B_C",
                                Subfolders = null
                            }
                        ]
                    }
                ]
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        //then
        var topFolders = await Api.Folders.GetTop(
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie);

        var folderIdMap = bulkResponse
            .Items
            .ToDictionary(x => x.TemporaryId, x => x.ExternalId);

        topFolders.Should().BeEquivalentTo(new GetTopFolderContentResponseDto
        {
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folderIdMap[1],
                    Name = "Folder A",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[8],
                    Name = "Folder B",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],
            Files = null,
            Uploads = null
        });

        // Verify Folder A structure
        var folderAContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[1]), 
            cookie: user.Cookie);

        folderAContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = folderIdMap[1],
                Name = "Folder A",
                Ancestors = null
            },
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folderIdMap[2],
                    Name = "Folder A_A",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[3],
                    Name = "Folder A_B",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[7],
                    Name = "Folder A_C",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],
            Files = null,
            Uploads = null
        });

        // Verify Folder A_B structure
        var folderABContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[3]),
            cookie: user.Cookie);

        folderABContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = folderIdMap[3],
                Name = "Folder A_B",
                Ancestors =
                [
                    new AncestorFolderDto
                    {
                        ExternalId = folderIdMap[1],
                        Name = "Folder A"
                    }
                ]
            },
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folderIdMap[4],
                    Name = "Folder A_B_A",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[5],
                    Name = "Folder A_B_B",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[6],
                    Name = "Folder A_B_C",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],
            Files = null,
            Uploads = null
        });

        // Verify Folder B structure
        var folderBContent = await Api.Folders.Get(
            workspaceExternalId: workspace.ExternalId,
            folderExternalId: FolderExtId.Parse(folderIdMap[8]),
            cookie: user.Cookie);

        folderBContent.Should().BeEquivalentTo(new GetFolderContentResponseDto
        {
            Folder = new CurrentFolderDto
            {
                ExternalId = folderIdMap[8],
                Name = "Folder B",
                Ancestors = null
            },
            Subfolders =
            [
                new SubfolderDto
                {
                    ExternalId = folderIdMap[9],
                    Name = "Folder B_A",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[10],
                    Name = "Folder B_B",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                },
                new SubfolderDto
                {
                    ExternalId = folderIdMap[11],
                    Name = "Folder B_C",
                    WasCreatedByUser = true,
                    CreatedAt = Clock.UtcNow.DateTime
                }
            ],
            Files = null,
            Uploads = null
        });
    }

    public create_folder_tests(HostFixture8081 hostFixture, ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    { 
    }
}