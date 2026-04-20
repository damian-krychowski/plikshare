namespace PlikShare.AuditLog;

public static class AuditLogEventTypes
{
    public static class Auth
    {
        public const string SignedUp = "auth.signed-up";
        public const string SignUpFailed = "auth.sign-up-failed";
        public const string EmailConfirmed = "auth.email-confirmed";
        public const string EmailConfirmationFailed = "auth.email-confirmation-failed";
        public const string SignedIn = "auth.signed-in";
        public const string SignInFailed = "auth.sign-in-failed";
        public const string SignedIn2Fa = "auth.signed-in-2fa";
        public const string SignIn2FaFailed = "auth.sign-in-2fa-failed";
        public const string SignedOut = "auth.signed-out";
        public const string PasswordChanged = "auth.password-changed";
        public const string PasswordChangeFailed = "auth.password-change-failed";
        public const string PasswordResetRequested = "auth.password-reset-requested";
        public const string PasswordResetCompleted = "auth.password-reset-completed";
        public const string PasswordResetFailed = "auth.password-reset-failed";
        public const string TwoFaEnabled = "auth.2fa-enabled";
        public const string TwoFaEnableFailed = "auth.2fa-enable-failed";
        public const string TwoFaDisabled = "auth.2fa-disabled";
        public const string RecoveryCodesRegenerated = "auth.recovery-codes-regenerated";
        public const string SsoLogin = "auth.sso-login";
        public const string SsoUserCreated = "auth.sso-user-created";

        public static readonly string[] All =
        [
            SignedUp, SignUpFailed,
            EmailConfirmed, EmailConfirmationFailed,
            SignedIn, SignInFailed,
            SignedIn2Fa, SignIn2FaFailed,
            SignedOut,
            PasswordChanged, PasswordChangeFailed,
            PasswordResetRequested, PasswordResetCompleted, PasswordResetFailed,
            TwoFaEnabled, TwoFaEnableFailed, TwoFaDisabled,
            RecoveryCodesRegenerated,
            SsoLogin, SsoUserCreated
        ];
    }

    public static class User
    {
        public const string Invited = "user.invited";
        public const string Deleted = "user.deleted";
        public const string PermissionsAndRolesUpdated = "user.permissions-and-roles-updated";
        public const string MaxWorkspaceNumberUpdated = "user.max-workspace-number-updated";
        public const string DefaultMaxWorkspaceSizeUpdated = "user.default-max-workspace-size-updated";
        public const string DefaultMaxWorkspaceTeamMembersUpdated = "user.default-max-workspace-team-members-updated";

        public static readonly string[] All =
        [
            Invited, Deleted,
            PermissionsAndRolesUpdated,
            MaxWorkspaceNumberUpdated,
            DefaultMaxWorkspaceSizeUpdated,
            DefaultMaxWorkspaceTeamMembersUpdated
        ];
    }

    public static class Settings
    {
        public const string AppNameChanged = "settings.app-name-changed";
        public const string SignUpOptionChanged = "settings.sign-up-option-changed";
        public const string DefaultPermissionsChanged = "settings.default-permissions-changed";
        public const string DefaultMaxWorkspaceNumberChanged = "settings.default-max-workspace-number-changed";
        public const string DefaultMaxWorkspaceSizeChanged = "settings.default-max-workspace-size-changed";
        public const string DefaultMaxWorkspaceTeamMembersChanged = "settings.default-max-workspace-team-members-changed";
        public const string AlertOnNewUserChanged = "settings.alert-on-new-user-changed";
        public const string TermsOfServiceUploaded = "settings.terms-of-service-uploaded";
        public const string TermsOfServiceDeleted = "settings.terms-of-service-deleted";
        public const string PrivacyPolicyUploaded = "settings.privacy-policy-uploaded";
        public const string PrivacyPolicyDeleted = "settings.privacy-policy-deleted";
        public const string SignUpCheckboxCreatedOrUpdated = "settings.sign-up-checkbox-created-or-updated";
        public const string SignUpCheckboxDeleted = "settings.sign-up-checkbox-deleted";
        public const string PasswordLoginToggled = "settings.password-login-toggled";

        public static readonly string[] All =
        [
            AppNameChanged, SignUpOptionChanged,
            DefaultPermissionsChanged,
            DefaultMaxWorkspaceNumberChanged, DefaultMaxWorkspaceSizeChanged, DefaultMaxWorkspaceTeamMembersChanged,
            AlertOnNewUserChanged,
            TermsOfServiceUploaded, TermsOfServiceDeleted,
            PrivacyPolicyUploaded, PrivacyPolicyDeleted,
            SignUpCheckboxCreatedOrUpdated, SignUpCheckboxDeleted,
            PasswordLoginToggled
        ];
    }

