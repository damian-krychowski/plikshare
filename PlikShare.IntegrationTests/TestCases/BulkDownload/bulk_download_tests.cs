using System.IO.Compression;
using System.Text;
using FluentAssertions;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.BulkDownload;

[Collection(IntegrationTestsCollection.Name)]
public class bulk_download_tests : TestFixture
{
    private AppSignedInUser AppOwner { get; }
    private AppStorage Storage { get; }

    public bulk_download_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper) : base(hostFixture, testOutputHelper)
    {
        AppOwner = SignIn(
            user: Users.AppOwner).Result;

        Storage = CreateHardDriveStorage(
            user: AppOwner,
            encryptionType: StorageEncryptionType.None).Result;
    }

    [Fact]
    public async Task bulk_download_of_single_folder_should_contain_all_files()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var file1Content = Encoding.UTF8.GetBytes("file one content");
        var file2Content = Encoding.UTF8.GetBytes("file two content");

        var file1 = await UploadFile(
            content: file1Content, fileName: "file1.txt", contentType: "text/plain",
            folder: folder, workspace: workspace, user: AppOwner);

        var file2 = await UploadFile(
            content: file2Content, fileName: "file2.txt", contentType: "text/plain",
            folder: folder, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [],
            selectedFolders: [folder.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(2);
        entries.Should().ContainKey($"{folder.Name}/file1.txt")
            .WhoseValue.Should().Equal(file1Content);
        entries.Should().ContainKey($"{folder.Name}/file2.txt")
            .WhoseValue.Should().Equal(file2Content);
    }

    [Fact]
    public async Task bulk_download_of_nested_folders_should_preserve_structure()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var rootFolder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);
        var childFolder = await CreateFolder(parent: rootFolder, workspace: workspace, user: AppOwner);

        var rootFileContent = Encoding.UTF8.GetBytes("root file");
        var childFileContent = Encoding.UTF8.GetBytes("child file");

        await UploadFile(
            content: rootFileContent, fileName: "root.txt", contentType: "text/plain",
            folder: rootFolder, workspace: workspace, user: AppOwner);

        await UploadFile(
            content: childFileContent, fileName: "child.txt", contentType: "text/plain",
            folder: childFolder, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [],
            selectedFolders: [rootFolder.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(2);
        entries.Should().ContainKey($"{rootFolder.Name}/root.txt")
            .WhoseValue.Should().Equal(rootFileContent);
        entries.Should().ContainKey($"{rootFolder.Name}/{childFolder.Name}/child.txt")
            .WhoseValue.Should().Equal(childFileContent);
    }

    [Fact]
    public async Task bulk_download_of_individual_files_should_place_them_at_zip_root()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var file1Content = Encoding.UTF8.GetBytes("first");
        var file2Content = Encoding.UTF8.GetBytes("second");

        var file1 = await UploadFile(
            content: file1Content, fileName: "a.txt", contentType: "text/plain",
            folder: folder, workspace: workspace, user: AppOwner);

        var file2 = await UploadFile(
            content: file2Content, fileName: "b.txt", contentType: "text/plain",
            folder: folder, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [file1.ExternalId, file2.ExternalId],
            selectedFolders: []);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(2);
        entries.Should().ContainKey("a.txt")
            .WhoseValue.Should().Equal(file1Content);
        entries.Should().ContainKey("b.txt")
            .WhoseValue.Should().Equal(file2Content);
    }

    [Fact]
    public async Task bulk_download_mixing_files_and_folders_should_work()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folderA = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);
        var folderB = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var fileAContent = Encoding.UTF8.GetBytes("in folder A");
        var fileBContent = Encoding.UTF8.GetBytes("standalone file in B");

        await UploadFile(
            content: fileAContent, fileName: "a.txt", contentType: "text/plain",
            folder: folderA, workspace: workspace, user: AppOwner);

        var standaloneFile = await UploadFile(
            content: fileBContent, fileName: "b.txt", contentType: "text/plain",
            folder: folderB, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [standaloneFile.ExternalId],
            selectedFolders: [folderA.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(2);
        entries.Should().ContainKey($"{folderA.Name}/a.txt")
            .WhoseValue.Should().Equal(fileAContent);
        entries.Should().ContainKey("b.txt")
            .WhoseValue.Should().Equal(fileBContent);
    }

    [Fact]
    public async Task bulk_download_with_excluded_files_should_skip_them()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        var keepContent = Encoding.UTF8.GetBytes("keep me");
        var excludeContent = Encoding.UTF8.GetBytes("exclude me");

        var keepFile = await UploadFile(
            content: keepContent, fileName: "keep.txt", contentType: "text/plain",
            folder: folder, workspace: workspace, user: AppOwner);

        var excludeFile = await UploadFile(
            content: excludeContent, fileName: "exclude.txt", contentType: "text/plain",
            folder: folder, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [],
            selectedFolders: [folder.ExternalId],
            excludedFiles: [excludeFile.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(1);
        entries.Should().ContainKey($"{folder.Name}/keep.txt")
            .WhoseValue.Should().Equal(keepContent);
        entries.Should().NotContainKey($"{folder.Name}/exclude.txt");
    }

    [Fact]
    public async Task bulk_download_with_excluded_subfolder_should_skip_its_contents()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var rootFolder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);
        var keepFolder = await CreateFolder(parent: rootFolder, workspace: workspace, user: AppOwner);
        var excludeFolder = await CreateFolder(parent: rootFolder, workspace: workspace, user: AppOwner);

        var keepContent = Encoding.UTF8.GetBytes("keep");
        var excludeContent = Encoding.UTF8.GetBytes("exclude");

        await UploadFile(
            content: keepContent, fileName: "keep.txt", contentType: "text/plain",
            folder: keepFolder, workspace: workspace, user: AppOwner);

        await UploadFile(
            content: excludeContent, fileName: "excluded.txt", contentType: "text/plain",
            folder: excludeFolder, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [],
            selectedFolders: [rootFolder.ExternalId],
            excludedFolders: [excludeFolder.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(1);
        entries.Should().ContainKey($"{rootFolder.Name}/{keepFolder.Name}/keep.txt")
            .WhoseValue.Should().Equal(keepContent);
    }

    [Fact]
    public async Task bulk_download_of_deeply_nested_structure_should_preserve_full_path()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var level1 = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);
        var level2 = await CreateFolder(parent: level1, workspace: workspace, user: AppOwner);
        var level3 = await CreateFolder(parent: level2, workspace: workspace, user: AppOwner);

        var deepContent = Encoding.UTF8.GetBytes("deep file");

        await UploadFile(
            content: deepContent, fileName: "deep.txt", contentType: "text/plain",
            folder: level3, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [],
            selectedFolders: [level1.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);

        entries.Should().HaveCount(1);
        entries.Should().ContainKey($"{level1.Name}/{level2.Name}/{level3.Name}/deep.txt")
            .WhoseValue.Should().Equal(deepContent);
    }

    [Fact]
    public async Task bulk_download_of_empty_folder_should_produce_empty_zip()
    {
        //given
        var workspace = await CreateWorkspace(storage: Storage, user: AppOwner);
        var folder = await CreateFolder(parent: null, workspace: workspace, user: AppOwner);

        //when
        var zipBytes = await BulkDownload(
            workspace: workspace,
            selectedFiles: [],
            selectedFolders: [folder.ExternalId]);

        //then
        var entries = ExtractZipEntries(zipBytes);
        entries.Should().BeEmpty();
    }

    private async Task<byte[]> BulkDownload(
        AppWorkspace workspace,
        List<FileExtId> selectedFiles,
        List<FolderExtId> selectedFolders,
        List<FileExtId>? excludedFiles = null,
        List<FolderExtId>? excludedFolders = null)
    {
        var linkResponse = await Api.Files.GetBulkDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            selectedFiles: selectedFiles,
            selectedFolders: selectedFolders,
            cookie: AppOwner.Cookie,
            antiforgery: AppOwner.Antiforgery,
            excludedFiles: excludedFiles,
            excludedFolders: excludedFolders);

        return await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: linkResponse.PreSignedUrl,
            cookie: AppOwner.Cookie);
    }

    private static Dictionary<string, byte[]> ExtractZipEntries(byte[] zipBytes)
    {
        using var zipStream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var entries = new Dictionary<string, byte[]>();

        foreach (var entry in archive.Entries)
        {
            if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                continue; // skip directory entries

            using var entryStream = entry.Open();
            using var ms = new MemoryStream();
            entryStream.CopyTo(ms);
            entries[entry.FullName] = ms.ToArray();
        }

        return entries;
    }
}
