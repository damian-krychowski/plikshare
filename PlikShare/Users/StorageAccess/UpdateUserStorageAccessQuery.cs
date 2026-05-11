using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Id;
using PlikShare.Users.Cache;
using Serilog;

namespace PlikShare.Users.StorageAccess;

/// <summary>
/// Replaces a user's storage-access policy: writes <c>u_storage_access_mode</c> and rewrites
/// <c>usa_user_storage_access</c> rows to match the supplied external-id list. Storage external
/// ids are resolved to internal ids inside the same transaction; any id that cannot be resolved
/// causes a rollback and is returned in <see cref="Result.UnknownExternalIds"/>.
/// </summary>
public class UpdateUserStorageAccessQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        UserContext user,
        UserStorageAccessMode mode,
        List<string> storageExternalIds,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                user: user,
                mode: mode,
                storageExternalIds: storageExternalIds),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        UserContext user,
        UserStorageAccessMode mode,
        List<string> storageExternalIds)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var distinctExternalIds = mode == UserStorageAccessMode.All
                ? new List<string>()
                : storageExternalIds.Distinct().ToList();

            var resolvedIds = new List<int>(distinctExternalIds.Count);
            var unknown = new List<string>();

            foreach (var externalId in distinctExternalIds)
            {
                var storageId = TryResolveStorageId(
                    externalId: externalId,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                if (storageId is null)
                    unknown.Add(externalId);
                else
                    resolvedIds.Add(storageId.Value);
            }

            if (unknown.Count > 0)
            {
                transaction.Rollback();
                return new Result(Code.UnknownStorageExternalIds, unknown);
            }

            UpdateUserMode(
                userId: user.Id,
                mode: mode,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            DeleteAllAccessRows(
                userId: user.Id,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            foreach (var storageId in resolvedIds)
            {
                InsertAccessRow(
                    userId: user.Id,
                    storageId: storageId,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            }

            transaction.Commit();

            Log.Information(
                "User#{UserId} storage access updated. Mode: {Mode}. StorageIds: [{StorageIds}].",
                user.Id,
                mode,
                string.Join(", ", resolvedIds));

            return new Result(Code.Ok, []);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while updating storage access for User '{UserId}'",
                user.Id);

            throw;
        }
    }

    private static int? TryResolveStorageId(
        string externalId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     SELECT s_id
                     FROM s_storages
                     WHERE s_external_id = $externalId
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", externalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static void UpdateUserMode(
        int userId,
        UserStorageAccessMode mode,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE u_users
                     SET
                         u_storage_access_mode = $mode,
                         u_concurrency_stamp = $concurrencyStamp
                     WHERE u_id = $userId
                     RETURNING u_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithEnumParameter("$mode", mode)
            .WithParameter("$concurrencyStamp", Guid.NewGuid())
            .WithParameter("$userId", userId)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update User#{userId} StorageAccessMode to '{mode}'.");
        }
    }

    private static void DeleteAllAccessRows(
        int userId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .Cmd(
                sql: """
                     DELETE FROM usa_user_storage_access
                     WHERE usa_user_id = $userId
                     RETURNING usa_storage_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .Execute();
    }

    private static void InsertAccessRow(
        int userId,
        int storageId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction)
    {
        dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO usa_user_storage_access (
                         usa_user_id,
                         usa_storage_id
                     ) VALUES (
                         $userId,
                         $storageId
                     )
                     ON CONFLICT (usa_user_id, usa_storage_id) DO NOTHING
                     RETURNING usa_user_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$userId", userId)
            .WithParameter("$storageId", storageId)
            .Execute();
    }

    public enum Code
    {
        Ok = 0,
        UnknownStorageExternalIds = 1
    }

    public record Result(Code Code, List<string> UnknownExternalIds);
}
