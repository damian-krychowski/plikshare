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
using PlikShare.Uploads.Id;
using PlikShare.Users.Id;
using PlikShare.Users.PermissionsAndRoles;
using PlikShare.Workspaces.Id;
using Serilog;

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
            List<AuditLogDetails.UserRef> users) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.Invited,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.Invited {
                Users = users })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            AuditLogDetails.UserRef target) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.User,
            EventType = AuditLogEventTypes.User.Deleted,
            Severity = AuditLogSeverities.Critical,
            DetailsJson = Json.Serialize(new AuditLogDetails.User.Deleted {
                Target = target })
        };

        public static AuditLogEntry PermissionsAndRolesUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.UserRef target,
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
                Target = target,
                IsAdmin = request.IsAdmin,
                Permissions = request.GetPermissionsList()
            })
        };

        public static AuditLogEntry MaxWorkspaceNumberUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.UserRef target,
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
                Target = target,
                Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceSizeUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.UserRef target,
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
                Target = target,
                Value = value })
        };

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.UserRef target,
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
                Target = target,
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
            EmailProviderExtId externalId,
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
                ExternalId = externalId,
                Name = name,
                Type = type,
                EmailFrom = emailFrom })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            EmailProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.Deleted {
                ExternalId = externalId,
                Name = name })
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
            EmailProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ActivationChanged {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Deactivated(
            AuditLogActorContext actor,
            EmailProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ActivationChanged {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Confirmed(
            AuditLogActorContext actor,
            EmailProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.Confirmed,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ActivationChanged {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry ConfirmationEmailResent(
            AuditLogActorContext actor,
            EmailProviderExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.EmailProvider,
            EventType = AuditLogEventTypes.EmailProvider.ConfirmationEmailResent,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.EmailProvider.ConfirmationEmailResent {
                ExternalId = externalId,
                Name = name })
        };
    }

    public static class Workspace
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            long? maxSizeInBytes,
            string bucketName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.Created {
                Storage = storage,
                Workspace = workspace,
                MaxSizeInBytes = maxSizeInBytes,
                BucketName = bucketName })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.Deleted,
            Severity = AuditLogSeverities.Critical,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.Deleted {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.NameUpdated {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry OwnerChanged(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.UserRef newOwner) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.OwnerChanged,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.OwnerChanged {
                Storage = storage,
                Workspace = workspace,
                NewOwner = newOwner })
        };

        public static AuditLogEntry MaxSizeUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            long? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MaxSizeUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MaxSizeUpdated {
                Storage = storage,
                Workspace = workspace,
                Value = value })
        };

        public static AuditLogEntry MaxTeamMembersUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            int? value) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MaxTeamMembersUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MaxTeamMembersUpdated {
                Storage = storage,
                Workspace = workspace,
                Value = value })
        };

        public static AuditLogEntry MemberInvited(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            List<AuditLogDetails.UserRef> members) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberInvited,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberInvited {
                Storage = storage,
                Workspace = workspace,
                Members = members })
        };

        public static AuditLogEntry MemberRevoked(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.UserRef member) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberRevoked,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberRevoked {
                Storage = storage,
                Workspace = workspace,
                Member = member })
        };

        public static AuditLogEntry MemberPermissionsUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.UserRef member,
            bool allowShare) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberPermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberPermissionsUpdated {
                Storage = storage,
                Workspace = workspace,
                Member = member,
                AllowShare = allowShare })
        };

        public static AuditLogEntry InvitationAccepted(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.InvitationAccepted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.InvitationResponse {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry InvitationRejected(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.InvitationRejected,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.InvitationResponse {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry MemberLeft(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.MemberLeft,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.MemberLeft {
                Storage = storage,
                Workspace = workspace })
        };

        public static AuditLogEntry BulkDeleteRequested(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage,
            AuditLogDetails.WorkspaceRef workspace,
            List<AuditLogDetails.FileRef> files,
            List<AuditLogDetails.FolderRef> folders,
            List<AuditLogDetails.FileUploadRef> fileUploads) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Workspace,
            EventType = AuditLogEventTypes.Workspace.BulkDeleteRequested,
            Severity = AuditLogSeverities.Critical,
            WorkspaceExternalId = workspace.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Workspace.BulkDeleteRequested {
                Storage = storage,
                Workspace = workspace,
                Files = files,
                Folders = folders,
                FileUploads = fileUploads })
        };
    }

    public static class Folder
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FolderRef folder,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.Created {
                Workspace = workspace,
                Folder = folder,
                Box = box })
        };

        public static AuditLogEntry BulkCreated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            List<AuditLogDetails.FolderRef> folders,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.BulkCreated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.BulkCreated {
                Workspace = workspace,
                Folders = folders,
                Box = box })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FolderRef folder,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.NameUpdated {
                Workspace = workspace,
                Folder = folder,
                Box = box })
        };

        public static AuditLogEntry ItemsMoved(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FolderRef? destinationFolder,
            List<AuditLogDetails.FolderRef> folders,
            List<AuditLogDetails.FileRef> files,
            List<AuditLogDetails.FileUploadRef> fileUploads,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Folder,
            EventType = AuditLogEventTypes.Folder.ItemsMoved,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Folder.ItemsMoved {
                Workspace = workspace,
                DestinationFolder = destinationFolder,
                Folders = folders,
                Files = files,
                FileUploads = fileUploads,
                Box = box })
        };
    }

    public static class Box
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.Created,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.Created {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.Deleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.Deleted {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.NameUpdated {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry HeaderIsEnabledUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.HeaderIsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.HeaderIsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry HeaderUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            string contentJson) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.HeaderUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.HeaderUpdated {
                Workspace = workspace,
                Box = box,
                ContentJson = contentJson })
        };

        public static AuditLogEntry FooterIsEnabledUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FooterIsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.FooterIsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry FooterUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            string contentJson) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FooterUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.FooterUpdated {
                Workspace = workspace,
                Box = box,
                ContentJson = contentJson })
        };

        public static AuditLogEntry FolderUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.FolderRef newFolder) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.FolderUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.FolderUpdated {
                Workspace = workspace,
                Box = box,
                NewFolder = newFolder })
        };

        public static AuditLogEntry IsEnabledUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.IsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.IsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry MemberInvited(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            List<AuditLogDetails.UserRef> members) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberInvited,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberInvited {
                Workspace = workspace,
                Box = box,
                Members = members })
        };

        public static AuditLogEntry MemberRevoked(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.UserRef member) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberRevoked,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberRevoked {
                Workspace = workspace,
                Box = box,
                Member = member })
        };

        public static AuditLogEntry MemberPermissionsUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.UserRef member,
            BoxPermissions permissions) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberPermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberPermissionsUpdated {
                Workspace = workspace,
                Box = box,
                Member = member,
                Permissions = permissions })
        };

        public static AuditLogEntry LinkCreated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            BoxLinkExtId linkExternalId,
            string linkName) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.LinkCreated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.LinkCreated {
                Workspace = workspace,
                Box = box,
                LinkExternalId = linkExternalId,
                LinkName = linkName })
        };

        public static AuditLogEntry InvitationAccepted(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.InvitationAccepted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.InvitationAccepted {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry InvitationRejected(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.InvitationRejected,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.InvitationRejected {
                Workspace = workspace,
                Box = box })
        };

        public static AuditLogEntry MemberLeft(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Box,
            EventType = AuditLogEventTypes.Box.MemberLeft,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.Box.MemberLeft {
                Workspace = workspace,
                Box = box })
        };
    }

    public static class BoxLink
    {
        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.BoxLinkRef boxLink) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.Deleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.Deleted {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.BoxLinkRef boxLink) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.NameUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.NameUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink })
        };

        public static AuditLogEntry WidgetOriginsUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.BoxLinkRef boxLink,
            List<string> widgetOrigins) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.WidgetOriginsUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.WidgetOriginsUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink,
                WidgetOrigins = widgetOrigins })
        };

        public static AuditLogEntry IsEnabledUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.BoxLinkRef boxLink,
            bool isEnabled) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.IsEnabledUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.IsEnabledUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink,
                IsEnabled = isEnabled })
        };

        public static AuditLogEntry PermissionsUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.BoxLinkRef boxLink,
            BoxPermissions permissions) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.PermissionsUpdated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.PermissionsUpdated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink,
                Permissions = permissions })
        };

        public static AuditLogEntry AccessCodeRegenerated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.BoxRef box,
            AuditLogDetails.BoxLinkRef boxLink) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.BoxLink,
            EventType = AuditLogEventTypes.BoxLink.AccessCodeRegenerated,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box.ExternalId.Value,
            BoxLinkExternalId = boxLink.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.BoxLink.AccessCodeRegenerated {
                Workspace = workspace,
                Box = box,
                BoxLink = boxLink })
        };
    }

    public static class File
    {
        public static AuditLogEntry Renamed(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.Renamed,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.Renamed {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry NoteSaved(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.NoteSaved,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.NoteSaved {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry CommentCreated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            FileArtifactExtId commentExternalId,
            string contentJson,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentCreated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.CommentCreated {
                Workspace = workspace,
                File = file,
                CommentExternalId = commentExternalId,
                ContentJson = contentJson,
                Box = box })
        };

        public static AuditLogEntry CommentDeleted(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            FileArtifactExtId commentExternalId,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentDeleted,
            Severity = AuditLogSeverities.Warning,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.CommentDeleted {
                Workspace = workspace,
                File = file,
                CommentExternalId = commentExternalId,
                Box = box })
        };

        public static AuditLogEntry CommentEdited(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            FileArtifactExtId commentExternalId,
            string contentJson,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.CommentEdited,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.CommentEdited {
                Workspace = workspace,
                File = file,
                CommentExternalId = commentExternalId,
                ContentJson = contentJson,
                Box = box })
        };

        public static AuditLogEntry ContentUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.ContentUpdated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.ContentUpdated {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry AttachmentUploaded(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef parentFile,
            AuditLogDetails.FileRef attachment,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.AttachmentUploaded,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.AttachmentUploaded {
                Workspace = workspace,
                ParentFile = parentFile,
                Attachment = attachment,
                Box = box })
        };

        public static AuditLogEntry DownloadLinkGenerated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.DownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.DownloadLinkGenerated {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry BulkDownloadLinkGenerated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            List<FileExtId> selectedFileExternalIds,
            List<FolderExtId> selectedFolderExternalIds,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.BulkDownloadLinkGenerated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.BulkDownloadLinkGenerated {
                Workspace = workspace,
                SelectedFileExternalIds = selectedFileExternalIds,
                SelectedFolderExternalIds = selectedFolderExternalIds,
                Box = box })
        };

        public static AuditLogEntry Downloaded(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileRef file,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.Downloaded,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.Downloaded {
                Workspace = workspace,
                File = file,
                Box = box })
        };

        public static AuditLogEntry BulkDownloaded(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            List<AuditLogDetails.FileRef> files,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.BulkDownloaded,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.BulkDownloaded {
                Workspace = workspace,
                Files = files,
                Box = box })
        };

        public static AuditLogEntry UploadInitiated(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            List<AuditLogDetails.FileUploadRef> fileUploads,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.UploadInitiated,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.UploadInitiated {
                Workspace = workspace,
                FileUploads = fileUploads,
                Box = box })
        };

        public static AuditLogEntry UploadCompleted(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            AuditLogDetails.FileUploadRef fileUpload,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.UploadCompleted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.UploadCompleted {
                Workspace = workspace,
                FileUpload = fileUpload,
                Box = box })
        };

        public static AuditLogEntry MultiUploadCompleted(
            AuditLogActorContext actor,
            AuditLogDetails.WorkspaceRef workspace,
            List<AuditLogDetails.FileUploadRef> fileUploads,
            AuditLogDetails.BoxAccessRef? box = null) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.File,
            EventType = AuditLogEventTypes.File.MultiUploadCompleted,
            Severity = AuditLogSeverities.Info,
            WorkspaceExternalId = workspace.ExternalId.Value,
            BoxExternalId = box?.ExternalId.Value,
            BoxLinkExternalId = box?.BoxLink?.ExternalId.Value,
            DetailsJson = Json.Serialize(new AuditLogDetails.File.MultiUploadCompleted {
                Workspace = workspace,
                FileUploads = fileUploads,
                Box = box })
        };
    }

    public static class Storage
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.Created,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.Created {
                Storage = storage })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.Deleted {
                Storage = storage })
        };

        public static AuditLogEntry NameUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.NameUpdated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.NameUpdated {
                Storage = storage })
        };

        public static AuditLogEntry DetailsUpdated(
            AuditLogActorContext actor,
            AuditLogDetails.StorageRef storage) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Storage,
            EventType = AuditLogEventTypes.Storage.DetailsUpdated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Storage.DetailsUpdated {
                Storage = storage })
        };
    }

    public static class Integration
    {
        public static AuditLogEntry Created(
            AuditLogActorContext actor,
            IntegrationExtId externalId,
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
                ExternalId = externalId,
                Name = name,
                Type = type })
        };

        public static AuditLogEntry Deleted(
            AuditLogActorContext actor,
            IntegrationExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Deleted,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.Deleted {
                ExternalId = externalId,
                Name = name })
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
            IntegrationExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Activated,
            Severity = AuditLogSeverities.Info,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.ActivationChanged {
                ExternalId = externalId,
                Name = name })
        };

        public static AuditLogEntry Deactivated(
            AuditLogActorContext actor,
            IntegrationExtId externalId,
            string name) => new()
        {
            Actor = actor.Identity,
            ActorEmail = actor.Email,
            ActorIp = actor.Ip,
            CorrelationId = actor.CorrelationId,
            EventCategory = AuditLogEventCategories.Integration,
            EventType = AuditLogEventTypes.Integration.Deactivated,
            Severity = AuditLogSeverities.Warning,
            DetailsJson = Json.Serialize(new AuditLogDetails.Integration.ActivationChanged {
                ExternalId = externalId,
                Name = name })
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
