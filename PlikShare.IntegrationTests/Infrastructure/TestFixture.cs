using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlikShare.Auth.Contracts;
using PlikShare.AuthProviders.Id;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.Files.Id;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.Resend.Create;
using PlikShare.EmailProviders.Id;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.IntegrationTests.Infrastructure.Mocks;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Id;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Users.Id;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Id;
using Serilog;
using Serilog.Events;
using System.Text.Json;
using Xunit.Abstractions;

namespace PlikShare.IntegrationTests.Infrastructure;

public class TestFixture: IAsyncLifetime
{
    public ITestOutputHelper TestOutputHelper { get; }
    public HostFixture HostFixture { get; }

    protected string AppUrl { get; }

    protected Api Api { get; }
    protected AppUsers Users { get; }
    protected AppVolume MainVolume { get; }

    protected ClockMock Clock { get; }
    protected OneTimeCodeMock OneTimeCode { get; }
    protected OneTimeInvitationCodeMock OneTimeInvitationCode { get; }
    protected ResendEmailServer ResendEmailServer { get; }
    protected MockOidcServer MockOidcServer { get; }
    protected AppSettings AppSettings { get; }

    protected EmailTemplates EmailTemplates { get; }
    protected RandomGenerator Random { get; } = new();
    
    protected TestFixture(
        HostFixture hostFixture,
        ITestOutputHelper testOutputHelper)
    {
        HostFixture = hostFixture;
        TestOutputHelper = testOutputHelper;
        AppUrl = HostFixture.AppUrl;
        Clock = hostFixture.Clock;
        OneTimeCode = hostFixture.OneTimeCode;
        OneTimeInvitationCode = hostFixture.OneTimeInvitationCode;
        ResendEmailServer = hostFixture.ResendEmailServer;
        MockOidcServer = hostFixture.MockOidcServer;
        EmailTemplates = hostFixture.EmailTemplates;
        AppSettings = hostFixture.AppSettings;
        
        ConfigureLogger(testOutputHelper);
        
        Api = new Api(
            flurlClient: hostFixture.FlurlClient, 
            appUrl: hostFixture.AppUrl);

        Users = new AppUsers(
            AppOwner: new User(
                Email: "damian@integrationtests.com",
                Password: "PlikshareIntegrationTestsPassword123!@#"),
            SecondAppOwner: new User(
                Email: "second-owner@integrationtests.com",
                Password: "PlikshareIntegrationTestsPassword123!@#")); //same values in appsettings.integrationtests.json

        MainVolume = new AppVolume($"integration_tests_volumes_{hostFixture.MainVolumePathSuffix}/main");

        ClearAuditLog();
    }

    private static void ConfigureLogger(ITestOutputHelper testOutputHelper)
    {
        var configuration = new LoggerConfiguration()
            .WriteTo.TestOutput(testOutputHelper, LogEventLevel.Debug)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Debug);

        Log.Logger = configuration.CreateLogger();
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    protected void ClearAuditLog()
    {
        var auditLogDb = HostFixture.App.Services.GetRequiredService<PlikShareAuditLogDb>();
        using var connection = auditLogDb.OpenConnection();

        connection
            .NonQueryCmd(sql: "DELETE FROM al_audit_logs")
            .Execute();
    }

    protected Task AssertAuditLogContains(
        string expectedEventType,
        string? expectedActorEmail = null,
        string? expectedSeverity = null)
    {
        return AssertAuditLogContainsCore(
            expectedEventType,
            expectedActorEmail,
            expectedSeverity,
            assertDetailsRaw: null);
    }

