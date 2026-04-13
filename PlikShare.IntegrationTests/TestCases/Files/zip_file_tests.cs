using System.IO.Compression;
using System.Text;
using FluentAssertions;
using PlikShare.Core.Utils;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Zip;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

[Collection(IntegrationTestsCollection.Name)]
public class zip_file_tests : TestFixture
{
    public zip_file_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
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

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_file_listing_returns_correct_entries(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var fileContents = new Dictionary<string, byte[]>
        {
            ["hello.txt"] = "Hello World!"u8.ToArray(),
            ["data.bin"] = new byte[256],
            ["subdir/nested.txt"] = "Nested file content"u8.ToArray()
        };
        new Random(500).NextBytes(fileContents["data.bin"]);

        var zipContent = CreateZipArchive(fileContents);

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "test-archive.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then — verify against System.IO.Compression.ZipArchive as ground truth
        using var zipStream = new MemoryStream(zipContent);
        using var expectedArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        var expectedEntries = expectedArchive
            .Entries
            .Where(e => e.Length > 0)
            .ToList();

        zipDetails.Items.Should().HaveCount(expectedEntries.Count);

        for (var i = 0; i < expectedEntries.Count; i++)
        {
            var expected = expectedEntries[i];
            var actual = zipDetails.Items.Single(item => item.FilePath == expected.FullName);

            actual.SizeInBytes.Should().Be(expected.Length,
                $"SizeInBytes mismatch for '{expected.FullName}'");
            actual.CompressedSizeInBytes.Should().Be(expected.CompressedLength,
                $"CompressedSizeInBytes mismatch for '{expected.FullName}'");
            actual.FileNameLength.Should().Be((ushort)expected.FullName.Length,
                $"FileNameLength mismatch for '{expected.FullName}'");
            actual.IndexInArchive.Should().Be((uint)i,
                $"IndexInArchive mismatch for '{expected.FullName}'");
        }

        // Offsets should be strictly increasing (each entry starts after the previous one)
        var offsets = zipDetails
            .Items
            .OrderBy(i => i.IndexInArchive)
            .Select(i => i.OffsetToLocalFileHeader)
            .ToList();

        for (var i = 1; i < offsets.Count; i++)
        {
            offsets[i].Should().BeGreaterThan(offsets[i - 1],
                $"OffsetToLocalFileHeader should be strictly increasing (entry {i})");
        }
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_entry_download_returns_correct_content(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var textContent = "Entry content inside the ZIP!"u8.ToArray();
        var binaryContent = new byte[1024];
        new Random(502).NextBytes(binaryContent);

        var zipContent = CreateZipArchive(new Dictionary<string, byte[]>
        {
            ["entry.txt"] = textContent,
            ["payload.bin"] = binaryContent
        });

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "download-test.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //when — download each entry
        var textEntry = zipDetails.Items.Single(i => i.FilePath == "entry.txt");
        var textDownloadLink = await Api.Files.GetZipContentDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            item: new ZipFileDto(
                FilePath: textEntry.FilePath,
                CompressedSizeInBytes: textEntry.CompressedSizeInBytes,
                SizeInBytes: textEntry.SizeInBytes,
                OffsetToLocalFileHeader: textEntry.OffsetToLocalFileHeader,
                FileNameLength: textEntry.FileNameLength,
                CompressionMethod: textEntry.CompressionMethod,
                IndexInArchive: textEntry.IndexInArchive),
            contentDisposition: ContentDispositionType.Attachment,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            fullEncryptionSession: workspace.FullEncryptionSession);

        var textDownloaded = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: textDownloadLink.DownloadPreSignedUrl,
            cookie: user.Cookie);

