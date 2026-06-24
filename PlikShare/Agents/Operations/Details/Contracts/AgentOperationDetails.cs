using System.Text.Json.Serialization;
using PlikShare.Mcp.BoxAccess.BulkDownloadLink.Contracts;
using PlikShare.Mcp.BoxAccess.Content.Contracts;
using PlikShare.Mcp.BoxAccess.CreateFile.Contracts;
using PlikShare.Mcp.BoxAccess.CreateFolder.Contracts;
using PlikShare.Mcp.BoxAccess.Delete.Contracts;
using PlikShare.Mcp.BoxAccess.DownloadLink.Contracts;
using PlikShare.Mcp.BoxAccess.GetDetails.Contracts;
using PlikShare.Mcp.BoxAccess.List.Contracts;
using PlikShare.Mcp.BoxAccess.MoveItems.Contracts;
using PlikShare.Mcp.BoxAccess.ReadFile.Contracts;
using PlikShare.Mcp.BoxAccess.RenameFile.Contracts;
using PlikShare.Mcp.BoxAccess.RenameFolder.Contracts;
using PlikShare.Mcp.BoxAccess.Search.Contracts;
using PlikShare.Mcp.Boxes.Create.Contracts;
using PlikShare.Mcp.Boxes.Delete.Contracts;
using PlikShare.Mcp.Boxes.Get.Contracts;
using PlikShare.Mcp.Boxes.ListWorkspaceBoxes.Contracts;
using PlikShare.Mcp.Boxes.Members.Invite.Contracts;
using PlikShare.Mcp.Boxes.Members.List.Contracts;
using PlikShare.Mcp.Boxes.Members.Revoke.Contracts;
using PlikShare.Mcp.Boxes.Members.UpdatePermissions.Contracts;
using PlikShare.Mcp.Boxes.Update.Contracts;
using PlikShare.Mcp.BoxLinks.Create.Contracts;
using PlikShare.Mcp.BoxLinks.Delete.Contracts;
using PlikShare.Mcp.BoxLinks.List.Contracts;
using PlikShare.Mcp.BoxLinks.RegenerateAccessCode.Contracts;
using PlikShare.Mcp.BoxLinks.Update.Contracts;
using PlikShare.Mcp.BulkDelete.Contracts;
using PlikShare.Mcp.Files.BulkDownloadLink.Contracts;
using PlikShare.Mcp.Files.Create.Contracts;
using PlikShare.Mcp.Files.DownloadLink.Contracts;
using PlikShare.Mcp.Files.Get.Contracts;
using PlikShare.Mcp.Files.Read.Contracts;
using PlikShare.Mcp.Files.Rename.Contracts;
using PlikShare.Mcp.Folders.Create.Contracts;
using PlikShare.Mcp.Folders.Rename.Contracts;
using PlikShare.Mcp.MoveItems.Contracts;
using PlikShare.Mcp.Search.Contracts;
using PlikShare.Mcp.ShareLinks.Create.Contracts;
using PlikShare.Mcp.ShareLinks.Delete.Contracts;
using PlikShare.Mcp.ShareLinks.Get.Contracts;
using PlikShare.Mcp.ShareLinks.List.Contracts;
using PlikShare.Mcp.ShareLinks.Update.Contracts;
using PlikShare.Mcp.Storages.List.Contracts;
using PlikShare.Mcp.Workspaces.Content.Contracts;
using PlikShare.Mcp.Workspaces.Create.Contracts;
using PlikShare.Mcp.Workspaces.List.Contracts;
using PlikShare.Mcp.Workspaces.Members.Invite.Contracts;
using PlikShare.Mcp.Workspaces.Members.List.Contracts;
using PlikShare.Mcp.Workspaces.Members.Revoke.Contracts;
using PlikShare.Mcp.Workspaces.Members.UpdatePermissions.Contracts;
using PlikShare.Mcp.Workspaces.Rename.Contracts;

namespace PlikShare.Agents.Operations.Details.Contracts;

