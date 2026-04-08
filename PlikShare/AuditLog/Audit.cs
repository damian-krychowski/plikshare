using PlikShare.AuthProviders.Id;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.Id;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Id;
using PlikShare.Users.Id;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog;

public static class Audit
{
    public static class Auth
    {
        public static AuditLogEntry SignedUp(
            AuditLogActorContext actor,
            string email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedUp,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry SignUpFailed(
            string? actorIp,
            Guid correlationId,
            string attemptedEmail,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = attemptedEmail,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignUpFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry EmailConfirmed(
            AuditLogActorContext actor,
            string email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.EmailConfirmed,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry EmailConfirmationFailed(
            string? actorIp,
            Guid correlationId,
            string? email,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.EmailConfirmationFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry SignedIn(
            AuditLogActorContext actor,
            string email,
            string method) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.SignedIn {
                Method = method })
        };

        public static AuditLogEntry SignInFailed(
            string? actorIp,
            Guid correlationId,
            string attemptedEmail,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = attemptedEmail,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignInFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry SignedIn2Fa(
            AuditLogActorContext actor,
            string? email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.SignedIn {
                Method = AuditLogSignInMethods.Authenticator })
        };

        public static AuditLogEntry SignedInRecoveryCode(
            AuditLogActorContext actor,
            string? email) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedIn2Fa,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.SignedIn {
                Method = AuditLogSignInMethods.RecoveryCode })
        };

        public static AuditLogEntry SignIn2FaFailed(
            string? actorIp,
            Guid correlationId,
            string reason,
            string? email) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignIn2FaFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry SignedOut(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SignedOut,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordChanged(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordChanged,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordChangeFailed(
            AuditLogActorContext actor,
            string reason) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordChangeFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry PasswordResetRequested(
            string? actorIp,
            Guid correlationId,
            string email) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetRequested,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordResetCompleted(
            string? actorIp,
            Guid correlationId,
            string? email) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetCompleted,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PasswordResetFailed(
            string? actorIp,
            Guid correlationId,
            string? email,
            string reason) => new()
        {
            Actor = AnonymousIdentity.Instance,
            ActorEmail = email,
            ActorIp = actorIp,
            CorrelationId = correlationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.PasswordResetFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry TwoFaEnabled(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaEnabled,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry TwoFaEnableFailed(
            AuditLogActorContext actor,
            string reason) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaEnableFailed,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Failed {
                Reason = reason })
        };

        public static AuditLogEntry TwoFaDisabled(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.TwoFaDisabled,
            Severity = AuditLogSeverities.Critical
        };

        public static AuditLogEntry RecoveryCodesRegenerated(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.RecoveryCodesRegenerated,
            Severity = AuditLogSeverities.Warning
        };

        public static AuditLogEntry SsoLogin(
            AuditLogActorContext actor,
            string email,
            string providerName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SsoLogin,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Sso {
                ProviderName = providerName })
        };

        public static AuditLogEntry SsoUserCreated(
            AuditLogActorContext actor,
            string email,
            string providerName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Auth,
            EventType = AuditLogEventTypes.Auth.SsoUserCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Auth.Sso {
                ProviderName = providerName })
        };
    }

