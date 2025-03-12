using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.BoxLinks.UpdatePermissions;

public class UpdateBoxLinkPermissionsQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxLinkContext boxLink,
        BoxPermissions permissions,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                boxLink: boxLink,
                permissions: permissions),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxLinkContext boxLink,
        BoxPermissions permissions)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bl_box_links
                     SET 
                         bl_allow_download = $allowDownload,
                         bl_allow_upload = $allowUpload,
                         bl_allow_list = $allowList,
                         bl_allow_delete_file = $allowDeleteFile,
                         bl_allow_rename_file = $allowRenameFile,
                         bl_allow_move_items = $allowMoveItems,
                         bl_allow_create_folder = $allowCreateFolder,
                         bl_allow_delete_folder = $allowDeleteFolder,
                         bl_allow_rename_folder = $allowRenameFolder
                     WHERE bl_id = $boxLinkId
                     RETURNING bl_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$boxLinkId", boxLink.Id)
            .WithParameter("$allowDownload", permissions.AllowDownload)
            .WithParameter("$allowUpload", permissions.AllowUpload)
            .WithParameter("$allowList", permissions.AllowList)
            .WithParameter("$allowDeleteFile", permissions.AllowDeleteFile)
            .WithParameter("$allowRenameFile", permissions.AllowRenameFile)
            .WithParameter("$allowMoveItems", permissions.AllowMoveItems)
            .WithParameter("$allowCreateFolder", permissions.AllowCreateFolder)
            .WithParameter("$allowDeleteFolder", permissions.AllowDeleteFolder)
            .WithParameter("$allowRenameFolder", permissions.AllowRenameFolder)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update BoxLink '{BoxLinkExternalId}' permissions to '{@Permissions}' because BoxLink was not found",
                boxLink.ExternalId,
                permissions);

            return ResultCode.BoxLinkNotFound;
        }
        
        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' permissions were updated to '{@Permissions}'.",
            boxLink.ExternalId,
            result.Value,
            permissions);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxLinkNotFound
    }
}