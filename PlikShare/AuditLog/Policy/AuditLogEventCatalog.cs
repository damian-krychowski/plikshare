namespace PlikShare.AuditLog.Policy;

/// <summary>
/// Whether an audit-log event is logged with a workspace context (configured by a workspace's
/// policy) or without one (configured by the app-level policy). Serialized to JSON via the
/// global <c>JsonStringEnumConverter</c> with <c>KebabCaseLower</c> — values appear in API
/// responses as <c>"application"</c> / <c>"workspace"</c>.
/// </summary>
public enum AuditLogEventScope
{
    /// <summary>Logged with no workspace context — configured by the app-level policy only.</summary>
    Application,
    /// <summary>Logged with a workspace context — configured by that workspace's policy
    /// (which is snapshotted from the workspace-defaults policy at workspace creation).</summary>
    Workspace
}

/// <summary>
/// Static catalog of every audit-log event type with metadata for the policy UI:
/// human-readable description, severity, category, and scope (application-wide vs workspace-scoped).
/// The severity here mirrors what the <c>Audit.*Entry</c> factory methods stamp on entries at log
/// time; it is duplicated here so the frontend can render badges and group toggles without making
/// 119 round-trips to discover what each event looks like.
/// </summary>
public static class AuditLogEventCatalog
{
    public record EventMetadata(
        string EventType,
        string Category,
        string Severity,
        string Description,
        AuditLogEventScope Scope);

