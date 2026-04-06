using PlikShare.AuthProviders.Id;
using PlikShare.EmailProviders.Id;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog;

public static class AuditLogDetails
{
    public static class Auth
    {
        public class SignedIn
        {
            public required string Method { get; init; }
        }

        public class Failed
        {
            public required string Reason { get; init; }
        }

        public class Sso
        {
            public required string ProviderName { get; init; }
        }
    }

    public static class User
    {
        public class Invited
        {
            public required List<string> Emails { get; init; }
        }

        public class Deleted
        {
            public required string TargetEmail { get; init; }
        }

        public class PermissionsAndRolesUpdated
        {
            public required string TargetEmail { get; init; }
            public required bool IsAdmin { get; init; }
            public required List<string> Permissions { get; init; }
        }

        public class LimitUpdated
        {
            public required long? Value { get; init; }
        }
    }

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
    }

    public static class EmailProvider
    {
        public class Created
        {
            public required string Name { get; init; }
            public required string Type { get; init; }
            public required string EmailFrom { get; init; }
        }

        public class Deleted
        {
            public required EmailProviderExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required EmailProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required EmailProviderExtId ExternalId { get; init; }
        }

        public class ConfirmationEmailResent
        {
            public required EmailProviderExtId ExternalId { get; init; }
        }
    }

    public static class Workspace
    {
        public class Created
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class Deleted
        {
            public required WorkspaceExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class OwnerChanged
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required string NewOwnerEmail { get; init; }
        }

        public class MaxSizeUpdated
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required long? Value { get; init; }
        }

        public class MaxTeamMembersUpdated
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required int? Value { get; init; }
        }

        public class MemberInvited
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required List<string> MemberEmails { get; init; }
        }

        public class MemberRevoked
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required string MemberEmail { get; init; }
        }

        public class MemberPermissionsUpdated
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required string MemberEmail { get; init; }
            public required bool AllowShare { get; init; }
        }

        public class InvitationResponse
        {
            public required WorkspaceExtId ExternalId { get; init; }
        }

        public class MemberLeft
        {
            public required WorkspaceExtId ExternalId { get; init; }
        }

        public class BulkDeleteRequested
        {
            public required WorkspaceExtId ExternalId { get; init; }
            public required List<FileExtId> FileExternalIds { get; init; }
            public required List<FolderExtId> FolderExternalIds { get; init; }
            public required List<FileUploadExtId> FileUploadExternalIds { get; init; }
        }
    }

    public static class Storage
    {
        public class Created
        {
            public required string Name { get; init; }
            public required string Type { get; init; }
        }

        public class Deleted
        {
            public required StorageExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required StorageExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class DetailsUpdated
        {
            public required StorageExtId ExternalId { get; init; }
            public required string Type { get; init; }
        }
    }

    public static class Integration
    {
        public class Created
        {
            public required string Name { get; init; }
            public required string Type { get; init; }
        }

        public class Deleted
        {
            public required IntegrationExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required IntegrationExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required IntegrationExtId ExternalId { get; init; }
        }
    }

    public static class AuthProvider
    {
        public class Created
        {
            public required string Name { get; init; }
            public required string Type { get; init; }
        }

        public class Deleted
        {
            public required AuthProviderExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required AuthProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class Updated
        {
            public required AuthProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required AuthProviderExtId ExternalId { get; init; }
        }

        public class PasswordLoginToggled
        {
            public required bool IsEnabled { get; init; }
        }
    }
}
