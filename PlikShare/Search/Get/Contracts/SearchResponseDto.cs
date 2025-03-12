using ProtoBuf;

namespace PlikShare.Search.Get.Contracts;

[ProtoContract]
public class SearchResponseDto
{
    [ProtoMember(1)]
    public required List<WorkspaceGroup> WorkspaceGroups { get; init; }

    [ProtoMember(2)]
    public required List<ExternalBoxGroup> ExternalBoxGroups { get; init; }

    [ProtoMember(3)]
    public required List<Workspace> Workspaces { get; init; }

    [ProtoMember(4)]
    public required List<WorkspaceFolder> WorkspaceFolders { get; init; }

    [ProtoMember(5)]
    public required List<WorkspaceBox> WorkspaceBoxes { get; init; }

    [ProtoMember(6)]
    public required List<WorkspaceFile> WorkspaceFiles { get; init; }

    [ProtoMember(7)]
    public required List<ExternalBox> ExternalBoxes { get; init; }

    [ProtoMember(8)]
    public required List<ExternalBoxFolder> ExternalBoxFolders { get; init; }

    [ProtoMember(9)]
    public required List<ExternalBoxFile> ExternalBoxFiles { get; init; }


    [ProtoContract]
    public class Workspace
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required long CurrentSizeInBytes { get; init; }

        [ProtoMember(4)]
        public required string OwnerEmail { get; init; }

        [ProtoMember(5)]
        public required string OwnerExternalId { get; init; }

        [ProtoMember(6)]
        public required bool IsOwnedByUser { get; init; }

        [ProtoMember(7)]
        public required bool AllowShare { get; init; }

        [ProtoMember(8)]
        public required bool IsUsedByIntegration { get; init; }

        [ProtoMember(9)]
        public required bool IsBucketCreated { get; init; }
    }


    [ProtoContract]
    public class WorkspaceFolder
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string WorkspaceExternalId { get; init; }

        [ProtoMember(4)]
        public required List<FolderAncestor> Ancestors { get; init; }
    }


    [ProtoContract]
    public class FolderAncestor
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }
    }


    [ProtoContract]
    public class WorkspaceBox
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string WorkspaceExternalId { get; init; }

        [ProtoMember(4)]
        public required bool IsEnabled { get; init; }

        [ProtoMember(5)]
        public required List<FolderAncestor> FolderPath { get; init; }
    }


    [ProtoContract]
    public class WorkspaceFile
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string WorkspaceExternalId { get; init; }

        [ProtoMember(4)]
        public required long SizeInBytes { get; init; }

        [ProtoMember(5)]
        public required string Extension { get; init; }

        [ProtoMember(6)]
        public required List<FolderAncestor> FolderPath { get; init; }
    }


    [ProtoContract]
    public class ExternalBox
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string OwnerEmail { get; init; }

        [ProtoMember(4)]
        public required string OwnerExternalId { get; init; }


        [ProtoMember(5)]
        public required bool AllowDownload { get; init; }

        [ProtoMember(6)]
        public required bool AllowUpload { get; init; }

        [ProtoMember(7)]
        public required bool AllowList { get; init; }

        [ProtoMember(8)]
        public required bool AllowDeleteFile { get; init; }

        [ProtoMember(9)]
        public required bool AllowRenameFile { get; init; }

        [ProtoMember(10)]
        public required bool AllowMoveItems { get; init; }

        [ProtoMember(11)]
        public required bool AllowCreateFolder { get; init; }

        [ProtoMember(12)]
        public required bool AllowDeleteFolder { get; init; }

        [ProtoMember(13)]
        public required bool AllowRenameFolder { get; init; }
    }


    [ProtoContract]
    public class ExternalBoxFolder
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string BoxExternalId { get; init; }

        [ProtoMember(4)]
        public required List<FolderAncestor> Ancestors { get; init; }
    }


    [ProtoContract]
    public class ExternalBoxFile
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required string BoxExternalId { get; init; }

        [ProtoMember(4)]
        public required long SizeInBytes { get; init; }

        [ProtoMember(5)]
        public required string Extension { get; init; }

        [ProtoMember(6)]
        public required List<FolderAncestor> FolderPath { get; init; }

        [ProtoMember(7)]
        public required bool WasUploadedByUser { get; init; }
    }


    [ProtoContract]
    public class WorkspaceGroup
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required bool AllowShare { get; init; }

        [ProtoMember(4)]
        public required bool IsOwnedByUser { get; init; }
    }


    [ProtoContract]
    public class ExternalBoxGroup
    {
        [ProtoMember(1)]
        public required string ExternalId { get; init; }

        [ProtoMember(2)]
        public required string Name { get; init; }

        [ProtoMember(3)]
        public required bool AllowDownload { get; init; }

        [ProtoMember(4)]
        public required bool AllowUpload { get; init; }

        [ProtoMember(5)]
        public required bool AllowList { get; init; }

        [ProtoMember(6)]
        public required bool AllowDeleteFile { get; init; }

        [ProtoMember(7)]
        public required bool AllowRenameFile { get; init; }

        [ProtoMember(8)]
        public required bool AllowMoveItems { get; init; }

        [ProtoMember(9)]
        public required bool AllowCreateFolder { get; init; }

        [ProtoMember(10)]
        public required bool AllowDeleteFolder { get; init; }

        [ProtoMember(11)]
        public required bool AllowRenameFolder { get; init; }
    }
}