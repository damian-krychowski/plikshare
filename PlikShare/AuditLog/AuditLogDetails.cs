using PlikShare.AuthProviders.Id;
using PlikShare.Boxes.Id;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Id;
using PlikShare.EmailProviders.Id;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Id;
using PlikShare.Users.Id;
using PlikShare.Workspaces.Id;

namespace PlikShare.AuditLog;

public static class AuditLogDetails
{
    public class WorkspaceRef
    {
        public required WorkspaceExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }

    public class FileRef
    {
        public required FileExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required long SizeInBytes { get; init; }
        public string? FolderPath { get; init; }
    }

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
            public required string TargetEmail { get; init; }
            public required UserExtId TargetExternalId { get; init; }
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

    public static class Folder
    {
        public class Created
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required FolderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class BulkCreated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required List<FolderExtId> FolderExternalIds { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required FolderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ItemsMoved
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required FolderExtId? DestinationFolderExternalId { get; init; }
            public required List<FolderExtId> FolderExternalIds { get; init; }
            public required List<FileExtId> FileExternalIds { get; init; }
            public required List<FileUploadExtId> FileUploadExternalIds { get; init; }
        }
    }

    public static class Box
    {
        public class Created
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required string Name { get; init; }
            public required FolderExtId FolderExternalId { get; init; }
        }

        public class Deleted
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class HeaderIsEnabledUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class HeaderUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
        }

        public class FooterIsEnabledUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class FooterUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
        }

        public class FolderUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required FolderExtId FolderExternalId { get; init; }
        }

        public class IsEnabledUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class MemberInvited
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required List<string> MemberEmails { get; init; }
        }

        public class MemberRevoked
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required string MemberEmail { get; init; }
        }

        public class MemberPermissionsUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId ExternalId { get; init; }
            public required string MemberEmail { get; init; }
            public required BoxPermissions Permissions { get; init; }
        }

        public class LinkCreated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class InvitationAccepted
        {
            public required BoxExtId ExternalId { get; init; }
        }

        public class InvitationRejected
        {
            public required BoxExtId ExternalId { get; init; }
        }

        public class MemberLeft
        {
            public required BoxExtId ExternalId { get; init; }
        }
    }

    public static class BoxLink
    {
        public class Deleted
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class WidgetOriginsUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
            public required List<string> WidgetOrigins { get; init; }
        }

        public class IsEnabledUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class PermissionsUpdated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
            public required BoxPermissions Permissions { get; init; }
        }

        public class AccessCodeRegenerated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required BoxExtId BoxExternalId { get; init; }
            public required BoxLinkExtId ExternalId { get; init; }
        }
    }

    public static class File
    {
        public class Renamed
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
        }

        public class NoteSaved
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
        }

        public class CommentCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public required string ContentJson { get; init; }
        }

        public class CommentDeleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
        }

        public class CommentEdited
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public required string ContentJson { get; init; }
        }

        public class ContentUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
        }

        public class AttachmentUploaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef ParentFile { get; init; }
            public required FileRef Attachment { get; init; }
        }

        public class DownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
        }

        public class BulkDownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileExtId> SelectedFileExternalIds { get; init; }
            public required List<FolderExtId> SelectedFolderExternalIds { get; init; }
        }

        public class Downloaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
        }

        public class BulkDownloaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileRef> Files { get; init; }
        }
    }

    public static class Upload
    {
        public class BulkInitiated
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required List<string> FileNames { get; init; }
        }

        public class Completed
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required FileUploadExtId FileUploadExternalId { get; init; }
            public required FileExtId FileExternalId { get; init; }
        }

        public class FilePartUploaded
        {
            public required FileUploadExtId FileUploadExternalId { get; init; }
            public required int PartNumber { get; init; }
        }

        public class MultiFileDirectUploaded
        {
            public required WorkspaceExtId WorkspaceExternalId { get; init; }
            public required List<FileExtId> FileExternalIds { get; init; }
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
