using System.IO.Compression;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Account;
using PlikShare.Account.GetKnownUsers;
using PlikShare.Antiforgery;
using PlikShare.AuditLog;
using PlikShare.AuditLog.Decryption;
using PlikShare.AuditLog.Policy;
using PlikShare.AuditLog.Policy.Queries;
using PlikShare.AuditLog.Queries;
using PlikShare.ArtificialIntelligence;
using PlikShare.ArtificialIntelligence.CheckConversationStatus;
using PlikShare.ArtificialIntelligence.DeleteConversation;
using PlikShare.ArtificialIntelligence.DeleteConversation.QueueJob;
using PlikShare.ArtificialIntelligence.GetFileArtifact;
using PlikShare.ArtificialIntelligence.GetMessages;
using PlikShare.ArtificialIntelligence.SendFileMessage;
using PlikShare.ArtificialIntelligence.SendFileMessage.QueueJob;
using PlikShare.ArtificialIntelligence.UpdateConversationName;
using PlikShare.Auth;
using PlikShare.Auth.CheckInvitation;
using PlikShare.Boxes;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Create;
using PlikShare.Boxes.CreateLink;
using PlikShare.Boxes.Delete;
using PlikShare.Boxes.Delete.QueueJob;
using PlikShare.Boxes.Get;
using PlikShare.Boxes.List;
using PlikShare.Boxes.Members.CreateInvitation;
using PlikShare.Boxes.Members.Revoke;
using PlikShare.Boxes.Members.UpdatePermissions;
using PlikShare.Boxes.UpdateDefaultDisplayConfiguration;
using PlikShare.Boxes.UpdateFolder;
using PlikShare.Boxes.UpdateFooter;
using PlikShare.Boxes.UpdateFooterIsEnabled;
using PlikShare.Boxes.UpdateHeader;
using PlikShare.Boxes.UpdateHeaderIsEnabled;
using PlikShare.Boxes.UpdateIsEnabled;
using PlikShare.Boxes.UpdateName;
using PlikShare.BoxExternalAccess;
using PlikShare.BoxExternalAccess.Handler;
using PlikShare.BoxExternalAccess.Handler.GetContent;
using PlikShare.BoxExternalAccess.Handler.GetHtml;
using PlikShare.BoxExternalAccess.Invitations.Accept;
using PlikShare.BoxExternalAccess.Invitations.Reject;
using PlikShare.BoxExternalAccess.LeaveBox;
using PlikShare.BoxLinks;
using PlikShare.BoxLinks.Cache;
using PlikShare.BoxLinks.Delete;
using PlikShare.BoxLinks.RegenerateAccessCode;
using PlikShare.BoxLinks.UpdateIsEnabled;
using PlikShare.BoxLinks.UpdateName;
using PlikShare.BoxLinks.UpdatePermissions;
using PlikShare.BoxLinks.UpdateWidgetOrigins;
using PlikShare.BulkDelete;
using PlikShare.BulkDownload;
using PlikShare.Core.Angular;
using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.CorrelationId;
using PlikShare.Core.CORS;
using PlikShare.Core.Database.AuditLogDatabase.Migrations;
using PlikShare.Core.Database.MainDatabase.Migrations;
using PlikShare.Core.Emails;
using PlikShare.Core.Emails.Alerts;
using PlikShare.Core.Emails.Templates;
using PlikShare.Core.Encryption;
using PlikShare.Core.ExceptionHandlers;
using PlikShare.Core.IdentityProvider;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Core.Volumes;
using PlikShare.Dashboard;
using PlikShare.Dashboard.Content;
using PlikShare.Auth.Sso;
using PlikShare.AuthProviders;
using PlikShare.AuthProviders.Activate;
using PlikShare.AuthProviders.Create;
using PlikShare.AuthProviders.Deactivate;
using PlikShare.AuthProviders.Delete;
using PlikShare.AuthProviders.GetDetails;
using PlikShare.AuthProviders.List;
using PlikShare.AuthProviders.PasswordLogin;
using PlikShare.AuthProviders.TestConfiguration;
using PlikShare.AuthProviders.Update;
using PlikShare.AuthProviders.UpdateName;
using PlikShare.EmailProviders;
using PlikShare.EmailProviders.Activate;
using PlikShare.EmailProviders.Confirm;
using PlikShare.EmailProviders.Create;
using PlikShare.EmailProviders.Deactivate;
using PlikShare.EmailProviders.Delete;
using PlikShare.EmailProviders.EmailSender;
using PlikShare.EmailProviders.ExternalProviders.Resend;
using PlikShare.EmailProviders.ExternalProviders.Smtp;
using PlikShare.EmailProviders.List;
using PlikShare.EmailProviders.ResendConfirmationEmail;
using PlikShare.EmailProviders.SendConfirmationEmail;
using PlikShare.EmailProviders.UpdateName;
using PlikShare.EntryPage;
using PlikShare.Files;
using PlikShare.Files.BulkDelete.QueueJob;
using PlikShare.Files.BulkDownload;
using PlikShare.Files.Created;
using PlikShare.Files.Delete;
using PlikShare.Files.Delete.QueueJob;
using PlikShare.Files.Download;
using PlikShare.Files.Get;
using PlikShare.Files.PreSignedLinks;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Files.Processing;
using PlikShare.Files.Preview.Comment.CreateComment;
using PlikShare.Files.Preview.Comment.DeleteComment;
using PlikShare.Files.Preview.Comment.EditComment;
using PlikShare.Files.Preview.GetDetails;
using PlikShare.Files.Preview.GetZipBulkDownloadLink;
using PlikShare.Files.Preview.GetZipContentDownloadLink;
using PlikShare.Files.Preview.GetZipDetails;
using PlikShare.Files.Preview.SaveNote;
using PlikShare.Files.Rename;
using PlikShare.Files.Rename.Contracts;
using PlikShare.Files.UpdateSize;
using PlikShare.MediaProcessing;
using PlikShare.MediaProcessing.Dimensions;
using PlikShare.MediaProcessing.Generation;
using PlikShare.Files.UploadAttachment;
using PlikShare.Folders;
using PlikShare.Folders.Create;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Delete.QueueJob;
using PlikShare.Folders.List;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Folders.Rename;
using PlikShare.Folders.Rename.Contracts;
using PlikShare.Folders.UpdatePositions;
using PlikShare.GeneralSettings;
using PlikShare.GeneralSettings.GetStatus;
using PlikShare.GeneralSettings.LegalFiles;
using PlikShare.GeneralSettings.LegalFiles.DeleteLegalFile;
using PlikShare.GeneralSettings.LegalFiles.UploadLegalFile;
using PlikShare.GeneralSettings.SignUpCheckboxes.CreateOrUpdate;
using PlikShare.GeneralSettings.SignUpCheckboxes.Delete;
using PlikShare.HealthCheck;
using PlikShare.Integrations;
using PlikShare.Integrations.Activate;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Aws.Textract.Jobs;
using PlikShare.Integrations.Aws.Textract.Jobs.CheckStatus;
using PlikShare.Integrations.Aws.Textract.Jobs.CheckTextractAnalysisStatus;
using PlikShare.Integrations.Aws.Textract.Jobs.Delete;
using PlikShare.Integrations.Aws.Textract.Jobs.DownloadTextractAnalysis;
using PlikShare.Integrations.Aws.Textract.Jobs.InitiateTextractAnalysis;
using PlikShare.Integrations.Aws.Textract.Jobs.StartJob;
using PlikShare.Integrations.Aws.Textract.Jobs.UpdateJobTextractFileAndStatus;
using PlikShare.Integrations.Aws.Textract.Register;
using PlikShare.Integrations.Aws.Textract.TestConfiguration;
using PlikShare.Integrations.Create;
using PlikShare.Integrations.Deactivate;
using PlikShare.Integrations.Delete;
using PlikShare.Integrations.List;
using PlikShare.Integrations.OpenAi.ChatGpt;
using PlikShare.Integrations.OpenAi.ChatGpt.Register;
using PlikShare.Integrations.OpenAi.ChatGpt.TestConfiguration;
using PlikShare.Integrations.UpdateName;
using PlikShare.Locks;
using PlikShare.Locks.CheckFileLocks;
using PlikShare.Search;
using PlikShare.Search.Get;
using PlikShare.Storages;
using PlikShare.Storages.Create;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Users.UserEncryptionPassword;
using PlikShare.Storages.Delete;
using PlikShare.Storages.FileCopying;
using PlikShare.Storages.FileCopying.BulkInitiateCopyFiles;
using PlikShare.Storages.FileCopying.CopyFile;
using PlikShare.Storages.FileCopying.Delete;
using PlikShare.Storages.FileCopying.OnCompletedActionHandler;
using PlikShare.Storages.HardDrive;
using PlikShare.Storages.HardDrive.GetVolumes;
using PlikShare.Storages.List;
using PlikShare.Storages.Names;
using PlikShare.Storages.AzureBlob;
using PlikShare.Storages.S3.AwsS3;
using PlikShare.Storages.S3.BackblazeB2;
using PlikShare.Storages.S3.CloudflareR2;
using PlikShare.Storages.S3.DigitalOcean;
using PlikShare.Storages.S3.GoogleCloudStorage;
using PlikShare.Storages.UpdateDefaultTrashPolicy;
using PlikShare.Storages.UpdateDetails;
using PlikShare.Storages.UpdateName;
using PlikShare.Trash;
using PlikShare.Trash.DeleteForever;
using PlikShare.Trash.Empty;
using PlikShare.Trash.List;
using PlikShare.Trash.Restore;
using PlikShare.Trash.Sweeper;
using PlikShare.Uploads;
using PlikShare.Uploads.Abort.QueueJob;
using PlikShare.Uploads.Cache;
using PlikShare.Uploads.CompleteFileUpload;
using PlikShare.Uploads.CompleteFileUpload.QueueJob;
using PlikShare.Uploads.Count;
using PlikShare.Uploads.Delete;
using PlikShare.Uploads.FilePartUpload.Complete;
using PlikShare.Uploads.FilePartUpload.Initiate;
using PlikShare.Uploads.GetDetails;
using PlikShare.Uploads.Initiate;
using PlikShare.Uploads.Initiate.Contracts;
using PlikShare.Uploads.List;
using PlikShare.Users;
using PlikShare.Users.Cache;
using PlikShare.Users.Delete;
using PlikShare.Users.GetDetails;
using PlikShare.Users.GetOrCreate;
using PlikShare.Users.Invite;
using PlikShare.Users.List;
using PlikShare.Users.UpdateDefaultMaxWorkspaceSizeInBytes;
using PlikShare.Users.UpdateDefaultMaxWorkspaceTeamMembers;
using PlikShare.Users.StorageAccess;
using PlikShare.Users.UpdateMaxWorkspaceNumber;
using PlikShare.Users.UpdatePermissionsAndRoles;
using PlikShare.Users.UserIdentityResolver;
using PlikShare.Widgets;
using PlikShare.Workspaces;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.ChangeOwner;
using PlikShare.Workspaces.CountSelectedItems;
using PlikShare.Workspaces.Create;
using PlikShare.Workspaces.CreateBucket;
using PlikShare.Workspaces.Delete;
using PlikShare.Workspaces.Delete.QueueJob;
using PlikShare.Workspaces.DeleteBucket;
using PlikShare.Workspaces.Encryption;
using PlikShare.Workspaces.GetSize;
using PlikShare.Workspaces.Members.AcceptInvitation;
using PlikShare.Workspaces.Members.CountAll;
using PlikShare.Workspaces.Members.CreateInvitation;
using PlikShare.Workspaces.Members.GrantEncryptionAccess;
using PlikShare.Workspaces.Members.GrantEncryptionAccess.Cleanup;
using PlikShare.Workspaces.Members.LeaveWorkspace;
using PlikShare.Workspaces.Members.List;
using PlikShare.Workspaces.Members.RejectInvitation;
using PlikShare.Workspaces.ListAll;
using PlikShare.Workspaces.Members.AdminAdd;
using PlikShare.Workspaces.Members.Revoke;
using PlikShare.Workspaces.Members.UpdatePermissions;
using PlikShare.Workspaces.SearchFilesTree;
using PlikShare.Workspaces.UpdateCurrentSizeInBytes.QueueJob;
using PlikShare.Workspaces.UpdateMaxSize;
using PlikShare.Workspaces.UpdateMaxTeamMembers;
using PlikShare.Workspaces.UpdateName;
using PlikShare.Workspaces.UpdateTrashPolicy;
using PlikShare.Workspaces.UpdateImageDimensionsPolicy;
using PlikShare.Workspaces.UpdateThumbnailsPolicy;
using PlikShare.QuickShares;
using PlikShare.QuickShares.Cache;
using PlikShare.QuickShares.Create;
using PlikShare.QuickShares.Delete;
using PlikShare.QuickShares.EffectiveSet;
using PlikShare.QuickShares.Get;
using PlikShare.QuickShares.List;
using PlikShare.QuickShares.TrackDownload;
using PlikShare.QuickShares.UpdateExpiration;
using PlikShare.QuickShares.UpdateItems;
using PlikShare.QuickShares.UpdateMaxDownloads;
using PlikShare.QuickShares.UpdateMode;
using PlikShare.QuickShares.UpdateName;
using PlikShare.QuickShares.UpdatePassword;
using PlikShare.QuickShares.UpdateSlug;
using PlikShare.QuickShareExternalAccess;
using PlikShare.QuickShareExternalAccess.Authorization;
using PlikShare.QuickShareExternalAccess.EffectiveSet;
using PlikShare.QuickShareExternalAccess.GetBulkDownloadLink;
using PlikShare.QuickShareExternalAccess.GetContent;
using PlikShare.QuickShareExternalAccess.GetFileDownloadLink;
using PlikShare.QuickShareExternalAccess.GetZipBulkDownloadLink;
using PlikShare.QuickShareExternalAccess.GetZipContentDownloadLink;
using PlikShare.QuickShareExternalAccess.GetZipFileDetails;
using Serilog;
using Serilog.Events;

