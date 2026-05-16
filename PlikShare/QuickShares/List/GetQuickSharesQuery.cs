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
                         qsh_external_id,
                         qsh_slug,
                         qsh_secret_hash IS NOT NULL AS qsh_has_secret,
                         qsh_name,
                         qsh_created_at,
                         qsh_expires_at,
                         qsh_password_hash IS NOT NULL AS qsh_has_password,
                         qsh_max_downloads,
                         qsh_downloads_count,
                         qsh_mode,
                         qsh_allow_individual_file_download,
                         qsh_last_accessed_at,
                         (
                             SELECT COUNT(*)
                             FROM qshi_quick_share_items
                             WHERE qshi_quick_share_id = qsh_id
                                 AND qshi_file_id IS NOT NULL
                                 AND qshi_is_excluded = FALSE
                         ) AS qsh_selected_files_count,
                         (
                             SELECT COUNT(*)
                             FROM qshi_quick_share_items
                             WHERE qshi_quick_share_id = qsh_id
                                 AND qshi_folder_id IS NOT NULL
                                 AND qshi_is_excluded = FALSE
                         ) AS qsh_selected_folders_count,
                         (
                             SELECT COUNT(*)
                             FROM qshi_quick_share_items
                             WHERE qshi_quick_share_id = qsh_id
                                 AND qshi_file_id IS NOT NULL
                                 AND qshi_is_excluded = TRUE
                         ) AS qsh_excluded_files_count,
                         (
                             SELECT COUNT(*)
                             FROM qshi_quick_share_items
                             WHERE qshi_quick_share_id = qsh_id
                                 AND qshi_folder_id IS NOT NULL
                                 AND qshi_is_excluded = TRUE
                         ) AS qsh_excluded_folders_count
                     FROM qsh_quick_shares
                     WHERE qsh_workspace_id = $workspaceId
                     ORDER BY qsh_id DESC
                     """,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<QuickShareExtId>(0);
                    var slug = reader.GetString(1);
                    var hasSecret = reader.GetBoolean(2);

                    // FE workspaces require a per-share secret token that is never
                    // stored — the owner can only see the full URL once at creation,
                    // so we omit it from the list view in that case.
                    var url = hasSecret ? null : urlBuilder.BuildUrl(slug);

                    return new GetQuickSharesItemDto(
                        ExternalId: externalId,
                        Name: reader.GetString(3),
                        CreatedAt: reader.GetFieldValue<DateTimeOffset>(4),
                        ExpiresAt: reader.GetDateTimeOffsetOrNull(5),
                        HasPassword: reader.GetBoolean(6),
                        MaxDownloads: reader.GetInt32OrNull(7),
                        DownloadsCount: reader.GetInt32(8),
                        Mode: reader.GetEnum<QuickShareMode>(9),
                        AllowIndividualFileDownload: reader.GetBoolean(10),
                        LastAccessedAt: reader.GetDateTimeOffsetOrNull(11),
                        Slug: slug,
                        HasSecret: hasSecret,
                        Url: url,
                        SelectedFilesCount: reader.GetInt32(12),
                        SelectedFoldersCount: reader.GetInt32(13),
                        ExcludedFilesCount: reader.GetInt32(14),
                        ExcludedFoldersCount: reader.GetInt32(15));
                })
            .WithParameter("$workspaceId", workspace.Id)
            .Execute();

        return new GetQuickSharesResponseDto(Items: items);
    }
}