    public static readonly IReadOnlyList<EventMetadata> All =
    [
        // ── Auth ─────────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Auth.SignedUp,                  AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "User signed up.",                                        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SignUpFailed,              AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Sign-up attempt failed.",                                AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.EmailConfirmed,            AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "Email address confirmed.",                               AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.EmailConfirmationFailed,   AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Email confirmation attempt failed.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SignedIn,                  AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "User signed in.",                                        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SignInFailed,              AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Sign-in attempt failed.",                                AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SignedIn2Fa,               AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "User completed two-factor sign-in.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SignIn2FaFailed,           AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Two-factor sign-in attempt failed.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SignedOut,                 AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "User signed out.",                                       AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.PasswordChanged,           AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "Password changed.",                                      AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.PasswordChangeFailed,      AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Password change attempt failed.",                        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.PasswordResetRequested,    AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "Password reset requested.",                              AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.PasswordResetCompleted,    AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "Password reset completed.",                              AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.PasswordResetFailed,       AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Password reset attempt failed.",                         AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.TwoFaEnabled,              AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "Two-factor authentication enabled.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.TwoFaEnableFailed,         AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Enabling two-factor authentication failed.",             AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.TwoFaDisabled,             AuditLogEventCategories.Auth, AuditLogSeverities.Critical, "Two-factor authentication disabled.",                    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.RecoveryCodesRegenerated,  AuditLogEventCategories.Auth, AuditLogSeverities.Warning,  "Recovery codes regenerated.",                            AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SsoLogin,                  AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "User signed in via SSO.",                                AuditLogEventScope.Application),
        new(AuditLogEventTypes.Auth.SsoUserCreated,            AuditLogEventCategories.Auth, AuditLogSeverities.Info,     "New user created via SSO sign-in.",                      AuditLogEventScope.Application),

        // ── User management ──────────────────────────────────────────────────────
        new(AuditLogEventTypes.User.Invited,                                  AuditLogEventCategories.User, AuditLogSeverities.Info,     "User invited.",                                          AuditLogEventScope.Application),
        new(AuditLogEventTypes.User.Deleted,                                  AuditLogEventCategories.User, AuditLogSeverities.Critical, "User deleted.",                                          AuditLogEventScope.Application),
        new(AuditLogEventTypes.User.PermissionsAndRolesUpdated,               AuditLogEventCategories.User, AuditLogSeverities.Warning,  "User permissions or roles updated.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.User.MaxWorkspaceNumberUpdated,                AuditLogEventCategories.User, AuditLogSeverities.Info,     "User max-workspace-number quota updated.",               AuditLogEventScope.Application),
        new(AuditLogEventTypes.User.DefaultMaxWorkspaceSizeUpdated,           AuditLogEventCategories.User, AuditLogSeverities.Info,     "User default max-workspace-size quota updated.",         AuditLogEventScope.Application),
        new(AuditLogEventTypes.User.DefaultMaxWorkspaceTeamMembersUpdated,    AuditLogEventCategories.User, AuditLogSeverities.Info,     "User default max-workspace-team-members quota updated.", AuditLogEventScope.Application),
        new(AuditLogEventTypes.User.StorageAccessUpdated,                     AuditLogEventCategories.User, AuditLogSeverities.Warning,  "User storage access policy updated.",                    AuditLogEventScope.Application),

        // ── Settings ─────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Settings.AppNameChanged,                        AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Application name changed.",                              AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.SignUpOptionChanged,                   AuditLogEventCategories.Settings, AuditLogSeverities.Warning, "Sign-up option changed.",                                AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.DefaultPermissionsChanged,             AuditLogEventCategories.Settings, AuditLogSeverities.Warning, "Default permissions for new users changed.",             AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.DefaultMaxWorkspaceNumberChanged,      AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Default max-workspace-number for new users changed.",    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.DefaultMaxWorkspaceSizeChanged,        AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Default max-workspace-size for new users changed.",      AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.DefaultMaxWorkspaceTeamMembersChanged, AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Default max-workspace-team-members for new users changed.", AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.AlertOnNewUserChanged,                 AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Alert-on-new-user setting toggled.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.TermsOfServiceUploaded,                AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Terms-of-service document uploaded.",                    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.TermsOfServiceDeleted,                 AuditLogEventCategories.Settings, AuditLogSeverities.Warning, "Terms-of-service document deleted.",                     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.PrivacyPolicyUploaded,                 AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Privacy-policy document uploaded.",                      AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.PrivacyPolicyDeleted,                  AuditLogEventCategories.Settings, AuditLogSeverities.Warning, "Privacy-policy document deleted.",                       AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.SignUpCheckboxCreatedOrUpdated,        AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Sign-up checkbox created or updated.",                   AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.SignUpCheckboxDeleted,                 AuditLogEventCategories.Settings, AuditLogSeverities.Info,    "Sign-up checkbox deleted.",                              AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.PasswordLoginToggled,                  AuditLogEventCategories.Settings, AuditLogSeverities.Warning, "Password login toggled.",                                AuditLogEventScope.Application),
        new(AuditLogEventTypes.Settings.NewUserDefaultStorageAccessUpdated,    AuditLogEventCategories.Settings, AuditLogSeverities.Warning, "Default storage access for new users updated.",          AuditLogEventScope.Application),

        // ── Email providers ──────────────────────────────────────────────────────
        new(AuditLogEventTypes.EmailProvider.Created,                  AuditLogEventCategories.EmailProvider, AuditLogSeverities.Info,    "Email provider created.",                AuditLogEventScope.Application),
        new(AuditLogEventTypes.EmailProvider.Deleted,                  AuditLogEventCategories.EmailProvider, AuditLogSeverities.Warning, "Email provider deleted.",                AuditLogEventScope.Application),
        new(AuditLogEventTypes.EmailProvider.NameUpdated,              AuditLogEventCategories.EmailProvider, AuditLogSeverities.Info,    "Email provider renamed.",                AuditLogEventScope.Application),
        new(AuditLogEventTypes.EmailProvider.Activated,                AuditLogEventCategories.EmailProvider, AuditLogSeverities.Info,    "Email provider activated.",              AuditLogEventScope.Application),
        new(AuditLogEventTypes.EmailProvider.Deactivated,              AuditLogEventCategories.EmailProvider, AuditLogSeverities.Warning, "Email provider deactivated.",            AuditLogEventScope.Application),
        new(AuditLogEventTypes.EmailProvider.Confirmed,                AuditLogEventCategories.EmailProvider, AuditLogSeverities.Info,    "Email provider confirmed.",              AuditLogEventScope.Application),
        new(AuditLogEventTypes.EmailProvider.ConfirmationEmailResent,  AuditLogEventCategories.EmailProvider, AuditLogSeverities.Info,    "Confirmation email re-sent.",            AuditLogEventScope.Application),

        // ── Auth providers ───────────────────────────────────────────────────────
        new(AuditLogEventTypes.AuthProvider.Created,                AuditLogEventCategories.AuthProvider, AuditLogSeverities.Info,    "Authentication provider created.",            AuditLogEventScope.Application),
        new(AuditLogEventTypes.AuthProvider.Deleted,                AuditLogEventCategories.AuthProvider, AuditLogSeverities.Warning, "Authentication provider deleted.",            AuditLogEventScope.Application),
        new(AuditLogEventTypes.AuthProvider.NameUpdated,            AuditLogEventCategories.AuthProvider, AuditLogSeverities.Info,    "Authentication provider renamed.",            AuditLogEventScope.Application),
        new(AuditLogEventTypes.AuthProvider.Updated,                AuditLogEventCategories.AuthProvider, AuditLogSeverities.Warning, "Authentication provider configuration updated.", AuditLogEventScope.Application),
        new(AuditLogEventTypes.AuthProvider.Activated,              AuditLogEventCategories.AuthProvider, AuditLogSeverities.Info,    "Authentication provider activated.",          AuditLogEventScope.Application),
        new(AuditLogEventTypes.AuthProvider.Deactivated,            AuditLogEventCategories.AuthProvider, AuditLogSeverities.Warning, "Authentication provider deactivated.",        AuditLogEventScope.Application),
        new(AuditLogEventTypes.AuthProvider.PasswordLoginToggled,   AuditLogEventCategories.AuthProvider, AuditLogSeverities.Warning, "Password login for the provider toggled.",    AuditLogEventScope.Application),

        // ── Storage ──────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Storage.Created,         AuditLogEventCategories.Storage, AuditLogSeverities.Info,    "Storage created.",        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Storage.Deleted,         AuditLogEventCategories.Storage, AuditLogSeverities.Warning, "Storage deleted.",        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Storage.NameUpdated,     AuditLogEventCategories.Storage, AuditLogSeverities.Info,    "Storage renamed.",        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Storage.DetailsUpdated,  AuditLogEventCategories.Storage, AuditLogSeverities.Info,    "Storage details updated.", AuditLogEventScope.Application),
        new(AuditLogEventTypes.Storage.DefaultTrashPolicyUpdated, AuditLogEventCategories.Storage, AuditLogSeverities.Info, "Storage default trash policy updated.", AuditLogEventScope.Application),

        // ── Integrations ─────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Integration.Created,      AuditLogEventCategories.Integration, AuditLogSeverities.Info,    "Integration created.",     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Integration.Deleted,      AuditLogEventCategories.Integration, AuditLogSeverities.Warning, "Integration deleted.",     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Integration.NameUpdated,  AuditLogEventCategories.Integration, AuditLogSeverities.Info,    "Integration renamed.",     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Integration.Activated,    AuditLogEventCategories.Integration, AuditLogSeverities.Info,    "Integration activated.",   AuditLogEventScope.Application),
        new(AuditLogEventTypes.Integration.Deactivated,  AuditLogEventCategories.Integration, AuditLogSeverities.Warning, "Integration deactivated.", AuditLogEventScope.Application),

        // ── Agents ───────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Agent.Created,                  AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent created.",                          AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.Deleted,                  AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent deleted.",                          AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.TokenRotated,             AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent token rotated.",                    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.WorkspaceAccessGranted,   AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent granted access to a workspace.",    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.WorkspaceAccessRevoked,   AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent access to a workspace revoked.",    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.BoxAccessGranted,         AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent granted access to a box.",          AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.BoxAccessRevoked,         AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent access to a box revoked.",          AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.PermissionsAndRolesUpdated,            AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent permissions or roles updated.",                  AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.MaxWorkspaceNumberUpdated,             AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent max-workspace-number quota updated.",            AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.DefaultMaxWorkspaceSizeUpdated,        AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent default max-workspace-size quota updated.",      AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.DefaultMaxWorkspaceTeamMembersUpdated, AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent default max-workspace-team-members quota updated.", AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.StorageAccessUpdated,                  AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent storage access policy updated.",                 AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.ToolConfigUpdated,                     AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent tool configuration updated.",                    AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.ToolWorkspaceOverrideUpdated,          AuditLogEventCategories.Agent, AuditLogSeverities.Warning, "Agent per-workspace tool override updated.",           AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Agent.WorkspacesListed,                      AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent listed the workspaces it can access.",          AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.StoragesListed,                        AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent listed the storages it can use.",               AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.WorkspaceContentListed,                AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent listed the content of a workspace folder.",     AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.ShareLinksListed,                      AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent listed the share links of a workspace.",        AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.ShareLinkViewed,                       AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent viewed a share link.",                          AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.FileViewed,                            AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent viewed the details of a file.",                 AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.FileContentRead,                       AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent read the content of a file.",                   AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.FileDownloadLinkGenerated,             AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent generated a download link for a file.",         AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.BulkDownloadLinkGenerated,             AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent generated a bulk download link.",               AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.SearchPerformed,                       AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent searched files and folders.",                   AuditLogEventScope.Application),
        new(AuditLogEventTypes.Agent.FileCreated,                           AuditLogEventCategories.Agent, AuditLogSeverities.Info,    "Agent created a file.",                               AuditLogEventScope.Application),

        // ── Workspaces ───────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Workspace.Created,                          AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace created.",                                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.Deleted,                          AuditLogEventCategories.Workspace, AuditLogSeverities.Critical, "Workspace deleted.",                                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.NameUpdated,                      AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace renamed.",                                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.OwnerChanged,                     AuditLogEventCategories.Workspace, AuditLogSeverities.Warning,  "Workspace owner changed.",                          AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MaxSizeUpdated,                   AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace max-size quota updated.",                 AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MaxTeamMembersUpdated,            AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace max-team-members quota updated.",         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MemberInvited,                    AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace member invited.",                         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.AdminAssignedMember,              AuditLogEventCategories.Workspace, AuditLogSeverities.Warning,  "Workspace member assigned by admin.",               AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MemberRevoked,                    AuditLogEventCategories.Workspace, AuditLogSeverities.Warning,  "Workspace member revoked.",                         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MemberPermissionsUpdated,         AuditLogEventCategories.Workspace, AuditLogSeverities.Warning,  "Workspace member permissions updated.",             AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MemberEncryptionAccessGranted,    AuditLogEventCategories.Workspace, AuditLogSeverities.Warning,  "Workspace member granted encryption access.",       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.InvitationAccepted,               AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace invitation accepted.",                    AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.InvitationRejected,               AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace invitation rejected.",                    AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.MemberLeft,                       AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace member left.",                            AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.BulkDeleteRequested,              AuditLogEventCategories.Workspace, AuditLogSeverities.Critical, "Bulk-delete inside workspace requested.",           AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.TrashPolicyUpdated,               AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace trash policy updated.",                   AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.ImageDimensionsPolicyUpdated,     AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace image-dimensions extraction policy updated.", AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Workspace.ThumbnailsPolicyUpdated,          AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Workspace thumbnails generation policy updated.",   AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Trash.ItemsRestored,                        AuditLogEventCategories.Workspace, AuditLogSeverities.Info,     "Items restored from trash.",                        AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Trash.ItemsPermanentlyDeleted,              AuditLogEventCategories.Workspace, AuditLogSeverities.Critical, "Items permanently deleted from trash.",             AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Trash.Emptied,                              AuditLogEventCategories.Workspace, AuditLogSeverities.Critical, "Trash emptied.",                                    AuditLogEventScope.Workspace),

        // ── Folders ──────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Folder.Created,      AuditLogEventCategories.Folder, AuditLogSeverities.Info, "Folder created.",            AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Folder.BulkCreated,  AuditLogEventCategories.Folder, AuditLogSeverities.Info, "Folders bulk-created.",      AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Folder.NameUpdated,  AuditLogEventCategories.Folder, AuditLogSeverities.Info, "Folder renamed.",            AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Folder.ItemsMoved,   AuditLogEventCategories.Folder, AuditLogSeverities.Info, "Items moved across folders.", AuditLogEventScope.Workspace),

        // ── Boxes ────────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.Box.Created,                    AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box created.",                       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.Deleted,                    AuditLogEventCategories.Box, AuditLogSeverities.Warning, "Box deleted.",                       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.NameUpdated,                AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box renamed.",                       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.HeaderIsEnabledUpdated,     AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box header is-enabled toggled.",     AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.HeaderUpdated,              AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box header content updated.",        AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.FooterIsEnabledUpdated,     AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box footer is-enabled toggled.",     AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.FooterUpdated,              AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box footer content updated.",        AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.FolderUpdated,              AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box root folder updated.",           AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.IsEnabledUpdated,           AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box is-enabled toggled.",            AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.MemberInvited,              AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box member invited.",                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.MemberRevoked,              AuditLogEventCategories.Box, AuditLogSeverities.Warning, "Box member revoked.",                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.MemberPermissionsUpdated,   AuditLogEventCategories.Box, AuditLogSeverities.Warning, "Box member permissions updated.",    AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.LinkCreated,                AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box public link created.",           AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.InvitationAccepted,         AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box invitation accepted.",           AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.InvitationRejected,         AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box invitation rejected.",           AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.Box.MemberLeft,                 AuditLogEventCategories.Box, AuditLogSeverities.Info,    "Box member left.",                   AuditLogEventScope.Workspace),

        // ── Box links ────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.BoxLink.Deleted,                AuditLogEventCategories.BoxLink, AuditLogSeverities.Warning, "Box link deleted.",                    AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.BoxLink.NameUpdated,            AuditLogEventCategories.BoxLink, AuditLogSeverities.Info,    "Box link renamed.",                    AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.BoxLink.WidgetOriginsUpdated,   AuditLogEventCategories.BoxLink, AuditLogSeverities.Info,    "Box link widget origins updated.",     AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.BoxLink.IsEnabledUpdated,       AuditLogEventCategories.BoxLink, AuditLogSeverities.Info,    "Box link is-enabled toggled.",         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.BoxLink.PermissionsUpdated,     AuditLogEventCategories.BoxLink, AuditLogSeverities.Warning, "Box link permissions updated.",        AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.BoxLink.AccessCodeRegenerated,  AuditLogEventCategories.BoxLink, AuditLogSeverities.Warning, "Box link access code regenerated.",    AuditLogEventScope.Workspace),

        // ── Files ────────────────────────────────────────────────────────────────
        new(AuditLogEventTypes.File.Renamed,                   AuditLogEventCategories.File, AuditLogSeverities.Info,    "File renamed.",                         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.NoteSaved,                 AuditLogEventCategories.File, AuditLogSeverities.Info,    "File note saved.",                      AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.CommentCreated,            AuditLogEventCategories.File, AuditLogSeverities.Info,    "File comment created.",                 AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.CommentDeleted,            AuditLogEventCategories.File, AuditLogSeverities.Warning, "File comment deleted.",                 AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.CommentEdited,             AuditLogEventCategories.File, AuditLogSeverities.Info,    "File comment edited.",                  AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.ContentUpdated,            AuditLogEventCategories.File, AuditLogSeverities.Info,    "File content updated.",                 AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.AttachmentUploaded,        AuditLogEventCategories.File, AuditLogSeverities.Info,    "File attachment uploaded.",             AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.DownloadLinkGenerated,     AuditLogEventCategories.File, AuditLogSeverities.Info,    "File download link generated.",         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.BulkDownloadLinkGenerated, AuditLogEventCategories.File, AuditLogSeverities.Info,    "Bulk download link generated.",         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.Downloaded,                AuditLogEventCategories.File, AuditLogSeverities.Info,    "File downloaded.",                      AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.BulkDownloaded,            AuditLogEventCategories.File, AuditLogSeverities.Info,    "Files bulk-downloaded.",                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.UploadInitiated,           AuditLogEventCategories.File, AuditLogSeverities.Info,    "File upload initiated.",                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.UploadCompleted,           AuditLogEventCategories.File, AuditLogSeverities.Info,    "File upload completed.",                AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.File.MultiUploadCompleted,      AuditLogEventCategories.File, AuditLogSeverities.Info,    "Multi-file upload completed.",          AuditLogEventScope.Workspace),

        // ── Quick shares ─────────────────────────────────────────────────────────
        new(AuditLogEventTypes.QuickShare.Created,                   AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share created.",                       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.Deleted,                   AuditLogEventCategories.QuickShare, AuditLogSeverities.Warning, "Quick share deleted.",                       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.Updated,                   AuditLogEventCategories.QuickShare, AuditLogSeverities.Warning, "Quick share settings updated.",              AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.NameUpdated,               AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share renamed.",                       AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.SlugUpdated,               AuditLogEventCategories.QuickShare, AuditLogSeverities.Warning, "Quick share slug updated.",                  AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.ExpirationUpdated,         AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share expiration updated.",            AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.PasswordUpdated,           AuditLogEventCategories.QuickShare, AuditLogSeverities.Warning, "Quick share password updated.",              AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.MaxDownloadsUpdated,       AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share max-downloads limit updated.",   AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.ModeUpdated,               AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share mode updated.",                  AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.ItemsUpdated,              AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share items updated.",                 AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.Unlocked,                  AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share unlocked.",                      AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.UnlockFailed,              AuditLogEventCategories.QuickShare, AuditLogSeverities.Warning, "Quick share unlock attempt failed.",         AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.BulkDownloadLinkGenerated, AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share bulk download link generated.",  AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.FileDownloadLinkGenerated, AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share file download link generated.",  AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.FilePreviewLinkGenerated,  AuditLogEventCategories.QuickShare, AuditLogSeverities.Info,    "Quick share file preview link generated.",   AuditLogEventScope.Workspace),
        new(AuditLogEventTypes.QuickShare.DownloadLimitReached,      AuditLogEventCategories.QuickShare, AuditLogSeverities.Warning, "Quick share download limit reached.",        AuditLogEventScope.Workspace),
    ];

    public static readonly IReadOnlyDictionary<string, EventMetadata> ByEventType =
        All.ToDictionary(e => e.EventType);

    /// <summary>Event types whose policy lives in the app-level <c>audit-log-app-policy</c> setting.</summary>
    public static readonly IReadOnlySet<string> ApplicationScopedEventTypes =
        All.Where(e => e.Scope == AuditLogEventScope.Application)
            .Select(e => e.EventType)
            .ToHashSet();

    /// <summary>Event types whose policy lives in the per-workspace column (snapshotted from
    /// <c>audit-log-workspace-default-policy</c> at workspace creation).</summary>
    public static readonly IReadOnlySet<string> WorkspaceScopedEventTypes =
        All.Where(e => e.Scope == AuditLogEventScope.Workspace)
            .Select(e => e.EventType)
            .ToHashSet();
}
