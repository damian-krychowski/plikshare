using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Workspaces.UpdateImageDimensionsPolicy;

public class UpdateWorkspaceImageDimensionsPolicyQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        WorkspaceContext workspace,
        bool extractOnUpload,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace,
                extractOnUpload: extractOnUpload),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        WorkspaceContext workspace,
        bool extractOnUpload)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE w_workspaces
                     SET w_media_processing_policy_json = json_set(
                         COALESCE(w_media_processing_policy_json, '{}'),
                         '$.imageDimensions',
                         json_object('extractOnUpload', json($extractOnUpload))
                     )
                     WHERE w_id = $workspaceId
                     RETURNING w_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$extractOnUpload", extractOnUpload ? "true" : "false")
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning(
                "Could not update Workspace#{WorkspaceId} image-dimensions policy because Workspace was not found.",
                workspace.Id);

            return ResultCode.NotFound;
        }

        Log.Information(
            "Workspace#{WorkspaceId} image-dimensions policy was set to extractOnUpload={ExtractOnUpload}",
            workspace.Id,
            extractOnUpload);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }
}
