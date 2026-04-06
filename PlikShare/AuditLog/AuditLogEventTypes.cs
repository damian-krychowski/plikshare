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

    public static readonly string[] All = [..Auth.All, ..User.All];
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