    public static class EmailProvider
    {
        public const string Created = "email-provider.created";
        public const string Deleted = "email-provider.deleted";
        public const string NameUpdated = "email-provider.name-updated";
        public const string Activated = "email-provider.activated";
        public const string Deactivated = "email-provider.deactivated";
        public const string Confirmed = "email-provider.confirmed";
        public const string ConfirmationEmailResent = "email-provider.confirmation-email-resent";

        public static readonly string[] All =
        [
            Created, Deleted, NameUpdated,
            Activated, Deactivated,
            Confirmed, ConfirmationEmailResent
        ];
    }

    public static class AuthProvider
    {
        public const string Created = "auth-provider.created";
        public const string Deleted = "auth-provider.deleted";
        public const string NameUpdated = "auth-provider.name-updated";
        public const string Updated = "auth-provider.updated";
        public const string Activated = "auth-provider.activated";
        public const string Deactivated = "auth-provider.deactivated";
        public const string PasswordLoginToggled = "auth-provider.password-login-toggled";

        public static readonly string[] All =
        [
            Created, Deleted, NameUpdated, Updated,
            Activated, Deactivated,
            PasswordLoginToggled
        ];
    }

    public static class Workspace
    {
        public const string Created = "workspace.created";
        public const string Deleted = "workspace.deleted";
        public const string NameUpdated = "workspace.name-updated";
        public const string OwnerChanged = "workspace.owner-changed";
        public const string MaxSizeUpdated = "workspace.max-size-updated";
        public const string MaxTeamMembersUpdated = "workspace.max-team-members-updated";
        public const string MemberInvited = "workspace.member-invited";
        public const string MemberRevoked = "workspace.member-revoked";
        public const string MemberPermissionsUpdated = "workspace.member-permissions-updated";
        public const string MemberEncryptionAccessGranted = "workspace.member-encryption-access-granted";
        public const string InvitationAccepted = "workspace.invitation-accepted";
        public const string InvitationRejected = "workspace.invitation-rejected";
        public const string MemberLeft = "workspace.member-left";
        public const string BulkDeleteRequested = "workspace.bulk-delete-requested";

        public static readonly string[] All =
        [
            Created, Deleted, NameUpdated,
            OwnerChanged, MaxSizeUpdated, MaxTeamMembersUpdated,
            MemberInvited, MemberRevoked, MemberPermissionsUpdated,
            MemberEncryptionAccessGranted,
            InvitationAccepted, InvitationRejected, MemberLeft,
            BulkDeleteRequested
        ];
    }

    public static class Storage
    {
        public const string Created = "storage.created";
        public const string Deleted = "storage.deleted";
        public const string NameUpdated = "storage.name-updated";
        public const string DetailsUpdated = "storage.details-updated";

        public static readonly string[] All =
        [
            Created, Deleted, NameUpdated, DetailsUpdated
        ];
    }

    public static class Integration
    {
        public const string Created = "integration.created";
        public const string Deleted = "integration.deleted";
        public const string NameUpdated = "integration.name-updated";
        public const string Activated = "integration.activated";
        public const string Deactivated = "integration.deactivated";

        public static readonly string[] All =
        [
            Created, Deleted, NameUpdated,
            Activated, Deactivated
        ];
    }

    public static class Folder
    {
        public const string Created = "folder.created";
        public const string BulkCreated = "folder.bulk-created";
        public const string NameUpdated = "folder.name-updated";
        public const string ItemsMoved = "folder.items-moved";

        public static readonly string[] All =
        [
            Created, BulkCreated, NameUpdated, ItemsMoved
        ];
    }

    public static class Box
    {
        public const string Created = "box.created";
        public const string Deleted = "box.deleted";
        public const string NameUpdated = "box.name-updated";
        public const string HeaderIsEnabledUpdated = "box.header-is-enabled-updated";
        public const string HeaderUpdated = "box.header-updated";
        public const string FooterIsEnabledUpdated = "box.footer-is-enabled-updated";
        public const string FooterUpdated = "box.footer-updated";
        public const string FolderUpdated = "box.folder-updated";
        public const string IsEnabledUpdated = "box.is-enabled-updated";
        public const string MemberInvited = "box.member-invited";
        public const string MemberRevoked = "box.member-revoked";
        public const string MemberPermissionsUpdated = "box.member-permissions-updated";
        public const string LinkCreated = "box.link-created";
        public const string InvitationAccepted = "box.invitation-accepted";
        public const string InvitationRejected = "box.invitation-rejected";
        public const string MemberLeft = "box.member-left";

