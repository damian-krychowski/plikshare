using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Permissions;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.Members.UpdatePermissions;

public class UpdateBoxMemberPermissionsQuery(DbWriteQueue dbWriteQueue)
{
    public Task Execute(
        BoxMembershipContext boxMembership,
        BoxPermissions permissions,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxMembership: boxMembership,
                permissions: permissions),
            cancellationToken: cancellationToken);
    }

    private void ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxMembershipContext boxMembership,
        BoxPermissions permissions)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bm_box_membership
                     SET 
                         bm_allow_download = $allowDownload,
                         bm_allow_upload = $allowUpload,
                         bm_allow_list = $allowList,
                         bm_allow_delete_file = $allowDeleteFile,
                         bm_allow_rename_file = $allowRenameFile,
                         bm_allow_move_items = $allowMoveItems,
                         bm_allow_create_folder = $allowCreateFolder,
                         bm_allow_delete_folder = $allowDeleteFolder,
                         bm_allow_rename_folder = $allowRenameFolder
                     WHERE 
                         bm_box_id = $boxId
                         AND bm_member_id = $memberId
                     RETURNING 
                         bm_box_id,
                         bm_member_id;
                     """,
                readRowFunc: reader => new BoxMember(
                    BoxId: reader.GetInt32(0),
                    MemberId: reader.GetInt32(1)))
            .WithParameter("$allowDownload", permissions.AllowDownload)
            .WithParameter("$allowUpload", permissions.AllowUpload)
            .WithParameter("$allowList", permissions.AllowList)
            .WithParameter("$allowDeleteFile", permissions.AllowDeleteFile)
            .WithParameter("$allowRenameFile", permissions.AllowRenameFile)
            .WithParameter("$allowMoveItems", permissions.AllowMoveItems)
            .WithParameter("$allowCreateFolder", permissions.AllowCreateFolder)
            .WithParameter("$allowDeleteFolder", permissions.AllowDeleteFolder)
            .WithParameter("$allowRenameFolder", permissions.AllowRenameFolder)
            .WithParameter("$boxId", boxMembership.Box.Id)
            .WithParameter("$memberId", boxMembership.Member.Id)
            .Execute();
        
        if (result.IsEmpty)
        {
            Log.Warning("Could not update membership permissions for Box '{BoxExternalId}' and Member '{MemberExternalId'} because it was not found.",
                boxMembership.Box.ExternalId,
                boxMembership.Member.ExternalId);
        }
        else
        {
            Log.Information("Membership permissions for Box '{BoxExternalId}' and Member '{MemberExternalId'} were updated.",
                boxMembership.Box.ExternalId,
                boxMembership.Member.ExternalId);
        }
    }

    private readonly record struct BoxMember(
        int BoxId,
        int MemberId);
}