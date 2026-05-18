using System.IO.Compression;
using FluentAssertions;
using PlikShare.Files.Preview.GetZipDetails.Contracts;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

[Collection(IntegrationTestsCollection.Name)]
public class zip_bulk_download_tests : TestFixture
{
    public zip_bulk_download_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task selecting_a_folder_extracts_its_contents_preserving_inner_structure(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["src/main.cs"] = "main"u8.ToArray(),
                ["src/util.cs"] = "util"u8.ToArray(),
                ["src/nested/deep.cs"] = "deep"u8.ToArray(),
                ["readme.txt"] = "readme"u8.ToArray()
            });

        var srcFolder = details.Folders.Single(f => f.Name == "src");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedFolderIds: [srcFolder.Id]);

        //then — only src tree, with "src" as the output root (readme.txt is filtered out)
        entries.Should().HaveCount(3);
        entries["src/main.cs"].Should().Equal(sourceContents["src/main.cs"]);
        entries["src/util.cs"].Should().Equal(sourceContents["src/util.cs"]);
        entries["src/nested/deep.cs"].Should().Equal(sourceContents["src/nested/deep.cs"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task selecting_individual_entries_places_them_at_zip_root(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["a/one.txt"] = "one"u8.ToArray(),
                ["b/two.txt"] = "two"u8.ToArray(),
                ["unused.txt"] = "unused"u8.ToArray()
            });

        var one = details.Items.Single(i => i.FileName == "one.txt");
        var two = details.Items.Single(i => i.FileName == "two.txt");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedEntryIndices: [one.IndexInArchive, two.IndexInArchive]);

        //then — both files land at root by basename; unused.txt is excluded
        entries.Should().HaveCount(2);
        entries["one.txt"].Should().Equal(sourceContents["a/one.txt"]);
        entries["two.txt"].Should().Equal(sourceContents["b/two.txt"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    public async Task excluding_a_subfolder_drops_its_entries_from_the_output(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["root/keep.txt"] = "keep"u8.ToArray(),
                ["root/skip/dropped.txt"] = "dropped"u8.ToArray(),
                ["root/skip/also-dropped.txt"] = "also"u8.ToArray()
            });

        var rootFolder = details.Folders.Single(f => f.Name == "root");
        var skipFolder = details.Folders.Single(f => f.Name == "skip");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedFolderIds: [rootFolder.Id],
            excludedFolderIds: [skipFolder.Id]);

        //then — only the surviving "root/keep.txt", with "skip" subtree pruned
        entries.Should().HaveCount(1);
        entries["root/keep.txt"].Should().Equal(sourceContents["root/keep.txt"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    public async Task excluding_a_single_entry_inside_a_selected_folder_drops_just_that_entry(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["data/a.txt"] = "a"u8.ToArray(),
                ["data/b.txt"] = "b"u8.ToArray(),
                ["data/c.txt"] = "c"u8.ToArray()
            });

        var dataFolder = details.Folders.Single(f => f.Name == "data");
        var b = details.Items.Single(i => i.FileName == "b.txt");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedFolderIds: [dataFolder.Id],
            excludedEntryIndices: [b.IndexInArchive]);

        //then — a and c survive, b is dropped
        entries.Should().HaveCount(2);
        entries["data/a.txt"].Should().Equal(sourceContents["data/a.txt"]);
        entries["data/c.txt"].Should().Equal(sourceContents["data/c.txt"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    public async Task selecting_multiple_top_level_folders_emits_both_subtrees(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["alpha/x.txt"] = "x"u8.ToArray(),
                ["beta/y.txt"] = "y"u8.ToArray(),
                ["gamma/z.txt"] = "z"u8.ToArray()
            });

        var alpha = details.Folders.Single(f => f.Name == "alpha");
        var beta = details.Folders.Single(f => f.Name == "beta");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedFolderIds: [alpha.Id, beta.Id]);

        //then — alpha and beta survive (each at its own root), gamma is dropped
        entries.Should().HaveCount(2);
        entries["alpha/x.txt"].Should().Equal(sourceContents["alpha/x.txt"]);
        entries["beta/y.txt"].Should().Equal(sourceContents["beta/y.txt"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    public async Task mixing_a_selected_folder_with_an_individual_entry_combines_both(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["pack/inside.txt"] = "inside"u8.ToArray(),
                ["standalone/buried.txt"] = "buried"u8.ToArray()
            });

        var pack = details.Folders.Single(f => f.Name == "pack");
        var buried = details.Items.Single(i => i.FileName == "buried.txt");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedFolderIds: [pack.Id],
            selectedEntryIndices: [buried.IndexInArchive]);

        //then — folder subtree under its name, plus the standalone entry at root
        entries.Should().HaveCount(2);
        entries["pack/inside.txt"].Should().Equal(sourceContents["pack/inside.txt"]);
        entries["buried.txt"].Should().Equal(sourceContents["standalone/buried.txt"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    public async Task selecting_a_nested_subfolder_strips_ancestors_above_it(
        StorageEncryptionType encryptionType)
    {
        //given
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: encryptionType,
            fileContents: new Dictionary<string, byte[]>
            {
                ["outer/middle/inner/leaf.txt"] = "leaf"u8.ToArray(),
                ["outer/middle/sibling.txt"] = "sibling"u8.ToArray(),
                ["outer/top.txt"] = "top"u8.ToArray()
            });

        var innerFolder = details.Folders.Single(f => f.Name == "inner");

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedFolderIds: [innerFolder.Id]);

        //then — selection root becomes "inner", outer/middle prefix is stripped
        entries.Should().HaveCount(1);
        entries["inner/leaf.txt"].Should().Equal(sourceContents["outer/middle/inner/leaf.txt"]);
    }

    [Fact]
    public async Task empty_selection_returns_bad_request()
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, StorageEncryptionType.None);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var zipContent = CreateZipArchive(new Dictionary<string, byte[]>
        {
            ["only.txt"] = "only"u8.ToArray()
        });

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "tiny.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when — no selectedFolders, no selectedEntries → server must refuse
        var act = () => Api.Files.GetZipBulkDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            selectedFolderIds: [],
            selectedEntryIndices: [],
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        //then
        var exception = await act.Should().ThrowAsync<TestApiCallException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task collisions_across_selection_roots_are_disambiguated()
    {
        //given — two distinct entries share the same basename but live in different folders;
        //selecting them as individual files would both want to land at the zip root.
        var (user, workspace, uploadedFile, details, sourceContents) = await SetupZipPreview(
            encryptionType: StorageEncryptionType.None,
            fileContents: new Dictionary<string, byte[]>
            {
                ["dir-a/clash.txt"] = "a"u8.ToArray(),
                ["dir-b/clash.txt"] = "b"u8.ToArray()
            });

        var allClashes = details.Items.Where(i => i.FileName == "clash.txt").ToList();
        allClashes.Should().HaveCount(2);

        //when
        var entries = await DownloadZipBulk(
            user: user,
            workspace: workspace,
            uploadedFile: uploadedFile,
            selectedEntryIndices: allClashes.Select(i => i.IndexInArchive).ToArray());

        //then — both entries are preserved; UniqueFileNames appends " (1)" to the second one
        entries.Should().HaveCount(2);
        entries.Keys.Should().Contain("clash.txt");
        entries.Keys.Should().ContainSingle(k => k != "clash.txt" && k.StartsWith("clash") && k.EndsWith(".txt"));
    }

    private async Task<Dictionary<string, byte[]>> DownloadZipBulk(
        AppSignedInUser user,
        AppWorkspace workspace,
        AppFile uploadedFile,
        uint[]? selectedFolderIds = null,
        uint[]? selectedEntryIndices = null,
        uint[]? excludedFolderIds = null,
        uint[]? excludedEntryIndices = null)
    {
        var linkResponse = await Api.Files.GetZipBulkDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            selectedFolderIds: selectedFolderIds ?? [],
            selectedEntryIndices: selectedEntryIndices ?? [],
            excludedFolderIds: excludedFolderIds,
            excludedEntryIndices: excludedEntryIndices,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        var zipBytes = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.DownloadPreSignedUrl,
            cookie: user.Cookie);

        return ExtractZipEntries(zipBytes);
    }

    private async Task<(
        AppSignedInUser User,
        AppWorkspace Workspace,
        AppFile UploadedFile,
        GetZipFileDetailsResponseDto Details,
        Dictionary<string, byte[]> FileContents)> SetupZipPreview(
            StorageEncryptionType encryptionType,
            Dictionary<string, byte[]> fileContents)
    {
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var zipContent = CreateZipArchive(fileContents);

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "source.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        var details = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        return (user, workspace, uploadedFile, details, fileContents);
    }

    private static byte[] CreateZipArchive(Dictionary<string, byte[]> files)
    {
        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, content) in files)
            {
                var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        return memoryStream.ToArray();
    }

    private static Dictionary<string, byte[]> ExtractZipEntries(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                continue;

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            entries[entry.FullName] = ms.ToArray();
        }

        return entries;
    }
}
