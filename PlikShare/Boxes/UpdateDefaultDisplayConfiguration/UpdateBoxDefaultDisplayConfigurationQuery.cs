using PlikShare.Boxes.Cache;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Boxes.UpdateDefaultDisplayConfiguration;

public class UpdateBoxDefaultDisplayConfigurationQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        BoxContext box,
        BoxViewMode viewMode,
        BoxSortMode sortMode,
        BoxSortDirection sortDirection,
        bool thumbnailsEnabled,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                box: box,
                viewMode: viewMode,
                sortMode: sortMode,
                sortDirection: sortDirection,
                thumbnailsEnabled: thumbnailsEnabled),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        BoxContext box,
        BoxViewMode viewMode,
        BoxSortMode sortMode,
        BoxSortDirection sortDirection,
        bool thumbnailsEnabled)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE bo_boxes
                     SET
                         bo_default_view_mode = $viewMode,
                         bo_default_sort_mode = $sortMode,
                         bo_default_sort_direction = $sortDirection,
                         bo_default_thumbnails_enabled = $thumbnailsEnabled
                     WHERE bo_id = $boxId
                     RETURNING bo_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithEnumParameter("$viewMode", viewMode)
            .WithEnumParameter("$sortMode", sortMode)
            .WithEnumParameter("$sortDirection", sortDirection)
            .WithParameter("$thumbnailsEnabled", thumbnailsEnabled)
            .WithParameter("$boxId", box.Id)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Box '{BoxExternalId}' default display configuration because Box was not found.",
                box.ExternalId);

            return ResultCode.BoxNotFound;
        }

        Log.Information("Box '{BoxExternalId}' default display configuration was updated.",
            box.ExternalId);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        BoxNotFound
    }
}
