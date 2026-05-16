using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.QuickShares.Cache;
using Serilog;

namespace PlikShare.QuickShares.UpdateSlug;

public class UpdateQuickShareSlugQuery(DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        QuickShareContext quickShare,
        string slug,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                quickShare: quickShare,
                slug: slug),
            cancellationToken: cancellationToken);
    }

    private static ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        QuickShareContext quickShare,
        string slug)
    {
        if (!QuickShareSlug.IsValid(slug))
            return ResultCode.SlugInvalid;

        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE qsh_quick_shares
                         SET qsh_slug = $slug
                         WHERE qsh_id = $quickShareId
                         RETURNING qsh_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$quickShareId", quickShare.Id)
                .WithParameter("$slug", slug)
                .Execute();

            if (result.IsEmpty)
            {
                Log.Warning(
                    "Could not update QuickShare '{ExternalId}' slug to '{Slug}' because it was not found",
                    quickShare.ExternalId,
                    slug);
                return ResultCode.NotFound;
            }

            Log.Information(
                "QuickShare '{ExternalId} ({Id})' slug updated from '{OldSlug}' to '{NewSlug}'",
                quickShare.ExternalId,
                result.Value,
                quickShare.Slug,
                slug);

            return ResultCode.Ok;
        }
        catch (SqliteException exception) when (exception.HasUniqueConstraintFailed(tableName: "qsh_quick_shares", columnName: "qsh_slug"))
        {
            return ResultCode.SlugTaken;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        SlugInvalid,
        SlugTaken
    }
}