namespace PlikShare;

public class Startup
{
    public static void SetupWebAppBuilder(WebApplicationBuilder builder)
    {
        SetupLogger(builder);
        
        SetupHsts(builder);

        SetupResponseCompression(builder);

        // Add services to the container.
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        builder.Services.AddHttpClient();
        builder.Services.AddMemoryCache();
        
        builder.Services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(30)
            };
        });

        builder.SetupCors();

        SetupJsonSerialization(builder);

        builder.UseVolumes();
        builder.UseMasterDataEncryption();

        builder.UseSqLite();
        builder.UseSqLiteForDataProtection();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_01_InitialDbSetup>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_02_FilesCreatedAtFoldersCreatedAtAndCreator>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_03_StorageEncryptionIntoruced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_04_FileUploadIsCompletedIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_05_FileArtifactsIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_06_IntegrationsTableIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_07_IntegrationsTextractJobsTableIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_08_CopyFileQueueTableIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_09_FilesParentFileIdColumnIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_10_FilesMetadataColumnIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_11_FileUploadsParentFileIdAndMetadataColumnsIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_12_QueueSagasTableIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_13_FilesAndFoldersLookupIndicesIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_14_WorkspaceIdAddedToIntegrationsTable>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_15_ReencryptDatabaseFromAesCcmToAesGcm>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_16_SignUpCheckboxesIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_17_WorkspaceMaxSizeColumnIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_18_UserMaxWorkspaceNumberAndMaxWorkspaceSizeColumnsIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_19_MaxWorkspaceTeamMembersColumnsIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_20_WidgetOriginsColumnAddedToBoxLinksTable>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_21_AuthProvidersTableIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_22_WorkspaceEncryptionSaltIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_23_FilesEncryptionChainSaltsIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_24_UserEncryptionKeypairIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_25_StorageEncryptionKeysIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_26_WorkspaceEncryptionKeysIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_27_InvitationCodeHashedAndInvalidated>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_28_EphemeralWorkspaceEncryptionKeysIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_29_FileArtifactContentHashIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_30_QSoftRetriesLeftIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_31_DigitalOceanSpacesStorageTypeRenamed>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_32_StorageQueueJobsRenamedToBackendNeutralNames>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_33_StorageDbColumnsRenamedToBackendNeutralNames>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_34_ReencryptDatabaseFromSlowPathToFastPath>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_35_FolderAndFilePositionIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_36_UserStorageAccessIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_37_WorkspaceAuditLogDisabledEventsIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_38_QuickSharesIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_39_TrashIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_40_QueueResultAndThumbnailIndexesIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_41_QueueJobCategoryAndPriorityColumns>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_42_BoxDefaultDisplayConfigurationIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_43_MediaProcessingPolicyIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_44_RecategorizeDbOnlyJobsToNormal>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_45_QueueBatchItemsCountColumns>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_46_AlignDependentFilesFolderIdWithParent>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_47_FolderChildrenMetadataIndexIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_48_QueueWorkspaceIdAndQueueFileJobs>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_49_ThumbnailJobsUploaderIdentityIntroduced>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_Ai_02_ReencryptDatabaseFromSlowPathToFastPath>();

        builder.Services.AddSingleton<ISQLiteMigration, Migration_Ai_01_InitialDbSetup>();

        builder.Services.AddSingleton<ISQLiteMigration, Migration_AuditLog_01_InitialDbSetup>();
        builder.Services.AddSingleton<ISQLiteMigration, Migration_AuditLog_02_BoxColumnsAdded>();

        builder.Services.AddSingleton<AuditLogChannel>();
        builder.Services.AddSingleton<AuditLogService>();
        builder.Services.AddHostedService<AuditLogWriter>();
        builder.Services.AddSingleton<GetAuditLogQuery>();
        builder.Services.AddSingleton<GetAuditLogStatsQuery>();
        builder.Services.AddSingleton<GetAuditLogFilterOptionsQuery>();
        builder.Services.AddSingleton<GetAuditLogEntryDetailsQuery>();
        builder.Services.AddSingleton<AuditLogDetailsDecryptor>();
        builder.Services.AddSingleton<WorkspaceAuditLogPolicyCache>();
        builder.Services.AddSingleton<AuditLogPolicyEvaluator>();
        builder.Services.AddSingleton<GetWorkspaceAuditLogPolicyQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceAuditLogPolicyQuery>();
        builder.Services.AddSingleton<GetAuditLogVolumeStatsQuery>();
        builder.Services.AddSingleton<GetWorkspacesWithAuditLogPolicyQuery>();
        builder.Services.AddSingleton<DeleteOldAuditLogsQuery>();
        builder.Services.AddSingleton<ArchiveAuditLogsQuery>();
        builder.Services.AddSingleton<GetFileAuditContextQuery>();
        builder.Services.AddSingleton<GetFolderAuditContextQuery>();
        builder.Services.AddSingleton<GetFileUploadAuditContextQuery>();
        builder.Services.AddSingleton<GetStorageAuditContextQuery>();

        builder.UseAppSettings();

        builder.StartSqLiteQueueProcessing(
            parallelConsumersCount: builder
                .Configuration
                .GetValue<int>("Queue:ParallelConsumersNumber"));
        
        builder.UseStorage();

        SetupIdentityCore(builder);
        RegisterServices(builder);

        builder.SetupAuth();
    }
    
     private static void SetupIdentityCore(WebApplicationBuilder builder)
    {
        builder
            .Services
            .AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddRoles<ApplicationRole>()
            .AddSQLiteStores()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddPlikShareSecurityStampValidator();
    }

    private static void SetupHsts(WebApplicationBuilder builder)
    {
        builder.Services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(60);
        });
    }

    private static void SetupLogger(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        Log.Logger = new LoggerConfiguration()
            .WriteTo
            .Console()
            .CreateBootstrapLogger();

        builder.Host.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));
    }

    private static void SetupResponseCompression(
        WebApplicationBuilder builder)
    {
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });

        builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });

        builder.Services.Configure<GzipCompressionProviderOptions>(options =>
        {
            options.Level = CompressionLevel.Optimal;
        });
    }

    private static void SetupJsonSerialization(
        WebApplicationBuilder builder)
    {
        builder
            .Services
            .ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.AddConverters();
            });
    }

    private static void RegisterServices(
        WebApplicationBuilder builder)
    {
        // Queue job category/priority map, built from the executor TYPES (static members) as they are
        // registered — no instance is constructed here, so nothing pulls in IQueue. The finished map
        // is handed to QueueJobInfoProvider at the end of this method (see registration below), which
        // is why the provider has no DI dependency on the executors and can't form a cycle.
        var queueJobCategory = new Dictionary<string, QueueJobCategory>(StringComparer.InvariantCultureIgnoreCase);
        var queueJobPriority = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

        void AddNormalQueueJob<T>() where T : class, IQueueNormalJobExecutor
        {
            builder.Services.AddSingleton<IQueueNormalJobExecutor, T>();
            queueJobCategory.Add(T.StaticJobType, QueueJobCategory.Normal);
            queueJobPriority.Add(T.StaticJobType, T.StaticPriority);
        }

        void AddLongRunningQueueJob<T>() where T : class, IQueueLongRunningJobExecutor
        {
            builder.Services.AddSingleton<IQueueLongRunningJobExecutor, T>();
            queueJobCategory.Add(T.StaticJobType, QueueJobCategory.LongRunning);
            queueJobPriority.Add(T.StaticJobType, T.StaticPriority);
        }

        builder.Services.AddSingleton<BoxLinkTokenService>();

        builder.Services.AddSingleton<IOneTimeCode, OneTimeCode>();
        builder.Services.AddSingleton<IConfig, AppConfig>();
        builder.Services.AddSingleton<IClock, Clock>();  
        AddNormalQueueJob<EmailQueueJobExecutor>();
        builder.Services.AddSingleton<EmailProviderStore>();
        builder.Services.AddSingleton<AlertsService>();
        builder.Services.AddSingleton<QueueJobStatusDecisionEngine>();

        builder.Services.AddSingleton<GenericEmailTemplate>();

        AddNormalQueueJob<CreateWorkspaceBucketJobExecutor>();
        builder.Services.AddSingleton<CreateWorkspaceQuery>();
        builder.Services.AddSingleton<UpsertStorageEncryptionKeyQuery>();
        builder.Services.AddSingleton<RevokeStorageEncryptionKeyQuery>();
        builder.Services.AddSingleton<UpsertWorkspaceEncryptionKeyQuery>();
        builder.Services.AddSingleton<WorkspaceCreationPreparation>();
        builder.Services.AddExceptionHandler<WorkspaceDekUnavailableExceptionHandler>();
        builder.Services.AddSingleton<RevokeWorkspaceEncryptionKeyQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceNameQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceMaxSizeQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceMaxTeamMembersQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceTrashPolicyQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceImageDimensionsPolicyQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceThumbnailsPolicyQuery>();
        builder.Services.AddSingleton<ExtractImageDimensionsBackfillOperation>();
        builder.Services.AddSingleton<ImageDimensionsBackfillStatusQuery>();
        builder.Services.AddSingleton<BatchProgressQuery>();
        builder.Services.AddSingleton<CancelBatchOperation>();
        builder.Services.AddSingleton<CreateWorkspaceMemberInvitationQuery>();
        builder.Services.AddSingleton<CreateWorkspaceMemberInvitationOperation>();
        builder.Services.AddSingleton<RollbackEncryptedInvitationQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceIsBucketCreatedQuery>();
        builder.Services.AddSingleton<GetWorkspaceMembersListQuery>();
        builder.Services.AddSingleton<CountWorkspaceTotalTeamMembersQuery>();
        builder.Services.AddSingleton<RevokeWorkspaceMemberQuery>();
        builder.Services.AddSingleton<LeaveSharedWorkspaceQuery>();
        builder.Services.AddSingleton<UpdateWorkspaceMemberPermissionsQuery>();
        builder.Services.AddSingleton<GrantEncryptionAccessOperation>();
        builder.Services.AddSingleton<NotifyOwnersOfPendingGrantsQuery>();
        builder.Services.AddSingleton<UpsertEphemeralWorkspaceEncryptionKeyQuery>();
        builder.Services.AddSingleton<CreateOrGetEphemeralUserKeyPairQuery>();
        builder.Services.AddSingleton<PromoteEphemeralWorkspaceEncryptionKeysQuery>();
        AddNormalQueueJob<DeleteEphemeralWorkspaceEncryptionKeysQueueJobExecutor>();
        builder.Services.AddSingleton<WorkspaceCache>();
        builder.Services.AddSingleton<WorkspaceMembershipCache>();
        builder.Services.AddSingleton<ScheduleWorkspaceDeleteQuery>();
        builder.Services.AddSingleton<DeleteWorkspaceWithDependenciesQuery>();
        AddNormalQueueJob<DeleteWorkspaceQueueJobExecutor>();
        AddNormalQueueJob<DeleteBucketJobExecutor>();
        AddNormalQueueJob<UpdateWorkspaceCurrentSizeInBytesQueueJobExecutor>();
        builder.Services.AddSingleton<BulkDeleteQuery>();
        builder.Services.AddSingleton<ChangeWorkspaceOwnerQuery>();
        builder.Services.AddSingleton<GetWorkspaceSizeQuery>();
        builder.Services.AddSingleton<WorkspaceSizeCache>();
        builder.Services.AddSingleton<ListAllWorkspacesQuery>();
        builder.Services.AddSingleton<AdminAddWorkspaceMemberOperation>();

        builder.Services.AddSingleton<UserCache>();
        builder.Services.AddSingleton<GetOrCreateUserInvitationQuery>();
        builder.Services.AddSingleton<IOneTimeInvitationCode, OneTimeInvitationCode>();
        builder.Services.AddSingleton<InviteUsersQuery>();
        builder.Services.AddSingleton<GetUsersQuery>();
        builder.Services.AddSingleton<DeleteUserQuery>();
        builder.Services.AddSingleton<GetUserDetailsQuery>();
        builder.Services.AddSingleton<UpdateUserPermissionsAndRoleQuery>();
        builder.Services.AddSingleton<UpdateUserMaxWorkspaceNumberQuery>();

        builder.Services.AddSingleton<UpsertUserEncryptionDataQuery>();
        builder.Services.AddSingleton<SetupUserEncryptionPasswordOperation>();
        builder.Services.AddSingleton<UnlockUserEncryptionPasswordOperation>();
        builder.Services.AddSingleton<ChangeUserEncryptionPasswordOperation>();
        builder.Services.AddSingleton<ResetUserEncryptionPasswordOperation>();
        builder.Services.AddSingleton<UpdateUserDefaultMaxWorkspaceSizeInBytesQuery>();
        builder.Services.AddSingleton<UpdateUserDefaultMaxWorkspaceTeamMembersQuery>();
        builder.Services.AddSingleton<UpdateUserStorageAccessQuery>();

        builder.Services.AddScoped<IValidator<CreateFolderRequestDto>, CreateFolderRequestValidator>();
        builder.Services.AddScoped<IValidator<BulkCreateFolderRequestDto>, BulkCreateFolderRequestValidator>();
        builder.Services.AddScoped<IValidator<UpdateFolderNameRequestDto>, UpdateFolderNameRequestValidator>();

        builder.Services.AddSingleton<ConvertFileUploadToFileOperation>();
        AddNormalQueueJob<CompleteMultipartUploadQueueJobExecutor>();

        builder.Services.AddSingleton<FileUploadCache>();
        builder.Services.AddSingleton<CompleteFilePartUploadQuery>();
        builder.Services.AddSingleton<InitiateFilePartUploadOperation>();
        builder.Services.AddSingleton<GetUploadsCountQuery>();
        builder.Services.AddSingleton<DeleteFileUploadsSubQuery>();

        builder.Services.AddSingleton<GetFileUploadDetailsQuery>();

        builder.Services.AddSingleton<UpdateFileNameQuery>();
        builder.Services.AddSingleton<UpdateFileSizeQuery>();

        builder.Services.AddSingleton<GetFileDetailsQuery>();
        builder.Services.AddSingleton<GetFileDownloadLinkOperation>();
        builder.Services.AddSingleton<GetBulkDownloadDetailsQuery>();
        builder.Services.AddSingleton<GetBulkDownloadLinkOperation>();

        builder.Services.AddSingleton<GetOrCreateFolderQuery>();
        builder.Services.AddSingleton<CreateFolderQuery>();
        builder.Services.AddSingleton<CountSelectedItemsQuery>();
        builder.Services.AddSingleton<UpdateFolderNameQuery>();
        builder.Services.AddSingleton<GetTopFolderContentQuery>();
        builder.Services.AddSingleton<GetFolderContentQuery>();
        builder.Services.AddSingleton<BulkDeleteFoldersWithDependenciesQuery>();
        AddNormalQueueJob<DeleteFoldersQueueJobExecutor>();

        builder.Services.AddSingleton<GetUploadsListQuery>();
        AddNormalQueueJob<AbortMultipartUploadQueueJobExecutor>();
        AddNormalQueueJob<DeleteFileQueueJobExecutor>();
        AddLongRunningQueueJob<BulkDeleteFilesQueueJobExecutor>();
        builder.Services.AddSingleton<MoveItemsToFolderQuery>();
        builder.Services.AddSingleton<UpdatePositionsQuery>();
        builder.Services.AddSingleton<GetBoxQuery>();
        builder.Services.AddSingleton<DeleteFilesSubQuery>();
        builder.Services.AddSingleton<PathSnapshotBuilder>();
        builder.Services.AddSingleton<SoftDeleteFilesSubQuery>();
        builder.Services.AddSingleton<PurgeFilesSubQuery>();
        builder.Services.AddSingleton<GetTrashItemsQuery>();
        builder.Services.AddSingleton<RestoreFromTrashQuery>();
        builder.Services.AddSingleton<DeleteForeverQuery>();
        builder.Services.AddSingleton<EmptyTrashQuery>();

        builder.Services.AddSingleton(_ =>
            builder.Configuration.GetSection("Trash").Get<TrashSweeperOptions>()
            ?? new TrashSweeperOptions());

        builder.Services.AddHostedService<TrashSweeperHostedService>();

        builder.Services.AddSingleton<CreateBoxQuery>();
        builder.Services.AddSingleton<GetBoxesListQuery>();
        builder.Services.AddSingleton<UpdateBoxNameQuery>();
        builder.Services.AddSingleton<UpdateBoxFolderQuery>();
        builder.Services.AddSingleton<UpdateBoxIsEnabledQuery>();
        builder.Services.AddSingleton<ScheduleBoxesDeleteQuery>();
        builder.Services.AddSingleton<CreateBoxMemberInvitationQuery>();
        builder.Services.AddSingleton<CreateBoxMemberInvitationOperation>();
        builder.Services.AddSingleton<LeaveBoxMembershipQuery>();
        builder.Services.AddSingleton<RevokeBoxMemberQuery>();
        builder.Services.AddSingleton<UpdateBoxMemberPermissionsQuery>();
        builder.Services.AddSingleton<BoxCache>();
        builder.Services.AddSingleton<BoxMembershipCache>();
        builder.Services.AddSingleton<BatchDeleteBoxesWithDependenciesQuery>();
        builder.Services.AddSingleton<UpdateBoxHeaderQuery>();
        builder.Services.AddSingleton<UpdateBoxFooterQuery>();
        builder.Services.AddSingleton<UpdateBoxHeaderIsEnabledQuery>();
        builder.Services.AddSingleton<UpdateBoxFooterIsEnabledQuery>();
        builder.Services.AddSingleton<UpdateBoxDefaultDisplayConfigurationQuery>();
        AddNormalQueueJob<DeleteBoxesQueueJobExecutor>();

        builder.Services.AddSingleton<CreateBoxLinkQuery>();
        builder.Services.AddSingleton<UpdateBoxLinkNameQuery>();
        builder.Services.AddSingleton<UpdateBoxLinkWidgetOriginsQuery>();
        builder.Services.AddSingleton<UpdateBoxLinkIsEnabledQuery>();
        builder.Services.AddSingleton<UpdateBoxLinkPermissionsQuery>();
        builder.Services.AddSingleton<RegenerateBoxLinkAccessCodeQuery>();
        builder.Services.AddSingleton<DeleteBoxLinkQuery>();
        builder.Services.AddSingleton<BoxLinkCache>();

        builder.Services.AddSingleton<GetBoxContentHandler>();
        builder.Services.AddSingleton<BoxExternalAccessHandler>();
        builder.Services.AddSingleton<GetBoxHtmlQuery>();

        builder.Services.AddSingleton<RejectWorkspaceInvitationQuery>();
        builder.Services.AddSingleton<AcceptWorkspaceInvitationQuery>();
        builder.Services.AddSingleton<AcceptBoxInvitationQuery>();
        builder.Services.AddSingleton<RejectBoxInvitationQuery>();

        builder.Services.AddSingleton<GetDashboardContentQuery>();

        builder.Services.AddSingleton<GetSearchQuery>();
        builder.Services.AddSingleton<SearchSessionLoader>();

        builder.Services.AddScoped<IValidator<UpdateFileNameRequestDto>, UpdateFileNameRequestValidator>();

        builder.Services.AddSingleton<HardDriveStorageClientFactory>();
        builder.Services.AddSingleton<CloudflareR2StorageClientFactory>();
        builder.Services.AddSingleton<BackblazeB2StorageClientFactory>();
        builder.Services.AddSingleton<AwsS3StorageClientFactory>();
        builder.Services.AddSingleton<DigitalOceanStorageClientFactory>();
        builder.Services.AddSingleton<AzureBlobStorageClientFactory>();
        builder.Services.AddSingleton<GoogleCloudStorageClientFactory>();
        builder.Services.AddSingleton<CreateStorageFlow>();
        builder.Services.AddSingleton<UpdateStorageFlow>();
        builder.Services.AddSingleton<GetStoragesQuery>();
        builder.Services.AddSingleton<GetStorageNamesQuery>();
        builder.Services.AddSingleton<DeleteStorageQuery>();
        builder.Services.AddSingleton<UpdateStorageNameQuery>();
        builder.Services.AddSingleton<UpdateStorageDetailsQuery>();
        builder.Services.AddSingleton<UpdateStorageDefaultTrashPolicyQuery>();
        builder.Services.AddSingleton<CreateStorageQuery>();

        builder.Services.AddSingleton<CheckUserInvitationCodeQuery>();
        builder.Services.AddSingleton<HasPendingEphemeralEncryptionKeysQuery>();

        builder.Services.AddSingleton<CreateEmailProviderQuery>();
        builder.Services.AddSingleton<DeleteEmailProviderQuery>();
        builder.Services.AddSingleton<CreateEmailProviderOperation>();
        builder.Services.AddSingleton<UpdateEmailProviderNameQuery>();
        builder.Services.AddSingleton<GetEmailProviderQuery>();
        builder.Services.AddSingleton<ResendConfirmationEmailOperation>();
        builder.Services.AddSingleton<ConfirmEmailProviderQuery>();
        builder.Services.AddSingleton<ActivateEmailProviderQuery>();
        builder.Services.AddSingleton<DeactivateEmailProviderQuery>();
        builder.Services.AddSingleton<GetEmailProvidersQuery>();
        builder.Services.AddSingleton<ResendEmailSenderFactory>();
        builder.Services.AddSingleton<SmtpEmailSenderFactory>();
        builder.Services.AddSingleton<EmailSenderFactory>();
        builder.Services.AddSingleton<EmailProviderConfirmationEmail>();

        builder.Services.AddSingleton<CreateAuthProviderQuery>();
        builder.Services.AddSingleton<DeleteAuthProviderQuery>();
        builder.Services.AddSingleton<UpdateAuthProviderNameQuery>();
        builder.Services.AddSingleton<ActivateAuthProviderQuery>();
        builder.Services.AddSingleton<DeactivateAuthProviderQuery>();
        builder.Services.AddSingleton<GetAuthProvidersQuery>();
        builder.Services.AddSingleton<GetActiveAuthProvidersPublicQuery>();
        builder.Services.AddSingleton<GetAuthProviderDetailsQuery>();
        builder.Services.AddSingleton<UpdateAuthProviderQuery>();
        builder.Services.AddSingleton<TestAuthProviderConfigurationOperation>();
        builder.Services.AddSingleton<CheckUserHasSsoLoginQuery>();

        builder.Services.AddSingleton<OidcDiscoveryCache>();
        builder.Services.AddSingleton<OidcJwksCache>();
        builder.Services.AddSingleton<OidcStateProtector>();
        builder.Services.AddSingleton<GetOrCreateSsoUserQuery>();
        builder.Services.AddSingleton<UploadLegalFileOperation>();
        builder.Services.AddSingleton<DeleteLegalFileOperation>();

        builder.Services.AddSingleton<GetApplicationSettingsStatusQuery>();

        builder.Services.AddSingleton<PreSignedPayloadTokenStore>();
        builder.Services.AddSingleton<PreSignedUrlsService>();
        builder.Services.AddSingleton<GetHardDriveVolumesOperation>();
        builder.Services.AddSingleton<GetFilePreSignedDownloadLinkDetailsQuery>();

        builder.Services.AddSingleton<BulkDownloadDetailsQuery>();
        
        builder.Services.AddSingleton<SaveFileNoteQuery>();
        builder.Services.AddSingleton<GetFilePreviewDetailsQuery>();
        builder.Services.AddSingleton<UserIdentityResolver>();
        builder.Services.AddSingleton<CreateFileCommentQuery>();
        builder.Services.AddSingleton<DeleteFileCommentQuery>();
        builder.Services.AddSingleton<UpdateFileCommentQuery>();

        builder.Services.AddSingleton<GetKnownUsersQuery>();
        builder.Services.AddSingleton<GetZipFileDetailsOperation>();
        builder.Services.AddSingleton<GetZipContentDownloadLinkOperation>();
        builder.Services.AddSingleton<GetZipBulkDownloadLinkOperation>();

        builder.Services.AddSingleton<InsertFileUploadPartQuery>();
        builder.Services.AddSingleton<MarkFileAsUploadedAndDeleteUploadQuery>();
        builder.Services.AddSingleton<ConvertFileUploadToFileQuery>();

        builder.Services.AddSingleton<BulkInsertFileUploadQuery>();
        builder.Services.AddSingleton<BulkInitiateFileUploadOperation>();
        builder.Services.AddSingleton<BulkInitiateCopyFileUploadOperation>();
        builder.Services.AddSingleton<BulkConvertDirectFileUploadsToFilesQuery>();

        builder.Services.AddScoped<IValidator<BulkInitiateFileUploadRequestDto>, BulkInitiateFileUploadRequestValidator>();

        builder.Services.AddSingleton<GetIntegrationsQuery>();
        builder.Services.AddSingleton<DeleteIntegrationQuery>();
        builder.Services.AddSingleton<UpdateIntegrationNameQuery>();
        builder.Services.AddSingleton<ActivateIntegrationQuery>();
        builder.Services.AddSingleton<ActivateIntegrationOperation>();
        builder.Services.AddSingleton<DeactivateIntegrationQuery>();
        builder.Services.AddSingleton<CreateIntegrationOperation>();

        builder.Services.AddSingleton<CreateIntegrationWithWorkspaceQuery>();
        builder.Services.AddSingleton<CheckTextractJobsStatusQuery>();
        builder.Services.AddSingleton<TestTextractConfigurationOperation>();
        builder.Services.AddSingleton<TextractClientStore>();
        builder.Services.AddSingleton<RegisterTextractClientOperation>();
        builder.Services.AddSingleton<StartTextractJobOperation>();
        builder.Services.AddSingleton<TextractResultTemporaryStore>();
        builder.Services.AddSingleton<DeleteTextractJobsSubQuery>();
        AddNormalQueueJob<InitiateTextractAnalysisQueueJobExecutor>();
        AddLongRunningQueueJob<CheckTextractAnalysisStatusQueueJobExecutor>();
        AddLongRunningQueueJob<DownloadTextractAnalysisQueueJobExecutor>();

        AddNormalQueueJob<BulkInitiateCopyFilesQueueJobExecutor>();

        builder.Services.AddSingleton<FinalizeCopyFileUploadQuery>();
        builder.Services.AddSingleton<DeleteCopyFileQueueJobsSubQuery>();
        builder.Services.AddSingleton<ICopyFileQueueCompletedActionHandler, UpdateTextractJobFileAndStatusOnCompletedFileCopyHandler>();
        AddLongRunningQueueJob<CopyFileQueueJobExecutor>();

        builder.Services.AddSingleton<SearchFilesTreeQuery>();
        builder.Services.AddSingleton<TestChatGptConfigurationOperation>();
        builder.Services.AddSingleton<ChatGptClientStore>();
        builder.Services.AddSingleton<RegisterChatGptClientOperation>();

        builder.Services.AddSingleton<SendAiFileMessageOperation>();
        builder.Services.AddSingleton<GetFileArtifactWithAiConversationQuery>();
        builder.Services.AddSingleton<UpdateAiConversationNameOperation>();
        builder.Services.AddSingleton<DeleteAiConversationOperation>();
        builder.Services.AddSingleton<GetAiMessagesOperation>();
        builder.Services.AddSingleton<CheckAiConversationsStatusQuery>();
        builder.Services.AddSingleton<GetFilesToIncludeDetailsQuery>();
        builder.Services.AddSingleton<GetFullAiConversationQuery>();
        builder.Services.AddSingleton<SaveAiChatCompletionQuery>();
        builder.Services.AddSingleton<InsertFileAttachmentQuery>();
        builder.Services.AddSingleton<MarkFileAsUploadedQuery>();
        builder.Services.AddSingleton<GetThumbnailsQuery>();
        builder.Services.AddSingleton<GetFileQuery>();
        builder.Services.AddSingleton<GetThumbnailDownloadDetailsQuery>();
        builder.Services.AddSingleton<BoxFileThumbnailHandler>();
        builder.Services.AddSingleton<HardDeleteFilesWithStorageCleanupSubQuery>();
        builder.Services.AddSingleton<InsertAndFinalizeThumbnailQuery>();
        builder.Services.AddSingleton<DeleteThumbnailsQuery>();
        builder.Services.AddSingleton<UploadFileThumbnailOperation>();
        builder.Services.AddSingleton<ValidateThumbnailParentQuery>();
        builder.Services.AddSingleton<DeleteFileThumbnailOperation>();
        builder.Services.AddSingleton<FfmpegService>();
        
        builder.Services.AddSingleton(_ =>
            builder.Configuration.GetSection("EphemeralKeyRing").Get<EphemeralKeyRingOptions>()
            ?? new EphemeralKeyRingOptions());

        builder.Services.AddSingleton<EphemeralKeyRing>();
        builder.Services.AddHostedService<EphemeralKeyRingSweeperHostedService>();

        AddLongRunningQueueJob<ProcessImageQueueJobExecutor>();
        AddLongRunningQueueJob<ProcessImageQueueJobExecutorV2>();
        AddLongRunningQueueJob<GenerateImageThumbnailsJobExecutor>();
        AddLongRunningQueueJob<ExtractImageDimensionsQueueJobExecutor>();
        builder.Services.AddSingleton<UpsertParentImageDimensionsQuery>();
        builder.Services.AddSingleton<FileCreatedDispatcher>();
        builder.Services.AddSingleton<IFileCreatedHandler, DimensionsFileCreatedHandler>();
        builder.Services.AddSingleton<IFileCreatedHandler, ThumbnailsFileCreatedHandler>();
        builder.Services.AddSingleton<GenerateFileThumbnailsBulkOperation>();
        builder.Services.AddSingleton<ThumbnailsBackfillOperation>();
        builder.Services.AddSingleton<ThumbnailsBackfillStatusQuery>();
        builder.Services.AddSingleton<GetThumbnailableSelectionFilesQuery>();
        builder.Services.AddSingleton<GetThumbnailGenerationStatusQuery>();
        builder.Services.AddSingleton<GetProcessingFileJobsQuery>();
        builder.Services.AddSingleton<CancelThumbnailBatchOperation>();
        builder.Services.AddSingleton<DownloadFileConvertedOperation>();
        AddLongRunningQueueJob<SendAiMessageQueueJobExecutor>();
        AddNormalQueueJob<DeleteAiConversationQueueJobExecutor>();

        builder.Services.AddSingleton<CreateOrUpdateSignUpCheckboxQuery>();
        builder.Services.AddSingleton<DeleteSignUpCheckboxQuery>();

        builder.Services.AddSingleton<CheckFileLocksQuery>();

        builder.Services.AddSingleton<QuickShareCache>();
        builder.Services.AddSingleton<QuickShareUrlBuilder>();
        builder.Services.AddSingleton<QuickSharePasswordHasher>();
        builder.Services.AddSingleton<QuickShareUnlockSession>();
        builder.Services.AddSingleton<CreateQuickShareQuery>();
        builder.Services.AddSingleton<DeleteQuickShareQuery>();
        builder.Services.AddSingleton<GetQuickShareItemsQuery>();
        builder.Services.AddSingleton<GetQuickSharesQuery>();
        builder.Services.AddSingleton<UpdateQuickShareNameQuery>();
        builder.Services.AddSingleton<UpdateQuickShareSlugQuery>();
        builder.Services.AddSingleton<UpdateQuickShareExpirationQuery>();
        builder.Services.AddSingleton<UpdateQuickSharePasswordQuery>();
        builder.Services.AddSingleton<UpdateQuickShareMaxDownloadsQuery>();
        builder.Services.AddSingleton<UpdateQuickShareModeQuery>();
        builder.Services.AddSingleton<UpdateQuickShareItemsQuery>();
        builder.Services.AddSingleton<GetQuickShareItemDbIdsQuery>();
        builder.Services.AddSingleton<TrackQuickShareDownloadQuery>();
        builder.Services.AddSingleton<GetQuickShareContentOperation>();
        builder.Services.AddSingleton<GenerateQuickShareBulkDownloadLinkOperation>();
        builder.Services.AddSingleton<GenerateQuickShareFileDownloadLinkOperation>();
        builder.Services.AddSingleton<IsFileInQuickShareQuery>();
        builder.Services.AddSingleton<GenerateQuickShareZipFileDetailsOperation>();
        builder.Services.AddSingleton<GenerateQuickShareZipContentDownloadLinkOperation>();
        builder.Services.AddSingleton<GenerateQuickShareZipBulkDownloadLinkOperation>();
        
        // Job-type maps were built (above) from the executor types as they were registered. Hand the
        // finished maps to the provider as a ready instance — no DI dependency on the executors, so no
        // IQueue -> Queue -> provider cycle.
        builder.Services.AddSingleton(new QueueJobInfoProvider(queueJobCategory, queueJobPriority));
    }

    public static void InitializeWebApp(WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        // Routes typed exceptions (e.g. WorkspaceDekForVersionNotAvailableException) through
        // IExceptionHandler implementations before the framework's default 500 takes over.
        app.UseExceptionHandler(_ => { });

        app.UseOperationCanceledHandler();
        app.UseResponseCompression();

        app.UseCors();
        app.UseHsts();
        app.UseHttpsRedirection();

        //app.UseMiddleware<RequestDetailsLoggingMiddleware>();
        app.UseMiddleware<HttpCorrelationIdMiddleware>();

        UseRequestLogging(app);

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseMiddleware<AutoAntiforgeryMiddleware>();

        //app.UseMiddleware<UserLoggingMiddleware>();

        app.UseMiddleware<AngularRoutingMiddleware>();

        UseStaticFiles(app);

        app.MapLockStatusEndpoints();
        app.MapApplicationSettingsEndpoints();
        app.MapAppCapabilitiesEndpoints();
        app.MapWorkspacesAdminEndpoints();
        app.MapFilesEndpoints();
        app.MapMediaProcessingEndpoints();
        app.MapFileProcessingEndpoints();
        app.MapEntryPageEndpoints();
        app.MapLegalFilesEndpoints();
        app.MapHealthCheckEndpoints();
        app.MapStoragesEndpoints();
        app.MapUserEncryptionSessionsEndpoints();
        app.MapUserEncryptionPasswordEndpoints();
        app.MapDashboardEndpoints();
        app.MapEmailProvidersEndpoints();
        app.MapAuthProvidersEndpoints();
        app.MapSsoEndpoints();
        app.MapAccountEndpoints();
        app.MapGeneralSettingsEndpoints();
        app.MapSearchEndpoints();
        app.MapUsersEndpoints();
        app.MapUploadsEndpoints();
        app.MapAuthEndpoints();
        app.MapBulkDownloadEndpoints();
        app.MapFoldersEndpoints();
        app.MapWorkspacesEndpoints();
        app.MapTrashEndpoints();
        app.MapBoxLinksEndpoints();
        app.MapPreSignedFilesEndpoints();
        app.MapPreSignedZipFilesEndpoints();
        app.MapPreSignedZipBulkDownloadEndpoints();
        app.MapBoxLinkAccessCodesEndpoints();
        app.MapBoxExternalAccessEndpoints();
        app.MapBoxesEndpoints();
        app.MapIntegrationsEndpoints();
        app.MapTextractEndpoints();
        app.MapChatGptEndpoints();
        app.MapAiEndpoints();
        app.MapAntiforgeryEndpoints();
        app.MapWidgetEndpoints();
        app.MapAuditLogEndpoints();
        app.MapAuditLogPolicyEndpoints();
        app.MapQuickSharesEndpoints();
        app.MapQuickShareExternalAccessEndpoints();
        
        //core functionality
        app.InitializeSqLite();
        app.InitializeStorageClientStore();
        app.InitializeEmailProvider();
        app.InitializeAppSettings();

        app.InitializeAppOwners();
        app.InitializeQueue();
        app.InitializeCopyFileQueue();

        //integrations
        app.InitializeTextractIntegrations();
        app.InitializeChatGptIntegrations();

        // Force ffmpeg probe at startup so the detection log fires regardless of whether any
        // request hits the capability endpoint or the generate endpoint first.
        app.Services.GetRequiredService<FfmpegService>();
    }

    private static void UseRequestLogging(WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.GetLevel = (context, d, arg3) =>
            {
                var level = HealthCheckUtils.IsHealthCheckEndpoint(context)
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Debug;

                return level;
            };
        });
    }

    private static void UseStaticFiles(WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                if (AngularExtensions.ShouldFileBeCached(ctx.File.Name))
                {
                    ctx.Context.Response.Headers["Cache-Control"] = "public; max-age=31536000";
                }
                else
                {
                    ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
                }
            }
        });
    }
}