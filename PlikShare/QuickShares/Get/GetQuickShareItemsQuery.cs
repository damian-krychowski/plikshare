using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.QuickShares.Get.Contracts;

namespace PlikShare.QuickShares.Get;

public class GetQuickShareItemsQuery(PlikShareDb plikShareDb)
{
    public GetQuickShareItemsDto Execute(int quickShareId)
    {
        using var connection = plikShareDb.OpenConnection();

        var fileRows = connection
            .Cmd(
                sql: """
                     SELECT
                         fi.fi_external_id,
                         qsi.qsi_is_excluded
                     FROM qsi_quick_share_items qsi
                     INNER JOIN fi_files fi ON fi.fi_id = qsi.qsi_file_id
                     WHERE
                         qsi.qsi_quick_share_id = $quickShareId
                         AND qsi.qsi_file_id IS NOT NULL
                     """,
                readRowFunc: reader => new ItemRow<FileExtId>(
                    ExternalId: reader.GetExtId<FileExtId>(0),
                    IsExcluded: reader.GetBoolean(1)))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        var folderRows = connection
            .Cmd(
                sql: """
                     SELECT
                         fo.fo_external_id,
                         qsi.qsi_is_excluded
                     FROM qsi_quick_share_items qsi
                     INNER JOIN fo_folders fo ON fo.fo_id = qsi.qsi_folder_id
                     WHERE
                         qsi.qsi_quick_share_id = $quickShareId
                         AND qsi.qsi_folder_id IS NOT NULL
                         AND fo.fo_is_being_deleted = FALSE
                     """,
                readRowFunc: reader => new ItemRow<FolderExtId>(
                    ExternalId: reader.GetExtId<FolderExtId>(0),
                    IsExcluded: reader.GetBoolean(1)))
            .WithParameter("$quickShareId", quickShareId)
            .Execute();

        return new GetQuickShareItemsDto(
            SelectedFiles: fileRows.Where(r => !r.IsExcluded).Select(r => r.ExternalId).ToList(),
            ExcludedFiles: fileRows.Where(r => r.IsExcluded).Select(r => r.ExternalId).ToList(),
            SelectedFolders: folderRows.Where(r => !r.IsExcluded).Select(r => r.ExternalId).ToList(),
            ExcludedFolders: folderRows.Where(r => r.IsExcluded).Select(r => r.ExternalId).ToList());
    }

    private readonly record struct ItemRow<T>(
        T ExternalId,
        bool IsExcluded);
}
