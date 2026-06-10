using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Metadata;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.UpdateThumbnailsPolicy;

public class UpdateWorkspaceThumbnailsPolicyQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        bool generateOnUpload,
        ThumbnailVariant[] variants,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                generateOnUpload: generateOnUpload,
                variants: variants),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        bool generateOnUpload,
        ThumbnailVariant[] variants)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_media_processing_policy_json = json_set(
                         COALESCE(w_media_processing_policy_json, '{}'),
                         '$.thumbnails',
                         json_object(
                             'generateOnUpload', json($generateOnUpload),
                             'variants', json($variants)
                         )
                     )
                     WHERE w_id = $workspaceId
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$generateOnUpload", generateOnUpload ? "true" : "false")
            .WithParameter("$variants", Json.Serialize(variants))
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update Workspace#{WorkspaceId} thumbnails policy because Workspace was not found.",
                workspace.Id);

            return ResultCode.NotFound;
        }

        Log.Information(
            "Workspace#{WorkspaceId} thumbnails policy was set to generateOnUpload={GenerateOnUpload}, variants={Variants}",
            workspace.Id,
            generateOnUpload,
            variants);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
