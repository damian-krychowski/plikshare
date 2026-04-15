using Flurl.Http;

namespace PlikShare.IntegrationTests.Infrastructure.Apis;

public class Api(IFlurlClient flurlClient, string appUrl)
{
    public DashboardApi Dashboard { get; } = new(flurlClient, appUrl);
    public AuthApi Auth { get; } = new(flurlClient, appUrl);
    public AntiforgeryApi Antiforgery { get; } = new(flurlClient, appUrl);
    public EntryPageApi EntryPage { get; } = new(flurlClient, appUrl);
    public AccountApi Account { get; } = new(flurlClient, appUrl);
    public GeneralSettingsApi GeneralSettings { get; } = new(flurlClient, appUrl);
    public StoragesApi Storages { get; } = new(flurlClient, appUrl);
    public EmailProvidersApi EmailProviders { get; } = new(flurlClient, appUrl);
    public UsersApi Users { get; } = new(flurlClient, appUrl);
    public UserEncryptionPasswordApi UserEncryptionPassword { get; } = new(flurlClient, appUrl);
    public WorkspacesApi Workspaces { get; } = new(flurlClient, appUrl);
    public WorkspacesAdminApi WorkspacesAdmin { get; } = new(flurlClient, appUrl);
    public FoldersApi Folders { get; } = new(flurlClient, appUrl);
    public BoxesApi Boxes { get; } = new(flurlClient, appUrl);
    public BoxLinksApi BoxLinks { get; } = new(flurlClient, appUrl);

    public AuthProvidersApi AuthProviders { get; } = new(flurlClient, appUrl);
    public SsoApi Sso { get; } = new(flurlClient, appUrl);

    public BoxExternalAccessApi BoxExternalAccess = new(flurlClient, appUrl);

    public AccessCodesApi AccessCodesApi = new(flurlClient, appUrl);

    public IntegrationsApi Integrations { get; } = new(flurlClient, appUrl);

    public AuditLogApi AuditLog { get; } = new(flurlClient, appUrl);

    public UploadsApi Uploads { get; } = new(flurlClient, appUrl);
    public FilesApi Files { get; } = new(flurlClient, appUrl);
    public PreSignedFilesApi PreSignedFiles { get; } = new(flurlClient);
}