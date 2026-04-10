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

    public class UserRef
    {
        public required UserExtId ExternalId { get; init; }
        public required string Email { get; init; }
    }

    public class StorageRef
    {
        public required StorageExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required string Type { get; init; }
    }

    public class FileRef
    {
        public required FileExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required long SizeInBytes { get; init; }
        public string? FolderPath { get; init; }
    }

    public class BoxAccessRef
    {
        public required BoxExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public BoxLinkAccessRef? BoxLink { get; init; }
    }

    public class BoxLinkAccessRef
    {
        public required BoxLinkExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }

    public class BoxRef
    {
        public required BoxExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public FolderRef? Folder { get; init; }
    }

    public class BoxLinkRef
    {
        public required BoxLinkExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public string? AccessCode { get; init; }
    }

    public class FolderRef
    {
        public required FolderExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public string? FolderPath { get; init; }
    }

    public class FileUploadRef
    {
        public required FileUploadExtId ExternalId { get; init; }
        public required FileExtId FileExternalId { get; init; }
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
            public required List<UserRef> Users { get; init; }
        }

        public class Deleted
        {
            public required UserRef Target { get; init; }
        }

        public class PermissionsAndRolesUpdated
        {
            public required UserRef Target { get; init; }
            public required bool IsAdmin { get; init; }
            public required List<string> Permissions { get; init; }
        }

        public class LimitUpdated
        {
            public required UserRef Target { get; init; }
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
            public required EmailProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
            public required string Type { get; init; }
            public required string EmailFrom { get; init; }
        }

        public class Deleted
        {
            public required EmailProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class NameUpdated
        {
            public required EmailProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required EmailProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ConfirmationEmailResent
        {
            public required EmailProviderExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }
    }

    public static class Workspace
    {
        public class Created
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required long? MaxSizeInBytes { get; init; }
            public required string BucketName {get;init;}
        }

        public class Deleted
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class NameUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class OwnerChanged
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required UserRef NewOwner { get; init; }
        }

        public class MaxSizeUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required long? Value { get; init; }
        }

        public class MaxTeamMembersUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required int? Value { get; init; }
        }

        public class MemberInvited
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required List<UserRef> Members { get; init; }
        }

        public class MemberRevoked
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required UserRef Member { get; init; }
        }

        public class MemberPermissionsUpdated
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required UserRef Member { get; init; }
            public required bool AllowShare { get; init; }
        }

        public class InvitationResponse
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class MemberLeft
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
        }

        public class BulkDeleteRequested
        {
            public required StorageRef Storage { get; init; }
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileRef> Files { get; init; }
            public required List<FolderRef> Folders { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
        }
    }

    public static class Storage
    {
        public class Created
        {
            public required StorageRef Storage { get; init; }
        }

        public class Deleted
        {
            public required StorageRef Storage { get; init; }
        }

        public class NameUpdated
        {
            public required StorageRef Storage { get; init; }
        }

        public class DetailsUpdated
        {
            public required StorageRef Storage { get; init; }
        }
    }

    public static class Integration
    {
        public class Created
        {
            public required IntegrationExtId ExternalId { get; init; }
            public required string Name { get; init; }
            public required string Type { get; init; }
        }

        public class Deleted
        {
            public required IntegrationExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class NameUpdated
        {
            public required IntegrationExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }

        public class ActivationChanged
        {
            public required IntegrationExtId ExternalId { get; init; }
            public required string Name { get; init; }
        }
    }

    public static class Folder
    {
        public class Created
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FolderRef Folder { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class BulkCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FolderRef> Folders { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FolderRef Folder { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class ItemsMoved
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FolderRef? DestinationFolder { get; init; }
            public required List<FolderRef> Folders { get; init; }
            public required List<FileRef> Files { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
            public BoxAccessRef? Box { get; init; }
        }
    }

    public static class Box
    {
        public class Created
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class Deleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class HeaderIsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class HeaderUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required string ContentJson { get; init; }
        }

        public class FooterIsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class FooterUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required string ContentJson { get; init; }
        }

        public class FolderUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required FolderRef NewFolder { get; init; }
        }

        public class IsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class MemberInvited
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required List<UserRef> Members { get; init; }
        }

        public class MemberRevoked
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required UserRef Member { get; init; }
        }

        public class MemberPermissionsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required UserRef Member { get; init; }
            public required BoxPermissions Permissions { get; init; }
        }

        public class LinkCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkExtId LinkExternalId { get; init; }
            public required string LinkName { get; init; }
        }

        public class InvitationAccepted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class InvitationRejected
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }

        public class MemberLeft
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
        }
    }

    public static class BoxLink
    {
        public class Deleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
        }

        public class NameUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
        }

        public class WidgetOriginsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
            public required List<string> WidgetOrigins { get; init; }
        }

        public class IsEnabledUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
            public required bool IsEnabled { get; init; }
        }

        public class PermissionsUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
            public required BoxPermissions Permissions { get; init; }
        }

        public class AccessCodeRegenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required BoxRef Box { get; init; }
            public required BoxLinkRef BoxLink { get; init; }
        }
    }

    public static class File
    {
        public class Renamed
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class NoteSaved
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class CommentCreated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public required string ContentJson { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class CommentDeleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class CommentEdited
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public required FileArtifactExtId CommentExternalId { get; init; }
            public required string ContentJson { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class ContentUpdated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class AttachmentUploaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef ParentFile { get; init; }
            public required FileRef Attachment { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class DownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class BulkDownloadLinkGenerated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileExtId> SelectedFileExternalIds { get; init; }
            public required List<FolderExtId> SelectedFolderExternalIds { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class Downloaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileRef File { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class BulkDownloaded
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileRef> Files { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class UploadInitiated
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class MultiUploadCompleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required List<FileUploadRef> FileUploads { get; init; }
            public BoxAccessRef? Box { get; init; }
        }

        public class UploadCompleted
        {
            public required WorkspaceRef Workspace { get; init; }
            public required FileUploadRef FileUpload { get; init; }
            public BoxAccessRef? Box { get; init; }
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