        public static readonly string[] All =
        [
            Created, Deleted, NameUpdated,
            HeaderIsEnabledUpdated, HeaderUpdated,
            FooterIsEnabledUpdated, FooterUpdated,
            FolderUpdated, IsEnabledUpdated,
            MemberInvited, MemberRevoked, MemberPermissionsUpdated,
            LinkCreated,
            InvitationAccepted, InvitationRejected, MemberLeft
        ];
    }

    public static class BoxLink
    {
        public const string Deleted = "box-link.deleted";
        public const string NameUpdated = "box-link.name-updated";
        public const string WidgetOriginsUpdated = "box-link.widget-origins-updated";
        public const string IsEnabledUpdated = "box-link.is-enabled-updated";
        public const string PermissionsUpdated = "box-link.permissions-updated";
        public const string AccessCodeRegenerated = "box-link.access-code-regenerated";

        public static readonly string[] All =
        [
            Deleted, NameUpdated, WidgetOriginsUpdated,
            IsEnabledUpdated, PermissionsUpdated, AccessCodeRegenerated
        ];
    }

    public static class File
    {
        public const string Renamed = "file.renamed";
        public const string NoteSaved = "file.note-saved";
        public const string CommentCreated = "file.comment-created";
        public const string CommentDeleted = "file.comment-deleted";
        public const string CommentEdited = "file.comment-edited";
        public const string ContentUpdated = "file.content-updated";
        public const string AttachmentUploaded = "file.attachment-uploaded";
        public const string DownloadLinkGenerated = "file.download-link-generated";
        public const string BulkDownloadLinkGenerated = "file.bulk-download-link-generated";
        public const string Downloaded = "file.downloaded";
        public const string BulkDownloaded = "file.bulk-downloaded";
        public const string UploadInitiated = "file.upload-initiated";
        public const string UploadCompleted = "file.upload-completed";
        public const string MultiUploadCompleted = "file.multi-upload-completed";

        public static readonly string[] All =
        [
            Renamed, NoteSaved,
            CommentCreated, CommentDeleted, CommentEdited,
            ContentUpdated, AttachmentUploaded,
            DownloadLinkGenerated, BulkDownloadLinkGenerated,
            Downloaded, BulkDownloaded,
            UploadInitiated, UploadCompleted, MultiUploadCompleted
        ];
    }

    public static class Upload
    {
        public static readonly string[] All = [];
    }

    public static readonly string[] All = [
        ..Auth.All, ..User.All, ..Settings.All,
        ..EmailProvider.All, ..AuthProvider.All,
        ..Storage.All, ..Integration.All,
        ..Workspace.All, ..Folder.All, ..Box.All, ..BoxLink.All,
        ..File.All, ..Upload.All
    ];
}

public static class AuditLogEventCategories
{
    public const string Auth = "auth";
    public const string User = "user";
    public const string Workspace = "workspace";
    public const string File = "file";
    public const string Folder = "folder";
    public const string Box = "box";
    public const string BoxLink = "box-link";
    public const string Storage = "storage";
    public const string Integration = "integration";
    public const string EmailProvider = "email-provider";
    public const string AuthProvider = "auth-provider";
    public const string Settings = "settings";
    public const string Upload = "upload";

    public static readonly string[] All =
    [
        Auth, User, Workspace, File, Folder,
        Box, BoxLink, Storage, Integration,
        EmailProvider, AuthProvider, Settings, Upload
    ];
}

public static class AuditLogFailureReasons
{
    public static class User
    {
        public const string UserNotFound = "user-not-found";
        public const string CannotDeleteAppOwner = "cannot-delete-app-owner";
        public const string CannotModifyOwnPermissions = "cannot-modify-own-permissions";
    }

    public static class Auth
    {
        public const string PasswordLoginDisabled = "password-login-disabled";
        public const string CheckboxesMissing = "checkboxes-missing";
        public const string InvitationRequired = "invitation-required";
        public const string WrongInvitationCode = "wrong-invitation-code";
        public const string DuplicateEmail = "duplicate-email";
        public const string UserNotFound = "user-not-found";
        public const string InvalidToken = "invalid-token";
        public const string InvalidCredentials = "invalid-credentials";
        public const string LockedOut = "locked-out";
        public const string No2FaSession = "no-2fa-session";
        public const string InvalidVerificationCode = "invalid-verification-code";
        public const string InvalidRecoveryCode = "invalid-recovery-code";
        public const string PasswordMismatch = "password-mismatch";
        public const string Failed = "failed";
    }
}

public static class AuditLogSignInMethods
{
    public const string Password = "password";
    public const string Authenticator = "authenticator";
    public const string RecoveryCode = "recovery-code";
}

public static class AuditLogSeverities
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Critical = "critical";

    public static readonly string[] All = [Info, Warning, Critical];
}