        var binaryEntry = zipDetails.Items.Single(i => i.FilePath == "payload.bin");
        var binaryDownloadLink = await Api.Files.GetZipContentDownloadLink(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            item: new ZipFileDto(
                FilePath: binaryEntry.FilePath,
                CompressedSizeInBytes: binaryEntry.CompressedSizeInBytes,
                SizeInBytes: binaryEntry.SizeInBytes,
                OffsetToLocalFileHeader: binaryEntry.OffsetToLocalFileHeader,
                FileNameLength: binaryEntry.FileNameLength,
                CompressionMethod: binaryEntry.CompressionMethod,
                IndexInArchive: binaryEntry.IndexInArchive),
            contentDisposition: ContentDispositionType.Attachment,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            fullEncryptionSession: workspace.FullEncryptionSession);

        var binaryDownloaded = await Api.PreSignedFiles.DownloadFile(
            preSignedUrl: binaryDownloadLink.DownloadPreSignedUrl,
            cookie: user.Cookie);

        //then
        textDownloaded.Should().BeEquivalentTo(textContent);
        binaryDownloaded.Should().BeEquivalentTo(binaryContent);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task empty_zip_listing_returns_no_entries(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var zipContent = CreateZipArchive(new Dictionary<string, byte[]>());

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "empty.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        (zipDetails.Items ?? []).Should().BeEmpty();
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_with_many_files_returns_all_entries(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var fileContents = new Dictionary<string, byte[]>();
        var random = new Random(600);

        for (var i = 0; i < 50; i++)
        {
            var content = new byte[random.Next(10, 500)];
            random.NextBytes(content);
            fileContents[$"file-{i:D3}.bin"] = content;
        }

        var zipContent = CreateZipArchive(fileContents);

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "many-files.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        zipDetails.Items.Should().HaveCount(50);

        foreach (var (fileName, content) in fileContents)
        {
            var entry = zipDetails.Items.Single(i => i.FilePath == fileName);
            entry.SizeInBytes.Should().Be(content.Length,
                $"SizeInBytes mismatch for '{fileName}'");
        }

        // Indexes should be 0..49
        zipDetails.Items
            .Select(i => i.IndexInArchive)
            .OrderBy(i => i)
            .Should()
            .BeEquivalentTo(Enumerable.Range(0, 50).Select(i => (uint)i));
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_with_deeply_nested_directories_returns_full_paths(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var fileContents = new Dictionary<string, byte[]>
        {
            ["root.txt"] = "root"u8.ToArray(),
            ["a/level1.txt"] = "level 1"u8.ToArray(),
            ["a/b/level2.txt"] = "level 2"u8.ToArray(),
            ["a/b/c/level3.txt"] = "level 3"u8.ToArray(),
            ["a/b/c/d/level4.txt"] = "level 4"u8.ToArray(),
            ["x/y/sibling.txt"] = "sibling branch"u8.ToArray()
        };

        var zipContent = CreateZipArchive(fileContents);

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "nested-dirs.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        zipDetails.Items.Should().HaveCount(6);

        zipDetails.Items.Select(i => i.FilePath).Should().BeEquivalentTo([
            "root.txt",
            "a/level1.txt",
            "a/b/level2.txt",
            "a/b/c/level3.txt",
            "a/b/c/d/level4.txt",
            "x/y/sibling.txt"
        ]);

        foreach (var (fileName, content) in fileContents)
        {
            var entry = zipDetails.Items.Single(i => i.FilePath == fileName);
            entry.SizeInBytes.Should().Be(content.Length,
                $"SizeInBytes mismatch for '{fileName}'");
            entry.FileNameLength.Should().Be((ushort)fileName.Length,
                $"FileNameLength mismatch for '{fileName}'");
        }
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_with_single_file_returns_one_entry(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var content = "single file"u8.ToArray();
        var zipContent = CreateZipArchive(new Dictionary<string, byte[]>
        {
            ["only-file.txt"] = content
        });

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "single.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        zipDetails.Items.Should().HaveCount(1);
        var entry = zipDetails.Items[0];
        entry.FilePath.Should().Be("only-file.txt");
        entry.SizeInBytes.Should().Be(content.Length);
        entry.IndexInArchive.Should().Be(0);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_with_comment_is_parsed_correctly(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var zipContent = CreateZipArchiveWithComment(
            files: new Dictionary<string, byte[]>
            {
                ["file-in-commented-zip.txt"] = "content here"u8.ToArray()
            },
            comment: "This is a ZIP archive comment that makes the EOCD not at the last 22 bytes");

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "commented.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        zipDetails.Items.Should().HaveCount(1);
        zipDetails.Items[0].FilePath.Should().Be("file-in-commented-zip.txt");
        zipDetails.Items[0].SizeInBytes.Should().Be(12);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_with_long_file_names_returns_correct_paths(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var longPath = string.Join("/",
            Enumerable.Range(0, 20).Select(i => $"directory-level-{i:D2}"))
            + "/final-file.txt";

        var fileContents = new Dictionary<string, byte[]>
        {
            [longPath] = "deep content"u8.ToArray()
        };

        var zipContent = CreateZipArchive(fileContents);

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "long-names.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        zipDetails.Items.Should().HaveCount(1);
        zipDetails.Items[0].FilePath.Should().Be(longPath);
        zipDetails.Items[0].FileNameLength.Should().Be((ushort)longPath.Length);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task zip_with_mix_of_empty_and_nonempty_files_returns_only_nonempty(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var fileContents = new Dictionary<string, byte[]>
        {
            ["empty1.txt"] = [],
            ["has-content.txt"] = "I have content"u8.ToArray(),
            ["empty2.dat"] = [],
            ["also-content.bin"] = new byte[] { 1, 2, 3 },
            ["empty3.log"] = []
        };

        var zipContent = CreateZipArchive(fileContents);

        var uploadedFile = await UploadFile(
            content: zipContent,
            fileName: "mixed-empty.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var zipDetails = await Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then — only non-empty files should be returned
        zipDetails.Items.Should().HaveCount(2);
        zipDetails.Items.Select(i => i.FilePath).Should().BeEquivalentTo(
            ["has-content.txt", "also-content.bin"]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task non_zip_file_with_zip_extension_returns_error(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var notAZip = "this is definitely not a zip file, just random text"u8.ToArray();

        var uploadedFile = await UploadFile(
            content: notAZip,
            fileName: "fake-archive.zip",
            contentType: "application/zip",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var act = () => Api.Files.GetZipDetails(
            workspaceExternalId: workspace.ExternalId,
            fileExternalId: uploadedFile.ExternalId,
            cookie: user.Cookie,
            fullEncryptionSession: workspace.FullEncryptionSession);

        //then
        var exception = await act.Should().ThrowAsync<TestApiCallException>();
        exception.Which.StatusCode.Should().Be(400);
    }

    private static byte[] CreateZipArchiveWithComment(
        Dictionary<string, byte[]> files,
        string comment)
    {
        var zipBytes = CreateZipArchive(files);
        var commentBytes = Encoding.UTF8.GetBytes(comment);

        // EOCD is at the end of the ZIP. Its last 2 bytes before any existing comment
        // are the comment length field. We need to find the EOCD, update the comment
        // length, and append the comment.

        // Find EOCD signature (0x06054b50) scanning backwards
        var eocdOffset = -1;
        for (var i = zipBytes.Length - 22; i >= 0; i--)
        {
            if (zipBytes[i] == 0x50 && zipBytes[i + 1] == 0x4b &&
                zipBytes[i + 2] == 0x05 && zipBytes[i + 3] == 0x06)
            {
                eocdOffset = i;
                break;
            }
        }

        if (eocdOffset < 0)
            throw new InvalidOperationException("EOCD not found");

        // Write comment length at EOCD offset + 20 (2 bytes, little-endian)
        var result = new byte[zipBytes.Length + commentBytes.Length];
        Buffer.BlockCopy(zipBytes, 0, result, 0, zipBytes.Length);
        result[eocdOffset + 20] = (byte)(commentBytes.Length & 0xFF);
        result[eocdOffset + 21] = (byte)((commentBytes.Length >> 8) & 0xFF);
        Buffer.BlockCopy(commentBytes, 0, result, zipBytes.Length, commentBytes.Length);

        return result;
    }
}
