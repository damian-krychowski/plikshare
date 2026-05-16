using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Id;
using PlikShare.QuickShares.List.Contracts;
using PlikShare.Workspaces.Cache;

namespace PlikShare.QuickShares.List;

public class GetQuickSharesQuery(
    PlikShareDb plikShareDb,
    QuickShareUrlBuilder urlBuilder)
{
    public GetQuickSharesResponseDto Execute(WorkspaceContext workspace)
    {
        using var connection = plikShareDb.OpenConnection();

        var items = connection
            .Cmd(
                sql: """
                     SELECT
                         qs.qs_external_id,
                         qs.qs_access_code,
                         qs.qs_name,
                         qs.qs_created_at,
                         qs.qs_expires_at,
                         qs.qs_password_hash IS NOT NULL AS qs_has_password,
                         qs.qs_max_downloads,
                         qs.qs_downloads_count,
                         qs.qs_mode,
                         qs.qs_allow_individual_file_download,
                         qs.qs_last_accessed_at,
                         (
                             SELECT COUNT(*)
                             FROM qsi_quick_share_items
                             WHERE qsi_quick_share_id = qs.qs_id
                                 AND qsi_file_id IS NOT NULL
                                 AND qsi_is_excluded = FALSE
                         ) AS qs_selected_files_count,
                         (
                             SELECT COUNT(*)
                             FROM qsi_quick_share_items
                             WHERE qsi_quick_share_id = qs.qs_id
                                 AND qsi_folder_id IS NOT NULL
                                 AND qsi_is_excluded = FALSE
                         ) AS qs_selected_folders_count,
                         (
                             SELECT COUNT(*)
                             FROM qsi_quick_share_items
                             WHERE qsi_quick_share_id = qs.qs_id
                                 AND qsi_file_id IS NOT NULL
                                 AND qsi_is_excluded = TRUE
                         ) AS qs_excluded_files_count,
                         (
                             SELECT COUNT(*)
                             FROM qsi_quick_share_items
                             WHERE qsi_quick_share_id = qs.qs_id
                                 AND qsi_folder_id IS NOT NULL
                                 AND qsi_is_excluded = TRUE
                         ) AS qs_excluded_folders_count
                     FROM qs_quick_shares qs
                     WHERE qs.qs_workspace_id = $workspaceId
                     ORDER BY qs.qs_id DESC
                     """,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<QuickShareExtId>(0);
                    var accessCode = reader.GetStringOrNull(1);

                    var accessCodeStatus = accessCode is not null
                        ? "available"
                        : "sealed";

                    var url = accessCode is not null
                        ? urlBuilder.BuildUrl(accessCode)
                        : null;

                    return new GetQuickSharesItemDto(
                        ExternalId: externalId,
                        Name: reader.GetString(2),
                        CreatedAt: reader.GetFieldValue<DateTimeOffset>(3),
                        ExpiresAt: reader.GetDateTimeOffsetOrNull(4),
                        HasPassword: reader.GetBoolean(5),
                        MaxDownloads: reader.GetInt32OrNull(6),
                        DownloadsCount: reader.GetInt32(7),
                        Mode: reader.GetEnum<QuickShareMode>(8),
                        AllowIndividualFileDownload: reader.GetBoolean(9),
                        LastAccessedAt: reader.GetDateTimeOffsetOrNull(10),
                        AccessCodeStatus: accessCodeStatus,
                        Url: url,
                        SelectedFilesCount: reader.GetInt32(11),
                        SelectedFoldersCount: reader.GetInt32(12),
                        ExcludedFilesCount: reader.GetInt32(13),
                        ExcludedFoldersCount: reader.GetInt32(14));
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return new GetQuickSharesResponseDto(Items: items);
    }
}
