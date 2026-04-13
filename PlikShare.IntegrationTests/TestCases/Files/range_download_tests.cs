using FluentAssertions;
using PlikShare.IntegrationTests.Infrastructure;
using PlikShare.Storages.Encryption;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.TestCases.Files;

[Collection(IntegrationTestsCollection.Name)]
public class range_download_tests : TestFixture
{
    public range_download_tests(
        HostFixture8081 hostFixture,
        ITestOutputHelper testOutputHelper)
        : base(hostFixture, testOutputHelper)
    {
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task range_download_first_100_bytes(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var originalContent = new byte[4096];
        new Random(300).NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-test.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var result = await DownloadFileRange(
            fileExternalId: uploadedFile.ExternalId,
            rangeStart: 0,
            rangeEnd: 99,
            workspace: workspace,
            user: user);

        //then
        result.StatusCode.Should().Be(206);
        result.ContentRange.Should().Be("bytes 0-99/4096");
        result.Content.Should().HaveCount(100);
        result.Content.Should().BeEquivalentTo(
            originalContent.AsSpan(0, 100).ToArray());
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task range_download_middle_chunk(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var originalContent = new byte[4096];
        new Random(302).NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-middle.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var result = await DownloadFileRange(
            fileExternalId: uploadedFile.ExternalId,
            rangeStart: 1000,
            rangeEnd: 1999,
            workspace: workspace,
            user: user);

        //then
        result.StatusCode.Should().Be(206);
        result.ContentRange.Should().Be("bytes 1000-1999/4096");
        result.Content.Should().HaveCount(1000);
        result.Content.Should().BeEquivalentTo(
            originalContent.AsSpan(1000, 1000).ToArray());
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task range_download_last_bytes(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var originalContent = new byte[4096];
        new Random(304).NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-last.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var result = await DownloadFileRange(
            fileExternalId: uploadedFile.ExternalId,
            rangeStart: 4000,
            rangeEnd: 4095,
            workspace: workspace,
            user: user);

        //then
        result.StatusCode.Should().Be(206);
        result.ContentRange.Should().Be("bytes 4000-4095/4096");
        result.Content.Should().HaveCount(96);
        result.Content.Should().BeEquivalentTo(
            originalContent.AsSpan(4000, 96).ToArray());
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task range_download_single_byte(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var originalContent = new byte[4096];
        new Random(306).NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-single.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var result = await DownloadFileRange(
            fileExternalId: uploadedFile.ExternalId,
            rangeStart: 2048,
            rangeEnd: 2048,
            workspace: workspace,
            user: user);

        //then
        result.StatusCode.Should().Be(206);
        result.ContentRange.Should().Be("bytes 2048-2048/4096");
        result.Content.Should().HaveCount(1);
        result.Content[0].Should().Be(originalContent[2048]);
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task range_download_crossing_segment_boundary(
        StorageEncryptionType encryptionType)
    {
        // File >1 segment, range spans the boundary between segment 1 and 2.
        // FirstSegmentCiphertextSize = 1,048,519 bytes of plaintext in segment 1.
        // Range [1,048,500 - 1,048,600] crosses from segment 1 into segment 2.

        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var originalContent = new byte[2 * 1024 * 1024]; // 2MB -> 2 segments
        new Random(307).NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-cross-segment.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var rangeStart = 1_048_500L;
        var rangeEnd = 1_048_600L;
        var expectedLength = (int)(rangeEnd - rangeStart + 1);

        var result = await DownloadFileRange(
            fileExternalId: uploadedFile.ExternalId,
            rangeStart: rangeStart,
            rangeEnd: rangeEnd,
            workspace: workspace,
            user: user);

        //then
        result.StatusCode.Should().Be(206);
        result.ContentRange.Should().Be($"bytes {rangeStart}-{rangeEnd}/{originalContent.Length}");
        result.Content.Should().HaveCount(expectedLength);
        result.Content.Should().BeEquivalentTo(
            originalContent.AsSpan((int)rangeStart, expectedLength).ToArray());
    }

    [Theory]
    [InlineData(StorageEncryptionType.None)]
    [InlineData(StorageEncryptionType.Managed)]
    [InlineData(StorageEncryptionType.Full)]
    public async Task range_download_entire_file_as_range(
        StorageEncryptionType encryptionType)
    {
        //given
        var user = await SignIn(Users.AppOwner);
        var storage = await CreateHardDriveStorage(user, encryptionType);
        var workspace = await CreateWorkspace(storage, user);
        var folder = await CreateFolder(parent: null, workspace, user);

        var originalContent = new byte[4096];
        new Random(308).NextBytes(originalContent);

        var uploadedFile = await UploadFile(
            content: originalContent,
            fileName: "range-full.bin",
            contentType: "application/octet-stream",
            folder: folder,
            workspace: workspace,
            user: user);

        //when
        var result = await DownloadFileRange(
            fileExternalId: uploadedFile.ExternalId,
            rangeStart: 0,
            rangeEnd: 4095,
            workspace: workspace,
            user: user);

        //then
        result.StatusCode.Should().Be(206);
        result.ContentRange.Should().Be("bytes 0-4095/4096");
        result.Content.Should().HaveCount(4096);
        result.Content.Should().BeEquivalentTo(originalContent);
    }
}
