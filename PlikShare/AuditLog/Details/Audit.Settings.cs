using PlikShare.Core.Utils;
using PlikShare.Users.PermissionsAndRoles;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public static class Settings
    {
        public class ValueChanged
        {
            public required string? Value { get; init; }
        }

        public class ToggleChanged
        {
            public required bool Value { get; init; }
        }

        public class DefaultPermissionsChanged
        {
            public required bool IsAdmin { get; init; }
            public required List<string> Permissions { get; init; }
        }

        public class SignUpCheckbox
        {
            public int? Id { get; init; }
            public required string Text { get; init; }
            public required bool IsRequired { get; init; }
        }

        public static AuditLogEntry AppNameChangedEntry(
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
            DetailsJson = Json.Serialize(new ValueChanged {
                Value = value })
        };

        public static AuditLogEntry SignUpOptionChangedEntry(
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
            DetailsJson = Json.Serialize(new ValueChanged {
                Value = value })
        };

        public static AuditLogEntry DefaultPermissionsChangedEntry(
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
            DetailsJson = Json.Serialize(new DefaultPermissionsChanged
            {
                IsAdmin = request.IsAdmin,
                Permissions = request.GetPermissionsList()
            })
        };

        public static AuditLogEntry DefaultMaxWorkspaceNumberChangedEntry(
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
            DetailsJson = Json.Serialize(new ValueChanged {
                Value = value?.ToString() })
        };

        public static AuditLogEntry DefaultMaxWorkspaceSizeChangedEntry(
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
            DetailsJson = Json.Serialize(new ValueChanged {
                Value = value?.ToString() })
        };

        public static AuditLogEntry DefaultMaxWorkspaceTeamMembersChangedEntry(
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
            DetailsJson = Json.Serialize(new ValueChanged {
                Value = value?.ToString() })
        };

        public static AuditLogEntry AlertOnNewUserChangedEntry(
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
            DetailsJson = Json.Serialize(new ToggleChanged {
                Value = value })
        };

        public static AuditLogEntry TermsOfServiceUploadedEntry(
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

        public static AuditLogEntry TermsOfServiceDeletedEntry(
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

        public static AuditLogEntry PrivacyPolicyUploadedEntry(
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

        public static AuditLogEntry PrivacyPolicyDeletedEntry(
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

        public static AuditLogEntry SignUpCheckboxCreatedOrUpdatedEntry(
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
            DetailsJson = Json.Serialize(new SignUpCheckbox {
                Id = id,
                Text = text,
                IsRequired = isRequired })
        };

        public static AuditLogEntry SignUpCheckboxDeletedEntry(
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
            DetailsJson = Json.Serialize(new SignUpCheckbox {
                Id = id,
                Text = "",
                IsRequired = false })
        };
    }
}