    protected Task AssertAuditLogContains<TDetails>(
        string expectedEventType,
        Action<TDetails> assertDetails,
        string? expectedActorEmail = null,
        string? expectedSeverity = null)
    {
        return AssertAuditLogContainsCore(
            expectedEventType,
            expectedActorEmail,
            expectedSeverity,
            assertDetailsRaw: detailsJson =>
            {
                var details = JsonSerializer.Deserialize<TDetails>(
                        detailsJson,
                        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                    ?? throw new InvalidOperationException(
                        $"Failed to deserialize audit log details to {typeof(TDetails).Name}: {detailsJson}");

                assertDetails(details);
            });
    }

    private async Task AssertAuditLogContainsCore(
        string expectedEventType,
        string? expectedActorEmail,
        string? expectedSeverity,
        Action<string>? assertDetailsRaw)
    {
        var auditLogDb = HostFixture.App.Services
            .GetRequiredService<PlikShareAuditLogDb>();

        Exception? lastException = null;

        for (var i = 0; i < 30; i++)
        {
            try
            {
                using var connection = auditLogDb.OpenConnection();

                var entries = connection
                    .Cmd(
                        sql: """
                            SELECT al_event_type, al_actor_email, al_event_severity, al_details
                            FROM al_audit_logs
                            WHERE al_event_type = $eventType
                            ORDER BY al_id DESC
                            LIMIT 10
                            """,
                        readRowFunc: reader => new
                        {
                            EventType = reader.GetString(0),
                            ActorEmail = reader.GetStringOrNull(1),
                            Severity = reader.GetString(2),
                            Details = reader.GetStringOrNull(3)
                        })
                    .WithParameter("$eventType", expectedEventType)
                    .Execute();

                var matching = entries.Where(e =>
                    (expectedActorEmail is null || e.ActorEmail == expectedActorEmail) &&
                    (expectedSeverity is null || e.Severity == expectedSeverity))
                    .ToList();

                matching.Should().NotBeEmpty(
                    $"expected audit log entry with event type '{expectedEventType}'" +
                    (expectedActorEmail is not null ? $", actor email '{expectedActorEmail}'" : "") +
                    (expectedSeverity is not null ? $", severity '{expectedSeverity}'" : ""));

                if (assertDetailsRaw is not null)
                {
                    var entry = matching.First();
                    entry.Details.Should().NotBeNullOrEmpty(
                        $"expected audit log entry '{expectedEventType}' to have details JSON");

                    assertDetailsRaw(entry.Details!);
                }

                return;
            }
            catch (Exception e)
            {
                lastException = e;
            }

            await Task.Delay(20);
        }

        throw lastException ?? new InvalidOperationException(
            $"Audit log entry with event type '{expectedEventType}' was not found");
    }

    protected async Task AssertAuditLogDoesNotContain(
        string expectedEventType,
        string? expectedActorEmail = null)
    {
        // Wait a bit to make sure any async audit log writes would have completed
        await Task.Delay(200);

        var auditLogDb = HostFixture.App.Services
            .GetRequiredService<PlikShareAuditLogDb>();

        using var connection = auditLogDb.OpenConnection();

        var entries = connection
            .Cmd(
                sql: """
                    SELECT al_event_type, al_actor_email
                    FROM al_audit_logs
                    WHERE al_event_type = $eventType
                    ORDER BY al_id DESC
                    LIMIT 10
                    """,
                readRowFunc: reader => new
                {
                    EventType = reader.GetString(0),
                    ActorEmail = reader.GetStringOrNull(1)
                })
            .WithParameter("$eventType", expectedEventType)
            .Execute();

        var matching = entries.Where(e =>
            expectedActorEmail is null || e.ActorEmail == expectedActorEmail)
            .ToList();

        matching.Should().BeEmpty(
            $"expected no audit log entry with event type '{expectedEventType}'" +
            (expectedActorEmail is not null ? $", actor email '{expectedActorEmail}'" : "") +
            " but found {0}", matching.Count);
    }

    protected async Task WaitFor(Action assertion)
    {
        Exception? lastException = null;
        
        for (var i = 0; i < 100; i++)
        {
            try
            {
                assertion();
                return;
            }
            catch (Exception e)
            {
                lastException = e;
            }

            await Task.Delay(100);
        }

        throw lastException ?? new InvalidOperationException("Assertion was not met!");
    }
    
    protected async Task<AppSignedInUser> SignIn(User user)
    {
        var anonymousAntiforgeryCookies = await Api
            .Antiforgery
            .GetToken();

        var sessionAuthCookie = await Api
            .Auth
            .SignInOrThrow(user, anonymousAntiforgeryCookies);

        var loggedInAntiforgeryCookies = await Api
            .Antiforgery
            .GetToken(sessionAuthCookie);

        var details = await Api
            .Account
            .GetDetails(sessionAuthCookie);

        return new AppSignedInUser(
            ExternalId: details.ExternalId,
            Email: user.Email,
            Password: user.Password,
            Cookie: sessionAuthCookie,
            Antiforgery: loggedInAntiforgeryCookies);
    }

    protected const string DefaultTestEncryptionPassword = "Test-Encryption-Password-1!";

    /// <summary>
    /// Runs the <c>/api/user-encryption-password/setup</c> flow for the given signed-in user
    /// and returns an updated <see cref="AppSignedInUser"/> carrying the resulting encryption
    /// session cookie, alongside the recovery code. Subsequent full-encryption calls (Full
    /// storage creation, workspace creation on a Full storage, file upload/download on a
    /// full-encrypted workspace) should use the returned user.
    /// </summary>
    protected async Task<(AppSignedInUser Updated, string RecoveryCode)> SetupUserEncryptionPassword(
        AppSignedInUser user,
        string encryptionPassword = DefaultTestEncryptionPassword)
    {
        await HostFixture.ResetUserEncryption();

        var result = await Api.UserEncryptionPassword.Setup(
            userExternalId: user.ExternalId,
            encryptionPassword: encryptionPassword,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return (user with { EncryptionCookie = result.EncryptionCookie }, result.RecoveryCode);
    }

    protected async Task<AppStorage> CreateHardDriveStorage(
        AppSignedInUser user)
    {
        var hardDriveName = $"hard-drive-{Guid.NewGuid().ToBase62()}"; 
        
        var hardDriveStorageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: hardDriveName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{hardDriveName}",
                EncryptionType: StorageEncryptionType.None),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppStorage(
            ExternalId: hardDriveStorageResponse.ExternalId,
            Name: hardDriveName,
            Type: StorageType.HardDrive,
            Details: $"{MainVolume.Path}/{hardDriveName}");
    }

    protected async Task<AppStorage> CreateHardDriveStorage(
        AppSignedInUser user,
        StorageEncryptionType encryptionType)
    {
        Cookie? encryptionCookie = user.EncryptionCookie;

        if (encryptionType == StorageEncryptionType.Full && encryptionCookie is null)
        {
            var (updated, _) = await SetupUserEncryptionPassword(user);
            encryptionCookie = updated.EncryptionCookie;
        }

        var hardDriveName = $"hard-drive-{Guid.NewGuid().ToBase62()}";

        var hardDriveStorageResponse = await Api.Storages.CreateHardDriveStorage(
            request: new CreateHardDriveStorageRequestDto(
                Name: hardDriveName,
                VolumePath: MainVolume.Path,
                FolderPath: $"/{hardDriveName}",
                EncryptionType: encryptionType),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppStorage(
            ExternalId: hardDriveStorageResponse.ExternalId,
            Name: hardDriveName,
            Type: StorageType.HardDrive,
            Details: $"{MainVolume.Path}/{hardDriveName}",
            WorkspaceEncryptionSession: encryptionType == StorageEncryptionType.Full
                ? encryptionCookie
                : null);
    }

    protected async Task WaitForBucketReady(
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        for (var i = 0; i < 100; i++)
        {
            var status = await Api.Workspaces.CheckBucketStatus(
                externalId: workspace.ExternalId,
                cookie: user.Cookie);

            if (status.IsBucketCreated)
                return;

            await Task.Delay(20);
        }

        throw new InvalidOperationException(
            $"Workspace '{workspace.ExternalId}' bucket was not created in time");
    }

    protected async Task<AppFile> UploadFile(
        byte[] content,
        string fileName,
        string contentType,
        AppFolder folder,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        await WaitForBucketReady(workspace, user);

        var fileUploadExternalId = FileUploadExtId.NewId();

        var initiateResponse = await Api.Uploads.BulkInitiate(
            workspaceExternalId: workspace.ExternalId,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items =
                [
                    new BulkInitiateFileUploadItemDto
                    {
                        FileUploadExternalId = fileUploadExternalId.Value,
                        FolderExternalId = folder.ExternalId.Value,
                        FileNameWithExtension = fileName,
                        FileContentType = contentType,
                        FileSizeInBytes = content.Length
                    }
                ]
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        if (initiateResponse.DirectUploads is not null)
        {
            var uploadResults = await Api.PreSignedFiles.MultiFileDirectUpload(
                preSignedUrl: initiateResponse.DirectUploads.PreSignedMultiFileDirectUploadLink,
                files: new Dictionary<FileUploadExtId, byte[]>
                {
                    [fileUploadExternalId] = content
                },
                cookie: user.Cookie);

            return new AppFile(
                ExternalId: uploadResults[0].FileExternalId,
                WorkspaceExternalId: workspace.ExternalId,
                Name: fileName);
        }

        if (initiateResponse.SingleChunkUploads is { Count: > 0 })
        {
            var singleChunk = initiateResponse.SingleChunkUploads[0];
            var uploadExtId = FileUploadExtId.Parse(singleChunk.FileUploadExternalId);

            var eTag = await Api.PreSignedFiles.UploadFilePart(
                preSignedUrl: singleChunk.PreSignedUploadLink,
                content: content,
                contentType: contentType,
                cookie: user.Cookie);

            await Api.Uploads.CompletePartUpload(
                workspaceExternalId: workspace.ExternalId,
                fileUploadExternalId: uploadExtId,
                partNumber: 1,
                request: new Uploads.FilePartUpload.Complete.Contracts.CompleteFilePartUploadRequestDto(
                    ETag: eTag),
                cookie: user.Cookie,
                antiforgery: user.Antiforgery,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            var completeResult = await Api.Uploads.CompleteUpload(
                workspaceExternalId: workspace.ExternalId,
                fileUploadExternalId: uploadExtId,
                cookie: user.Cookie,
                antiforgery: user.Antiforgery,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            return new AppFile(
                ExternalId: completeResult.FileExternalId,
                WorkspaceExternalId: workspace.ExternalId,
                Name: fileName);
        }

        if (initiateResponse.MultiStepChunkUploads is { Count: > 0 })
        {
            var multiStep = initiateResponse.MultiStepChunkUploads[0];
            var uploadExtId = FileUploadExtId.Parse(multiStep.FileUploadExternalId);

            for (var partNumber = 1; partNumber <= multiStep.ExpectedPartsCount; partNumber++)
            {
                var partInitiate = await Api.Uploads.InitiatePartUpload(
                    workspaceExternalId: workspace.ExternalId,
                    fileUploadExternalId: uploadExtId,
                    partNumber: partNumber,
                    cookie: user.Cookie,
                    antiforgery: user.Antiforgery,
                    workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

                var partContent = content
                    .AsSpan()
                    .Slice(
                        (int)partInitiate.StartsAtByte,
                        (int)(partInitiate.EndsAtByte - partInitiate.StartsAtByte + 1))
                    .ToArray();

                var eTag = await Api.PreSignedFiles.UploadFilePart(
                    preSignedUrl: partInitiate.UploadPreSignedUrl,
                    content: partContent,
                    contentType: contentType,
                    cookie: user.Cookie);

                if (partInitiate.IsCompleteFilePartUploadCallbackRequired)
                {
                    await Api.Uploads.CompletePartUpload(
                        workspaceExternalId: workspace.ExternalId,
                        fileUploadExternalId: uploadExtId,
                        partNumber: partNumber,
                        request: new Uploads.FilePartUpload.Complete.Contracts.CompleteFilePartUploadRequestDto(
                            ETag: eTag),
                        cookie: user.Cookie,
                        antiforgery: user.Antiforgery,
                        workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);
                }
            }

            var completeResult = await Api.Uploads.CompleteUpload(
                workspaceExternalId: workspace.ExternalId,
                fileUploadExternalId: uploadExtId,
                cookie: user.Cookie,
                antiforgery: user.Antiforgery,
                workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

            return new AppFile(
                ExternalId: completeResult.FileExternalId,
                WorkspaceExternalId: workspace.ExternalId,
                Name: fileName);
        }

        throw new InvalidOperationException(
            "BulkInitiate returned no upload instructions");
    }

    protected async Task<List<AppFile>> UploadFiles(
        List<(byte[] Content, string FileName, string ContentType)> files,
        AppFolder folder,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        await WaitForBucketReady(workspace, user);

        var uploadItems = files.Select(f => new
        {
            UploadExternalId = FileUploadExtId.NewId(),
            f.Content,
            f.FileName,
            f.ContentType
        }).ToList();

        var initiateResponse = await Api.Uploads.BulkInitiate(
            workspaceExternalId: workspace.ExternalId,
            request: new BulkInitiateFileUploadRequestDto
            {
                Items = uploadItems.Select(item => new BulkInitiateFileUploadItemDto
                {
                    FileUploadExternalId = item.UploadExternalId.Value,
                    FolderExternalId = folder.ExternalId.Value,
                    FileNameWithExtension = item.FileName,
                    FileContentType = item.ContentType,
                    FileSizeInBytes = item.Content.Length
                }).ToArray()
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        if (initiateResponse.DirectUploads is null)
            throw new InvalidOperationException(
                "Expected DirectUploads for multi-file upload but got a different algorithm. " +
                "Ensure all files are small enough for DirectUpload.");

        var filesToUpload = uploadItems.ToDictionary(
            item => item.UploadExternalId,
            item => item.Content);

        var uploadResults = await Api.PreSignedFiles.MultiFileDirectUpload(
            preSignedUrl: initiateResponse.DirectUploads.PreSignedMultiFileDirectUploadLink,
            files: filesToUpload,
            cookie: user.Cookie);

        return uploadResults.Select((result, index) => new AppFile(
            ExternalId: result.FileExternalId,
            WorkspaceExternalId: workspace.ExternalId,
            Name: uploadItems[index].FileName)).ToList();
    }

    protected async Task<byte[]> DownloadFile(
        FileExtId fileExternalId,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        // Multi-step uploads complete asynchronously via a queue job,
        // so we may need to retry until the file is ready on storage.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var downloadLinkResponse = await Api.Files.GetDownloadLink(
                    workspaceExternalId: workspace.ExternalId,
                    fileExternalId: fileExternalId,
                    contentDisposition: "attachment",
                    cookie: user.Cookie,
                    workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

                return await Api.PreSignedFiles.DownloadFile(
                    preSignedUrl: downloadLinkResponse.DownloadPreSignedUrl,
                    cookie: user.Cookie);
            }
            catch (TestApiCallException)
            {
                if (attempt == 99)
                    throw;

                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    protected async Task<PreSignedFilesApi.RangeDownloadResult> DownloadFileRange(
        FileExtId fileExternalId,
        long rangeStart,
        long rangeEnd,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            try
            {
                var downloadLinkResponse = await Api.Files.GetDownloadLink(
                    workspaceExternalId: workspace.ExternalId,
                    fileExternalId: fileExternalId,
                    contentDisposition: "inline",
                    cookie: user.Cookie,
                    workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

                return await Api.PreSignedFiles.DownloadFileRange(
                    preSignedUrl: downloadLinkResponse.DownloadPreSignedUrl,
                    rangeStart: rangeStart,
                    rangeEnd: rangeEnd,
                    cookie: user.Cookie);
            }
            catch (TestApiCallException)
            {
                if (attempt == 99)
                    throw;

                await Task.Delay(100);
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    protected async Task<AppWorkspace> CreateWorkspace(
        AppStorage storage,
        AppSignedInUser user)
    {
        var workspaceName = $"workspace-{Guid.NewGuid().ToBase62()}";

        var result = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: storage.ExternalId,
                Name: workspaceName),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            userEncryptionSession: storage.WorkspaceEncryptionSession);

        return new AppWorkspace(
            ExternalId: result.ExternalId,
            Name: workspaceName,
            WorkspaceEncryptionSession: storage.WorkspaceEncryptionSession);
    }
    
    protected async Task<AppWorkspace> CreateWorkspace(
        AppSignedInUser user)
    {
        var storage = await CreateHardDriveStorage(
            user);
        
        var workspaceName = $"workspace-{Guid.NewGuid().ToBase62()}";

        var result = await Api.Workspaces.Create(
            request: new CreateWorkspaceRequestDto(
                StorageExternalId: storage.ExternalId,
                Name: workspaceName),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppWorkspace(
            ExternalId: result.ExternalId,
            Name: workspaceName);
    }

    protected async Task<AppFolder> CreateFolder(
        string name,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        return await CreateFolder(
            parent: null,
            name: name,
            workspace: workspace,
            user: user);
    }

    protected Task<AppFolder> CreateFolder(
        AppFolder? parent,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        return CreateFolder(
            parent: parent,
            name: $"folder-{Guid.NewGuid().ToBase62()}",
            workspace: workspace,
            user: user);
    }

    private async Task<AppFolder> CreateFolder(
        AppFolder? parent,
        string name,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        var folderName = name;

        var folderResponse = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = parent?.ExternalId,
                Name = folderName
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery,
            workspaceEncryptionSession: workspace.WorkspaceEncryptionSession);

        return new AppFolder(
            ExternalId: folderResponse.ExternalId,
            WorkspaceExternalId: workspace.ExternalId,
            Name: folderName);
    }
    
    protected async Task<AppFolder> CreateFolder(
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        return await CreateFolder(
            parent: null,
            workspace: workspace,
            user: user);
    }
    
    protected async Task<AppFolder> CreateFolder(
        AppSignedInUser user)
    {
        var workspace = await CreateWorkspace(
            user);

        return await CreateFolder(
            parent: null,
            workspace: workspace,
            user: user);
    }

    protected async Task<AppBox> CreateBox(
        AppFolder folder,
        AppSignedInUser user)
    {
        var boxName = $"box-{Guid.NewGuid().ToBase62()}";
        
        var box = await Api.Boxes.Create(
            workspaceExternalId: folder.WorkspaceExternalId,
            request: new CreateBoxRequestDto(
                Name: boxName,
                FolderExternalId: folder.ExternalId),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppBox(
            ExternalId: box.ExternalId,
            FolderExternalId: folder.ExternalId,
            WorkspaceExternalId: folder.WorkspaceExternalId,
            Name: boxName);
    }
    
    protected async Task<AppBox> CreateBox(
        AppSignedInUser user)
    {
        var folder = await CreateFolder(
            user);

        return await CreateBox(
            folder: folder,
            user: user);
    }
    
    protected async Task<AppBoxLink> CreateBoxLink(
        AppBox box,
        AppSignedInUser user,
        AppBoxLinkPermissions? permissions = null)
    {
        var boxLinkName = $"box-link-{Guid.NewGuid().ToBase62()}";
        
        var boxLink = await Api.Boxes.CreateBoxLink(
            workspaceExternalId: box.WorkspaceExternalId,
            boxExternalId: box.ExternalId,
            request: new CreateBoxLinkRequestDto(
                Name: boxLinkName),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        if(permissions is not null)
        {
            await Api.BoxLinks.UpdatePermissions(
                workspaceExternalId: box.WorkspaceExternalId,
                boxLinkExternalId: boxLink.ExternalId,
                request: new UpdateBoxLinkPermissionsRequestDto(
                    AllowDownload: permissions.AllowDownload,
                    AllowUpload: permissions.AllowUpload,
                    AllowList: permissions.AllowList,
                    AllowDeleteFile: permissions.AllowDeleteFile,
                    AllowRenameFile: permissions.AllowRenameFile,
                    AllowMoveItems: permissions.AllowMoveItems,
                    AllowCreateFolder: permissions.AllowCreateFolder,
                    AllowRenameFolder: permissions.AllowRenameFolder,
                    AllowDeleteFolder: permissions.AllowDeleteFolder),
                cookie: user.Cookie,
                antiforgery: user.Antiforgery);
        }
        
        return new AppBoxLink(
            ExternalId: boxLink.ExternalId,
            BoxExternalId: box.ExternalId,
            WorkspaceExternalId: box.WorkspaceExternalId,
            Name: boxLinkName,
            AccessCode: boxLink.AccessCode);
    }
    
    protected async Task<AppBoxLink> CreateBoxLink(
        AppSignedInUser user,
        AppBoxLinkPermissions? permissions = null)
    {
        var box = await CreateBox(user);

        return await CreateBoxLink(
            box: box,
            user: user,
            permissions: permissions);
    }

    protected async Task<AppEmailProvider> CreateAndActivateEmailProviderIfMissing(
        AppSignedInUser user)
    {
        var emailProviders = await Api.EmailProviders.Get(
            cookie: user.Cookie);

        var activeEmailProvider = emailProviders
            .Items
            .FirstOrDefault(x => x.IsActive);

        if (activeEmailProvider is not null)
            return new AppEmailProvider(
                ExternalId: activeEmailProvider.ExternalId,
                Name: activeEmailProvider.Name,
                Type: activeEmailProvider.Type,
                EmailFrom: activeEmailProvider.EmailFrom);

        var emailProviderName = $"Resend-{Guid.NewGuid().ToBase62()}";
        var confirmationCode = Guid.NewGuid().ToBase62();
        var emailFrom = "PlikShare <damian@plikshare.com>";
        OneTimeCode.NextCodeToGenerate(confirmationCode);

        var provider = await Api.EmailProviders.CreateResend(
            request: new CreateResendEmailProviderRequestDto(
                Name: emailProviderName,
                EmailFrom: emailFrom,
                ApiKey: Guid.NewGuid().ToBase62()),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Confirm(
            emailProviderExternalId: provider.ExternalId,
            request: new ConfirmEmailProviderRequestDto(
                ConfirmationCode: confirmationCode),
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.EmailProviders.Activate(
            emailProviderExternalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppEmailProvider(
            ExternalId: provider.ExternalId,
            Name: emailProviderName,
            Type: EmailProviderType.Resend.Value,
            EmailFrom: emailFrom);
    }

    protected async Task<AppInvitedUser> InviteUser(
        AppSignedInUser user)
    {
        var email = Random.Email();
        var invitationCode = Random.InvitationCode();

        OneTimeInvitationCode.AddCode(invitationCode);

        var invitationResponse = await Api.Users.InviteUsers(
            request: new InviteUsersRequestDto
            {
                Emails = [email]
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppInvitedUser(
            ExternalId: invitationResponse.Users[0].ExternalId,
            Email: email,
            InvitationCode: invitationCode);
    }

    protected async Task<AppSignedInUser> InviteAndRegisterUser(
        AppSignedInUser user)
    {
        var invitedUser = await InviteUser(user);
        var password = Random.Password();

        var anonymousAntiforgeryCookies = await Api
            .Antiforgery
            .GetToken();

        var (signUpResponse, userCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = password,
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

        signUpResponse.Should().BeEquivalentTo(SignUpUserResponseDto.SingedUpAndSignedIn);

        var loggedInAntiforgeryCookies = await Api
            .Antiforgery
            .GetToken(userCookie);

        return new AppSignedInUser(
            ExternalId: invitedUser.ExternalId,
            Email: invitedUser.Email,
            Password: password,
            Cookie: userCookie!,
            Antiforgery: loggedInAntiforgeryCookies);
    }

    protected async Task<BoxLinkSession> StartBoxLinkSession()
    {
        var anonymousAntiforgeryCookies = await Api
           .Antiforgery
           .GetToken();

        var boxLinkToken = await Api.AccessCodesApi.StartSession(
            anonymousAntiforgeryCookies);

        return new BoxLinkSession(
            Token: boxLinkToken);
    }

    protected byte[]? GetStoredInvitationCodeHash(string email)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT u_invitation_code_hash
                     FROM u_users
                     WHERE u_normalized_email = $normalizedEmail
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.IsDBNull(0) ? null : reader.GetFieldValue<byte[]>(0))
            .WithParameter("$normalizedEmail", PlikShare.Users.Entities.Email.Normalize(email))
            .Execute();

        return rows.Count == 0 ? null : rows[0];
    }

    protected bool HasWorkspaceEncryptionKey(WorkspaceExtId workspaceExternalId, string userEmail)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT 1
                     FROM wek_workspace_encryption_keys wek
                     JOIN w_workspaces w ON w.w_id = wek.wek_workspace_id
                     JOIN u_users u ON u.u_id = wek.wek_user_id
                     WHERE w.w_external_id = $workspaceExternalId
                       AND u.u_normalized_email = $normalizedEmail
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .WithParameter("$normalizedEmail", PlikShare.Users.Entities.Email.Normalize(userEmail))
            .Execute();

        return rows.Count > 0;
    }

    protected int CountEphemeralWorkspaceEncryptionKeys(WorkspaceExtId workspaceExternalId, string userEmail)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT 1
                     FROM ewek_ephemeral_workspace_encryption_keys ewek
                     JOIN w_workspaces w ON w.w_id = ewek.ewek_workspace_id
                     JOIN u_users u ON u.u_id = ewek.ewek_user_id
                     WHERE w.w_external_id = $workspaceExternalId
                       AND u.u_normalized_email = $normalizedEmail
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .WithParameter("$normalizedEmail", PlikShare.Users.Entities.Email.Normalize(userEmail))
            .Execute()
            .Count;
    }

    protected bool HasEphemeralWorkspaceEncryptionKey(WorkspaceExtId workspaceExternalId, string userEmail)
        => CountEphemeralWorkspaceEncryptionKeys(workspaceExternalId, userEmail) > 0;

    protected DateTimeOffset? GetEphemeralWorkspaceEncryptionKeyExpiresAt(
        WorkspaceExtId workspaceExternalId,
        string userEmail)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT ewek.ewek_expires_at
                     FROM ewek_ephemeral_workspace_encryption_keys ewek
                     JOIN w_workspaces w ON w.w_id = ewek.ewek_workspace_id
                     JOIN u_users u ON u.u_id = ewek.ewek_user_id
                     WHERE w.w_external_id = $workspaceExternalId
                       AND u.u_normalized_email = $normalizedEmail
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetFieldValue<DateTimeOffset>(0))
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .WithParameter("$normalizedEmail", PlikShare.Users.Entities.Email.Normalize(userEmail))
            .Execute();

        return rows.Count == 0 ? null : rows[0];
    }

    protected int DeleteEphemeralWorkspaceEncryptionKeys(
        WorkspaceExtId workspaceExternalId,
        string userEmail)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .NonQueryCmd(
                sql: """
                     DELETE FROM ewek_ephemeral_workspace_encryption_keys
                     WHERE ewek_workspace_id IN (
                             SELECT w_id FROM w_workspaces WHERE w_external_id = $workspaceExternalId
                         )
                       AND ewek_user_id IN (
                             SELECT u_id FROM u_users WHERE u_normalized_email = $normalizedEmail
                         )
                     """)
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .WithParameter("$normalizedEmail", PlikShare.Users.Entities.Email.Normalize(userEmail))
            .Execute()
            .AffectedRows;
    }

    protected string BuildEphemeralCleanupDebounceId(
        WorkspaceExtId workspaceExternalId,
        string userEmail)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT w.w_id, u.u_id
                     FROM w_workspaces w
                     CROSS JOIN u_users u
                     WHERE w.w_external_id = $workspaceExternalId
                       AND u.u_normalized_email = $normalizedEmail
                     LIMIT 1
                     """,
                readRowFunc: reader => (WorkspaceId: reader.GetInt32(0), UserId: reader.GetInt32(1)))
            .WithParameter("$workspaceExternalId", workspaceExternalId.Value)
            .WithParameter("$normalizedEmail", PlikShare.Users.Entities.Email.Normalize(userEmail))
            .Execute();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                $"Could not resolve internal ids for workspace '{workspaceExternalId}' and user '{userEmail}'.");

        return $"ewek-cleanup-{rows[0].WorkspaceId}-{rows[0].UserId}";
    }

    protected (int Count, DateTimeOffset? ExecuteAfter) GetCleanupJobInfo(string debounceId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        var rows = connection
            .Cmd(
                sql: """
                     SELECT q_execute_after_date
                     FROM q_queue
                     WHERE q_debounce_id = $debounceId
                     """,
                readRowFunc: reader => reader.GetFieldValue<DateTimeOffset>(0))
            .WithParameter("$debounceId", debounceId)
            .Execute();

        return (rows.Count, rows.Count == 0 ? null : rows[0]);
    }

    protected List<string> GetStorageEncryptionKeyOwnerEmails(StorageExtId storageExternalId)
    {
        using var connection = HostFixture.Db.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT u.u_email
                     FROM sek_storage_encryption_keys sek
                     JOIN s_storages s ON s.s_id = sek.sek_storage_id
                     JOIN u_users u ON u.u_id = sek.sek_user_id
                     WHERE s.s_external_id = $storageExternalId
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$storageExternalId", storageExternalId.Value)
            .Execute();
    }

    protected record AppBoxLinkPermissions(
        bool AllowDownload = false,
        bool AllowUpload = false,
        bool AllowList = false,
        bool AllowDeleteFile = false,
        bool AllowRenameFile = false,
        bool AllowMoveItems = false,
        bool AllowCreateFolder = false,
        bool AllowRenameFolder = false,
        bool AllowDeleteFolder = false);
    
    protected record AppBoxLink(
        BoxLinkExtId ExternalId,
        BoxExtId BoxExternalId,
        WorkspaceExtId WorkspaceExternalId,
        string Name,
        string AccessCode);
    
    protected record AppBox(
        BoxExtId ExternalId,
        FolderExtId FolderExternalId,
        WorkspaceExtId WorkspaceExternalId,
        string Name);

    protected record AppFile(
        FileExtId ExternalId,
        WorkspaceExtId WorkspaceExternalId,
        string Name);

    protected record AppFolder(
        FolderExtId ExternalId,
        WorkspaceExtId WorkspaceExternalId,
        string Name);

    protected record AppWorkspace(
        WorkspaceExtId ExternalId,
        string Name,
        Cookie? WorkspaceEncryptionSession = null);
    
    public record AppUsers(
        User AppOwner,
        User SecondAppOwner);

    public record AppSignedInUser(
        UserExtId ExternalId,
        string Email,
        string Password,
        SessionAuthCookie Cookie,
        AntiforgeryCookies Antiforgery,
        Cookie? EncryptionCookie = null);

    public record AppVolume(string Path);

    public record AppStorage(
        StorageExtId ExternalId,
        string Name,
        string Type,
        string? Details,
        Cookie? WorkspaceEncryptionSession = null);

    public record AppEmailProvider(
        EmailProviderExtId ExternalId,
        string Name,
        string Type,
        string EmailFrom);

    protected record AppAuthProvider(
        AuthProviderExtId ExternalId,
        string Name);

    protected async Task<AppAuthProvider> CreateAndActivateAuthProvider(
        AppSignedInUser user)
    {
        var providerName = Random.Name("OidcProvider");
        var clientId = Random.ClientId();
        var clientSecret = Random.ClientSecret();

        var provider = await Api.AuthProviders.CreateOidc(
            request: new AuthProviders.Create.Contracts.CreateOidcAuthProviderRequestDto
            {
                Name = providerName,
                ClientId = clientId,
                ClientSecret = clientSecret,
                IssuerUrl = MockOidcServer.IssuerUrl
            },
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        await Api.AuthProviders.Activate(
            externalId: provider.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

        return new AppAuthProvider(
            ExternalId: provider.ExternalId,
            Name: providerName);
    }

    protected async Task<AppSignedInUser> SignInViaSso(
        AppAuthProvider authProvider,
        string email,
        string sub)
    {
        var authCode = Random.AuthCode();

        var initiateResult = await Api.Sso.Initiate(
            authProvider.ExternalId);

        var state = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "state");
        var nonce = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "nonce");
        var clientId = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "client_id");
        var codeChallenge = UrlHelper.ExtractQueryParam(initiateResult.LocationHeader!, "code_challenge");

        MockOidcServer.RegisterAuthCode(
            authCode,
            email,
            sub,
            nonce!,
            clientId!,
            codeChallenge!);

        var callbackResult = await Api.Sso.Callback(
            code: authCode,
            state: state!);

        var sessionAuthCookie = callbackResult.SessionAuthCookie
            ?? throw new InvalidOperationException(
                "SSO callback did not return a session cookie. " +
                $"Location: {callbackResult.LocationHeader}");

        var loggedInAntiforgeryCookies = await Api
            .Antiforgery
            .GetToken(sessionAuthCookie);

        var details = await Api
            .Account
            .GetDetails(sessionAuthCookie);

        return new AppSignedInUser(
            ExternalId: details.ExternalId,
            Email: email,
            Password: string.Empty,
            Cookie: sessionAuthCookie,
            Antiforgery: loggedInAntiforgeryCookies);
    }

    protected record AppInvitedUser(
        UserExtId ExternalId,
        string Email,
        string InvitationCode);

    public record BoxLinkSession(
        BoxLinkToken Token);
}