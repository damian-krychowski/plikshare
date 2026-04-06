using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PlikShare.Auth.Contracts;
using PlikShare.Core.Database.AuditLogDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Boxes.Create.Contracts;
using PlikShare.Boxes.CreateLink.Contracts;
using PlikShare.Boxes.Id;
using PlikShare.BoxLinks.Id;
using PlikShare.BoxLinks.UpdatePermissions.Contracts;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Confirm.Contracts;
using PlikShare.EmailProviders.Entities;
using PlikShare.EmailProviders.ExternalProviders.Resend.Create;
using PlikShare.EmailProviders.Id;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure.Apis;
using PlikShare.IntegrationTests.Infrastructure.Mocks;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.HardDrive.Create.Contracts;
using PlikShare.Storages.Id;
using PlikShare.Users.Id;
using PlikShare.Users.Invite.Contracts;
using PlikShare.Workspaces.Create.Contracts;
using PlikShare.Workspaces.Id;
using Serilog;
using Serilog.Events;
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
                Password: "PlikshareIntegrationTestsPassword123!@#")); //same values in appsettings.integrationtests.json

        MainVolume = new AppVolume($"integration_tests_volumes_{hostFixture.MainVolumePathSuffix}/main");
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
            antiforgery: user.Antiforgery);

        return new AppWorkspace(
            ExternalId: result.ExternalId,
            Name: workspaceName);
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
        AppFolder? parent,
        AppWorkspace workspace,
        AppSignedInUser user)
    {
        var folderName = $"folder-{Guid.NewGuid().ToBase62()}";
        
        var folderResponse = await Api.Folders.Create(
            request: new CreateFolderRequestDto
            {
                ExternalId = FolderExtId.NewId(),
                ParentExternalId = parent?.ExternalId,
                Name = folderName
            },
            workspaceExternalId: workspace.ExternalId,
            cookie: user.Cookie,
            antiforgery: user.Antiforgery);

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

    protected record AppFolder(
        FolderExtId ExternalId,
        WorkspaceExtId WorkspaceExternalId,
        string Name);

    protected record AppWorkspace(
        WorkspaceExtId ExternalId,
        string Name);
    
    public record AppUsers(
        User AppOwner);

    public record AppSignedInUser(
        UserExtId ExternalId,
        string Email,
        string Password,
        SessionAuthCookie Cookie,
        AntiforgeryCookies Antiforgery);

    public record AppVolume(string Path);

    public record AppStorage(
        StorageExtId ExternalId,
        string Name,
        string Type,
        string? Details);

    public record AppEmailProvider(
        EmailProviderExtId ExternalId,
        string Name,
        string Type,
        string EmailFrom);

    protected record AppAuthProvider(
        string ExternalId,
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