    public static class User
    {
        public static AuditLogEntry Invited(
            AuditLogActorContext actor,
            List<string> emails) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.Invited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.Invited {
                Emails = emails })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            string targetEmail) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.Deleted,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.Deleted {
                TargetEmail = targetEmail })
        };

        public static AuditLogEntry PermissionsAndRolesUpdated(
            AuditLogActorContext actor,
            string targetEmail,
            UserPermissionsAndRolesDto request) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.PermissionsAndRolesUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.PermissionsAndRolesUpdated
            {
                TargetEmail = targetEmail,
                IsAdmin = request.IsAdmin,
                Permissions = request.GetPermissionsList()
            })
        };

        public static AuditLogEntry MaxWorkspaceNumberUpdated(
            AuditLogActorContext actor,
            string targetEmail,
            UserExtId targetExternalId,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.MaxWorkspaceNumberUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.LimitUpdated {
                TargetEmail = targetEmail,
                TargetExternalId = targetExternalId,
                Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceSizeUpdated(
            AuditLogActorContext actor,
            string targetEmail,
            UserExtId targetExternalId,
            long? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.DefaultMaxWorkspaceSizeUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.LimitUpdated {
                TargetEmail = targetEmail,
                TargetExternalId = targetExternalId,
                Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersUpdated(
            AuditLogActorContext actor,
            string targetEmail,
            UserExtId targetExternalId,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.DefaultMaxWorkspaceTeamMembersUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.LimitUpdated {
                TargetEmail = targetEmail,
                TargetExternalId = targetExternalId,
                Value = value })
        };
    }

    public static class Settings
    {
        public static AuditLogEntry AppNameChanged(
            AuditLogActorContext actor,
            string? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.AppNameChanged,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.ValueChanged {
                Value = value })
        };

        public static AuditLogEntry SignUpOptionChanged(
            AuditLogActorContext actor,
            string value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.SignUpOptionChanged,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.ValueChanged {
                Value = value })
        };

        public static AuditLogEntry DefaultPermissionsChanged(
            AuditLogActorContext actor,
            UserPermissionsAndRolesDto request) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.DefaultPermissionsChanged,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.DefaultPermissionsChanged
            {
                IsAdmin = request.IsAdmin,
                Permissions = request.GetPermissionsList()
            })
        };

        public static AuditLogEntry DefaultMaxWorkspaceNumberChanged(
            AuditLogActorContext actor,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.DefaultMaxWorkspaceNumberChanged,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.ValueChanged {
                Value = value?.ToString() })
        };

        public static AuditLogEntry DefaultMaxWorkspaceSizeChanged(
            AuditLogActorContext actor,
            long? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.DefaultMaxWorkspaceSizeChanged,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.ValueChanged {
                Value = value?.ToString() })
        };

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersChanged(
            AuditLogActorContext actor,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.DefaultMaxWorkspaceTeamMembersChanged,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.ValueChanged {
                Value = value?.ToString() })
        };

        public static AuditLogEntry AlertOnNewUserChanged(
            AuditLogActorContext actor,
            bool value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.AlertOnNewUserChanged,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.ToggleChanged {
                Value = value })
        };

        public static AuditLogEntry TermsOfServiceUploaded(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.TermsOfServiceUploaded,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry TermsOfServiceDeleted(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.TermsOfServiceDeleted,
            Severity = AuditLogSeverities.Warning
        };

        public static AuditLogEntry PrivacyPolicyUploaded(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.PrivacyPolicyUploaded,
            Severity = AuditLogSeverities.Info
        };

        public static AuditLogEntry PrivacyPolicyDeleted(
            AuditLogActorContext actor) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.PrivacyPolicyDeleted,
            Severity = AuditLogSeverities.Warning
        };

        public static AuditLogEntry SignUpCheckboxCreatedOrUpdated(
            AuditLogActorContext actor,
            int? id,
            string text,
            bool isRequired) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.SignUpCheckboxCreatedOrUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.SignUpCheckbox {
                Id = id,
                Text = text,
                IsRequired = isRequired })
        };

        public static AuditLogEntry SignUpCheckboxDeleted(
            AuditLogActorContext actor,
            int id) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Settings,
            EventType = AuditLogEventTypes.Settings.SignUpCheckboxDeleted,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Settings.SignUpCheckbox {
                Id = id,
                Text = "",
                IsRequired = false })
        };
    }

    public static class EmailProvider
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            string name,
            string type,
            string emailFrom) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.Created {
                Name = name,
                Type = type,
                EmailFrom = emailFrom })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            EmailProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.Deleted {
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            EmailProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.NameUpdated {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Activated(
            AuditLogActorContext actor,
            EmailProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ActivationChanged {
                ExternalId = externalId })
        };

        public static AuditLogEntry Deactivated(
            AuditLogActorContext actor,
            EmailProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ActivationChanged {
                ExternalId = externalId })
        };

        public static AuditLogEntry Confirmed(
            AuditLogActorContext actor,
            EmailProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Confirmed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ActivationChanged {
                ExternalId = externalId })
        };

        public static AuditLogEntry ConfirmationEmailResent(
            AuditLogActorContext actor,
            EmailProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.ConfirmationEmailResent,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ConfirmationEmailResent {
                ExternalId = externalId })
        };
    }

    public static class Workspace
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.Created {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            WorkspaceExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.Deleted,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.Deleted {
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.NameUpdated {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry OwnerChanged(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            string newOwnerEmail) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.OwnerChanged,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.OwnerChanged {
                ExternalId = externalId,
                NewOwnerEmail = newOwnerEmail })
        };

        public static AuditLogEntry MaxSizeUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            long? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MaxSizeUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MaxSizeUpdated {
                ExternalId = externalId,
                Value = value })
        };

        public static AuditLogEntry MaxTeamMembersUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MaxTeamMembersUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MaxTeamMembersUpdated {
                ExternalId = externalId,
                Value = value })
        };

        public static AuditLogEntry MemberInvited(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            List<string> memberEmails) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberInvited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberInvited {
                ExternalId = externalId,
                MemberEmails = memberEmails })
        };

        public static AuditLogEntry MemberRevoked(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            string memberEmail) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberRevoked,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberRevoked {
                ExternalId = externalId,
                MemberEmail = memberEmail })
        };

        public static AuditLogEntry MemberPermissionsUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            string memberEmail,
            bool allowShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberPermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberPermissionsUpdated {
                ExternalId = externalId,
                MemberEmail = memberEmail,
                AllowShare = allowShare })
        };

        public static AuditLogEntry InvitationAccepted(
            AuditLogActorContext actor,
            WorkspaceExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.InvitationAccepted,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.InvitationResponse {
                ExternalId = externalId })
        };

        public static AuditLogEntry InvitationRejected(
            AuditLogActorContext actor,
            WorkspaceExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.InvitationRejected,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.InvitationResponse {
                ExternalId = externalId })
        };

        public static AuditLogEntry MemberLeft(
            AuditLogActorContext actor,
            WorkspaceExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberLeft,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberLeft {
                ExternalId = externalId })
        };

        public static AuditLogEntry BulkDeleteRequested(
            AuditLogActorContext actor,
            WorkspaceExtId externalId,
            List<FileExtId> fileExternalIds,
            List<FolderExtId> folderExternalIds,
            List<FileUploadExtId> fileUploadExternalIds) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.BulkDeleteRequested,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.BulkDeleteRequested {
                ExternalId = externalId,
                FileExternalIds = fileExternalIds,
                FolderExternalIds = folderExternalIds,
                FileUploadExternalIds = fileUploadExternalIds })
        };
    }

    public static class Folder
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FolderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.Created {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry BulkCreated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            List<FolderExtId> folderExternalIds) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.BulkCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.BulkCreated {
                WorkspaceExternalId = workspaceExternalId,
                FolderExternalIds = folderExternalIds })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FolderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.NameUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry ItemsMoved(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FolderExtId? destinationFolderExternalId,
            FolderExtId[] folderExternalIds,
            FileExtId[] fileExternalIds,
            FileUploadExtId[] fileUploadExternalIds) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.ItemsMoved,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.ItemsMoved {
                WorkspaceExternalId = workspaceExternalId,
                DestinationFolderExternalId = destinationFolderExternalId,
                FolderExternalIds = folderExternalIds.ToList(),
                FileExternalIds = fileExternalIds.ToList(),
                FileUploadExternalIds = fileUploadExternalIds.ToList() })
        };
    }

    public static class Box
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            string name,
            FolderExtId folderExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.Created {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                Name = name,
                FolderExternalId = folderExternalId })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.Deleted {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.NameUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry HeaderIsEnabledUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.HeaderIsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.HeaderIsEnabledUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry HeaderUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.HeaderUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.HeaderUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry FooterIsEnabledUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FooterIsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.FooterIsEnabledUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry FooterUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FooterUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.FooterUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry FolderUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            FolderExtId folderExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FolderUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.FolderUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                FolderExternalId = folderExternalId })
        };

        public static AuditLogEntry IsEnabledUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.IsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.IsEnabledUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry MemberInvited(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            List<string> memberEmails) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberInvited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberInvited {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                MemberEmails = memberEmails })
        };

        public static AuditLogEntry MemberRevoked(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            string memberEmail) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberRevoked,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberRevoked {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                MemberEmail = memberEmail })
        };

        public static AuditLogEntry MemberPermissionsUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId externalId,
            string memberEmail,
            BoxPermissions permissions) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberPermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberPermissionsUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                MemberEmail = memberEmail,
                Permissions = permissions })
        };

        public static AuditLogEntry LinkCreated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.LinkCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.LinkCreated {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry InvitationAccepted(
            AuditLogActorContext actor,
            BoxExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.InvitationAccepted,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.InvitationAccepted {
                ExternalId = externalId })
        };

        public static AuditLogEntry InvitationRejected(
            AuditLogActorContext actor,
            BoxExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.InvitationRejected,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.InvitationRejected {
                ExternalId = externalId })
        };

        public static AuditLogEntry MemberLeft(
            AuditLogActorContext actor,
            BoxExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberLeft,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberLeft {
                ExternalId = externalId })
        };
    }

    public static class BoxLink
    {
        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.Deleted {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.NameUpdated {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry WidgetOriginsUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId,
            List<string> widgetOrigins) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.WidgetOriginsUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.WidgetOriginsUpdated {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId,
                WidgetOrigins = widgetOrigins })
        };

        public static AuditLogEntry IsEnabledUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.IsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.IsEnabledUpdated {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry PermissionsUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId,
            BoxPermissions permissions) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.PermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.PermissionsUpdated {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId,
                Permissions = permissions })
        };

        public static AuditLogEntry AccessCodeRegenerated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            BoxExtId boxExternalId,
            BoxLinkExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.AccessCodeRegenerated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.AccessCodeRegenerated {
                WorkspaceExternalId = workspaceExternalId,
                BoxExternalId = boxExternalId,
                ExternalId = externalId })
        };
    }

    public static class File
    {
        public static AuditLogEntry Renamed(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.Renamed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.Renamed {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry NoteSaved(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.NoteSaved,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.NoteSaved {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry CommentCreated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId fileExternalId,
            FileArtifactExtId commentExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentCreated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.CommentCreated {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId,
                CommentExternalId = commentExternalId })
        };

        public static AuditLogEntry CommentDeleted(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId fileExternalId,
            FileArtifactExtId commentExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentDeleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.CommentDeleted {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId,
                CommentExternalId = commentExternalId })
        };

        public static AuditLogEntry CommentEdited(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId fileExternalId,
            FileArtifactExtId commentExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentEdited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.CommentEdited {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalId = fileExternalId,
                CommentExternalId = commentExternalId })
        };

        public static AuditLogEntry ContentUpdated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.ContentUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.ContentUpdated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry AttachmentUploaded(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId parentFileExternalId,
            FileExtId attachmentFileExternalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.AttachmentUploaded,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.AttachmentUploaded {
                WorkspaceExternalId = workspaceExternalId,
                ParentFileExternalId = parentFileExternalId,
                AttachmentFileExternalId = attachmentFileExternalId,
                Name = name })
        };

        public static AuditLogEntry DownloadLinkGenerated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.DownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.DownloadLinkGenerated {
                WorkspaceExternalId = workspaceExternalId,
                ExternalId = externalId })
        };

        public static AuditLogEntry BulkDownloadLinkGenerated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            List<FileExtId> selectedFileExternalIds,
            List<FolderExtId> selectedFolderExternalIds) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.BulkDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.BulkDownloadLinkGenerated {
                WorkspaceExternalId = workspaceExternalId,
                SelectedFileExternalIds = selectedFileExternalIds,
                SelectedFolderExternalIds = selectedFolderExternalIds })
        };

        public static AuditLogEntry Downloaded(
            AuditLogActorContext actor,
            FileExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.Downloaded,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.Downloaded {
                ExternalId = externalId })
        };

        public static AuditLogEntry BulkDownloaded(
            AuditLogActorContext actor,
            List<FileExtId> fileExternalIds) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.BulkDownloaded,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.BulkDownloaded {
                FileExternalIds = fileExternalIds })
        };
    }

    public static class Upload
    {
        public static AuditLogEntry BulkInitiated(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            List<string> fileNames) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Upload,
            EventType = AuditLogEventTypes.Upload.BulkInitiated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Upload.BulkInitiated {
                WorkspaceExternalId = workspaceExternalId,
                FileNames = fileNames })
        };

        public static AuditLogEntry Completed(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            FileUploadExtId fileUploadExternalId,
            FileExtId fileExternalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Upload,
            EventType = AuditLogEventTypes.Upload.Completed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Upload.Completed {
                WorkspaceExternalId = workspaceExternalId,
                FileUploadExternalId = fileUploadExternalId,
                FileExternalId = fileExternalId })
        };

        public static AuditLogEntry FilePartUploaded(
            AuditLogActorContext actor,
            FileUploadExtId fileUploadExternalId,
            int partNumber) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Upload,
            EventType = AuditLogEventTypes.Upload.FilePartUploaded,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Upload.FilePartUploaded {
                FileUploadExternalId = fileUploadExternalId,
                PartNumber = partNumber })
        };

        public static AuditLogEntry MultiFileDirectUploaded(
            AuditLogActorContext actor,
            WorkspaceExtId workspaceExternalId,
            List<FileExtId> fileExternalIds) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Upload,
            EventType = AuditLogEventTypes.Upload.MultiFileDirectUploaded,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Upload.MultiFileDirectUploaded {
                WorkspaceExternalId = workspaceExternalId,
                FileExternalIds = fileExternalIds })
        };
    }

    public static class Storage
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            string name,
            string type) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.Created {
                Name = name,
                Type = type })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            StorageExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.Deleted {
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            StorageExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.NameUpdated {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry DetailsUpdated(
            AuditLogActorContext actor,
            StorageExtId externalId,
            string type) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.DetailsUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.DetailsUpdated {
                ExternalId = externalId,
                Type = type })
        };
    }

    public static class Integration
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            string name,
            string type) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.Created {
                Name = name,
                Type = type })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            IntegrationExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.Deleted {
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            IntegrationExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.NameUpdated {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Activated(
            AuditLogActorContext actor,
            IntegrationExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.ActivationChanged {
                ExternalId = externalId })
        };

        public static AuditLogEntry Deactivated(
            AuditLogActorContext actor,
            IntegrationExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.ActivationChanged {
                ExternalId = externalId })
        };
    }

    public static class AuthProvider
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            string name,
            string type) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.Created {
                Name = name,
                Type = type })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            AuthProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.Deleted {
                ExternalId = externalId })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            AuthProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.NameUpdated {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Updated(
            AuditLogActorContext actor,
            AuthProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Updated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.Updated {
                ExternalId = externalId, Name = name })
        };

        public static AuditLogEntry Activated(
            AuditLogActorContext actor,
            AuthProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.ActivationChanged {
                ExternalId = externalId })
        };

        public static AuditLogEntry Deactivated(
            AuditLogActorContext actor,
            AuthProviderExtId externalId) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.ActivationChanged {
                ExternalId = externalId })
        };

        public static AuditLogEntry PasswordLoginToggled(
            AuditLogActorContext actor,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.AuthProvider,
            EventType = AuditLogEventTypes.AuthProvider.PasswordLoginToggled,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.AuthProvider.PasswordLoginToggled {
                IsEnabled = isEnabled })
        };
    }
}
