using ProtoBuf;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace PlikShare.Dashboard.Content.Contracts;

[ProtoContract]
public class GetDashboardContentResponseDto
{
    [ProtoMember(1)]
    public List<Storage> Storages { get; set; }

    [ProtoMember(2)]
    public List<WorkspaceDetails> Workspaces { get; set; }

    [ProtoMember(3)]
    public List<WorkspaceInvitation> WorkspaceInvitations { get; set; }

    [ProtoMember(4)]
    public List<WorkspaceDetails> OtherWorkspaces { get; set; }

    [ProtoMember(5)]
    public List<ExternalBox> Boxes { get; set; }

    [ProtoMember(6)]
    public List<ExternalBoxInvitation> BoxInvitations { get; set; }


    [ProtoContract]
    public class Storage
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string Type { get; init; }

        [ProtoMember(4)]
        public required int? WorkspacesCount { get; init; }

        [ProtoMember(5)]
        public required string EncryptionType { get; init; }
    }

    [ProtoContract]
    public class WorkspaceDetails
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required long CurrentSizeInBytes { get; init; }

        [ProtoMember(4)]
        public required long MaxSizeInBytes { get; init; }

        [ProtoMember(5)]
        public required User Owner { get; init; }

        [ProtoMember(6)]
        public required string? StorageName { get; init; }

        [ProtoMember(7)]
        public required WorkspacePermissions Permissions { get; init; }

        [ProtoMember(8)]
        public required bool IsUsedByIntegration { get; init; }

        [ProtoMember(9)]
        public required bool IsBucketCreated { get; init; }
    }

    [ProtoContract]
    public class WorkspaceInvitation
    {
        [ProtoMember(1)]
        public required string WorkspaceExternalId { get; init; }

        [ProtoMember(2)]
        public required string WorkspaceName { get; init; }

        [ProtoMember(3)]
        public required User Owner { get; init; }

        [ProtoMember(4)]
        public required User? Inviter { get; init; }

        [ProtoMember(5)]
        public required WorkspacePermissions Permissions { get; init; }

        [ProtoMember(6)]
        public required string? StorageName { get; init; }

        [ProtoMember(7)]
        public required bool IsUsedByIntegration { get; init; }

        [ProtoMember(8)]
        public required bool IsBucketCreated { get; init; }
    }

    [ProtoContract]
    public class User
    {

        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Email { get; init; }
    }

    [ProtoContract]
    public class WorkspacePermissions
    {

        [ProtoMember(1)]
        public required bool AllowShare { get; init; }
    }

    [ProtoContract]
    public class ExternalBox
    {

        [ProtoMember(1)]
        public required string BoxExternalId { get; init; }

        [ProtoMember(2)]
        public required string BoxName { get; init; }

        [ProtoMember(3)]
        public required User Owner { get; init; }

        [ProtoMember(4)]
        public required BoxPermissions Permissions { get; init; }
    }

    [ProtoContract]
    public class ExternalBoxInvitation
    {

        [ProtoMember(1)]
        public required string BoxExternalId { get; init; }

        [ProtoMember(2)]
        public required string BoxName { get; init; }

        [ProtoMember(3)]
        public required User Owner { get; init; }

        [ProtoMember(4)]
        public required User Inviter { get; init; }

        [ProtoMember(5)]
        public required BoxPermissions Permissions { get; init; }
    }


    [ProtoContract]
    public class BoxPermissions
    {

        [ProtoMember(1)]
        public required bool AllowDownload { get; init; }

        [ProtoMember(2)]
        public required bool AllowUpload { get; init; }

        [ProtoMember(3)]
        public required bool AllowList { get; init; }

        [ProtoMember(4)]
        public required bool AllowDeleteFile { get; init; }

        [ProtoMember(5)]
        public required bool AllowRenameFile { get; init; }

        [ProtoMember(6)]
        public required bool AllowMoveItems { get; init; }

        [ProtoMember(7)]
        public required bool AllowCreateFolder { get; init; }

        [ProtoMember(8)]
        public required bool AllowRenameFolder { get; init; }

        [ProtoMember(9)]
        public required bool AllowDeleteFolder { get; init; }
    }
}