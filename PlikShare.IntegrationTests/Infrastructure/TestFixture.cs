using PlikShare.Auth.Contracts;
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
        EmailTemplates = hostFixture.EmailTemplates;
        
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

        var (_, userCookie) = await Api.Auth.SignUp(
            request: new SignUpUserRequestDto
            {
                Email = invitedUser.Email,
                Password = password,
                InvitationCode = invitedUser.InvitationCode,
                SelectedCheckboxIds = []
            },
            antiforgeryCookies: anonymousAntiforgeryCookies);

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

        var boxLinkCookie = await Api.AccessCodesApi.StartSession(
            anonymousAntiforgeryCookies);

        return new BoxLinkSession(
            Cookie: boxLinkCookie);
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

    protected record AppInvitedUser(
        UserExtId ExternalId,
        string Email,
        string InvitationCode);

    public record BoxLinkSession(
        BoxLinkAuthCookie Cookie);
}