[JsonDerivedType(derivedType: typeof(BulkDeleteOperationDetails), typeDiscriminator: BulkDeleteOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(DeleteShareLinkOperationDetails), typeDiscriminator: DeleteShareLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RenameFolderOperationDetails), typeDiscriminator: RenameFolderOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RenameFileOperationDetails), typeDiscriminator: RenameFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateFolderOperationDetails), typeDiscriminator: CreateFolderOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(MoveItemsOperationDetails), typeDiscriminator: MoveItemsOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateFileOperationDetails), typeDiscriminator: CreateFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RenameWorkspaceOperationDetails), typeDiscriminator: RenameWorkspaceOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateShareLinkOperationDetails), typeDiscriminator: CreateShareLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(UpdateShareLinkOperationDetails), typeDiscriminator: UpdateShareLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateWorkspaceOperationDetails), typeDiscriminator: CreateWorkspaceOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ReadFileOperationDetails), typeDiscriminator: ReadFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetFileOperationDetails), typeDiscriminator: GetFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetFileDownloadLinkOperationDetails), typeDiscriminator: GetFileDownloadLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListWorkspacesOperationDetails), typeDiscriminator: ListWorkspacesOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListStoragesOperationDetails), typeDiscriminator: ListStoragesOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListShareLinksOperationDetails), typeDiscriminator: ListShareLinksOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetShareLinkOperationDetails), typeDiscriminator: GetShareLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(SearchOperationDetails), typeDiscriminator: SearchOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListWorkspaceContentOperationDetails), typeDiscriminator: ListWorkspaceContentOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetBulkDownloadLinkOperationDetails), typeDiscriminator: GetBulkDownloadLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListWorkspaceMembersOperationDetails), typeDiscriminator: ListWorkspaceMembersOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(InviteWorkspaceMembersOperationDetails), typeDiscriminator: InviteWorkspaceMembersOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(UpdateWorkspaceMemberPermissionsOperationDetails), typeDiscriminator: UpdateWorkspaceMemberPermissionsOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RevokeWorkspaceMemberOperationDetails), typeDiscriminator: RevokeWorkspaceMemberOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListWorkspaceBoxesOperationDetails), typeDiscriminator: ListWorkspaceBoxesOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetBoxOperationDetails), typeDiscriminator: GetBoxOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateBoxOperationDetails), typeDiscriminator: CreateBoxOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(UpdateBoxOperationDetails), typeDiscriminator: UpdateBoxOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(DeleteBoxOperationDetails), typeDiscriminator: DeleteBoxOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListBoxLinksOperationDetails), typeDiscriminator: ListBoxLinksOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateBoxLinkOperationDetails), typeDiscriminator: CreateBoxLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(UpdateBoxLinkOperationDetails), typeDiscriminator: UpdateBoxLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(DeleteBoxLinkOperationDetails), typeDiscriminator: DeleteBoxLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RegenerateBoxLinkAccessCodeOperationDetails), typeDiscriminator: RegenerateBoxLinkAccessCodeOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListBoxMembersOperationDetails), typeDiscriminator: ListBoxMembersOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(InviteBoxMembersOperationDetails), typeDiscriminator: InviteBoxMembersOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(UpdateBoxMemberPermissionsOperationDetails), typeDiscriminator: UpdateBoxMemberPermissionsOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RevokeBoxMemberOperationDetails), typeDiscriminator: RevokeBoxMemberOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListBoxesOperationDetails), typeDiscriminator: ListBoxesOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetBoxDetailsOperationDetails), typeDiscriminator: GetBoxDetailsOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ListBoxContentOperationDetails), typeDiscriminator: ListBoxContentOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(ReadBoxFileOperationDetails), typeDiscriminator: ReadBoxFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetBoxFileDownloadLinkOperationDetails), typeDiscriminator: GetBoxFileDownloadLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(GetBoxBulkDownloadLinkOperationDetails), typeDiscriminator: GetBoxBulkDownloadLinkOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(SearchBoxOperationDetails), typeDiscriminator: SearchBoxOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateBoxFolderOperationDetails), typeDiscriminator: CreateBoxFolderOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(CreateBoxFileOperationDetails), typeDiscriminator: CreateBoxFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RenameBoxFileOperationDetails), typeDiscriminator: RenameBoxFileOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(RenameBoxFolderOperationDetails), typeDiscriminator: RenameBoxFolderOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(MoveBoxItemsOperationDetails), typeDiscriminator: MoveBoxItemsOperationDetails.TypeDiscriminator)]
[JsonDerivedType(derivedType: typeof(DeleteBoxItemsOperationDetails), typeDiscriminator: DeleteBoxItemsOperationDetails.TypeDiscriminator)]
public abstract class AgentOperationDetails
{
}
