using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.Storages.FileCopying;

public static class CopyFileQueueStartupExtensions
{
    public static void InitializeCopyFileQueue(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var plikShareDb = app.Services.GetRequiredService<PlikShareDb>();

        using var connection = plikShareDb.OpenConnection();
        
        var unlockedQueueJobIds = connection
            .Cmd(
                sql: @"
                    UPDATE cfq_copy_file_queue
                    SET cfq_status = $pendingStatus
                    WHERE cfq_status = $uploadingStatus
                    RETURNING cfq_id
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithEnumParameter("$pendingStatus", CopyFileQueueStatus.Pending)
            .WithEnumParameter("$uploadingStatus", CopyFileQueueStatus.Uploading)
            .Execute();

        if (unlockedQueueJobIds.Any())
        {
            Log.Information("[INITIALIZATION] CopyFileQueue initialization finished. Following jobs were fixed from stale 'Processing' status: {CopyFileQueueJobIds}.",
                unlockedQueueJobIds);
        }
        else
        {
            Log.Information("[INITIALIZATION] CopyFileQueue initialization finished. No stale or blocked queue jobs were found.");
        }
    }

}