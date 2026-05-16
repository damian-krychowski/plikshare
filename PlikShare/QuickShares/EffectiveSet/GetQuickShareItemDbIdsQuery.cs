using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.QuickShares.EffectiveSet;

public class GetQuickShareItemDbIdsQuery(PlikShareDb plikShareDb)
{
    public QuickShareItemDbIds Execute(int quickShareId)
    {
        using var connection = plikShareDb.OpenConnection();

        var fileRows = connection
            .Cmd(
                sql: """
                     SELECT qsi_file_id, qsi_is_excluded
                     FROM qsi_quick_share_items
                     WHERE qsi_quick_share_id = $quickShareId
                         AND qsi_file_id IS NOT NULL
                     """,
                readRowFunc: reader => (Id: reader.GetInt32(0), IsExcluded: reader.GetBoolean(1)))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        var folderRows = connection
            .Cmd(
                sql: """
                     SELECT qsi_folder_id, qsi_is_excluded
                     FROM qsi_quick_share_items
                     WHERE qsi_quick_share_id = $quickShareId
                         AND qsi_folder_id IS NOT NULL
                     """,
                readRowFunc: reader => (Id: reader.GetInt32(0), IsExcluded: reader.GetBoolean(1)))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        return new QuickShareItemDbIds(
            SelectedFileIds: fileRows.Where(r => !r.IsExcluded).Select(r => r.Id).ToArray(),
            ExcludedFileIds: fileRows.Where(r => r.IsExcluded).Select(r => r.Id).ToArray(),
            SelectedFolderIds: folderRows.Where(r => !r.IsExcluded).Select(r => r.Id).ToArray(),
            ExcludedFolderIds: folderRows.Where(r => r.IsExcluded).Select(r => r.Id).ToArray());
    }
}

public sealed record QuickShareItemDbIds(
    int[] SelectedFileIds,
    int[] ExcludedFileIds,
    int[] SelectedFolderIds,
    int[] ExcludedFolderIds);
