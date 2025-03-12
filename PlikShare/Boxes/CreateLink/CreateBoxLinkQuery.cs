using PlikShare.Boxes.Cache;
using PlikShare.BoxLinks.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Boxes.CreateLink;

public class CreateBoxLinkQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        BoxContext box,
        string name,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                name: name),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        BoxContext box,
        string name)
    {
        var externalId = BoxLinkExtId.NewId();
        var accessCode = Guid.NewGuid().ToBase62();

        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO bl_box_links (
                         bl_external_id,
                         bl_box_id,
                         bl_is_enabled,
                         bl_name,
                         bl_access_code,
                         bl_allow_download,
                         bl_allow_upload,
                         bl_allow_list,
                         bl_allow_delete_file,
                         bl_allow_rename_file,
                         bl_allow_move_items,
                         bl_allow_create_folder,
                         bl_allow_delete_folder,
                         bl_allow_rename_folder
                     ) VALUES(                        
                         $boxLinkExternalId,
                         $boxId,
                         TRUE,
                         $name,
                         $accessCode,
                         FALSE,
                         FALSE,
                         TRUE,
                         FALSE,
                         FALSE,
                         FALSE,
                         FALSE,
                         FALSE,
                         FALSE
                     )
                     RETURNING bl_id     
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$boxLinkExternalId", externalId.Value)
            .WithParameter("$boxId", box.Id)
            .WithParameter("$name", name)
            .WithParameter("$accessCode", accessCode)
            .ExecuteOrThrow();

        Log.Information("BoxLink '{BoxLinkExternalId} ({BoxLinkId})' was created for Box '{BoxExternalId}'",
            externalId,
            result,
            box.ExternalId);

        return new Result(
            Code: ResultCode.Ok,
            BoxLink: new BoxLink(
                ExternalId: externalId,
                AccessCode: accessCode));
    }

    public readonly record struct Result(
        ResultCode Code,
        BoxLink BoxLink = default);

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }

    public readonly record struct BoxLink(
        BoxLinkExtId ExternalId,
        string AccessCode);
}