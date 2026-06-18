using System.Text.Json.Serialization;
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
public abstract class AgentOperationDetails
{
}
