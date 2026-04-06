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

    public static readonly string[] All = [..Auth.All, ..User.All, ..Settings.All, ..EmailProvider.All, ..AuthProvider.All];
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

    public static readonly string[] All =
    [
        Auth, User, Workspace, File, Folder,
        Box, BoxLink, Storage, Integration,
        EmailProvider, AuthProvider, Settings
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
