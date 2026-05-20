using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Storages.Entities;
using PlikShare.Storages.Id;
using PlikShare.Trash;
using Serilog;

namespace PlikShare.Storages.UpdateDefaultTrashPolicy;

public class UpdateStorageDefaultTrashPolicyQuery(DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        StorageExtId storageExternalId,
        TrashPolicy policy,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                storageExternalId: storageExternalId,
                policy: policy),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        StorageExtId storageExternalId,
        TrashPolicy policy)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE s_storages
                     SET s_default_trash_policy_json = $policyJson
                     WHERE s_external_id = $externalId
                     RETURNING s_name, s_type
                     """,
                readRowFunc: reader => new StorageRow(
                    Name: reader.GetString(0),
                    Type: reader.GetEnum<StorageType>(1)))
            .WithParameter("$policyJson", Json.Serialize(policy))
            .WithParameter("$externalId", storageExternalId.Value)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not update Storage '{StorageExternalId}' default trash policy because Storage was not found.",
                storageExternalId);

            return new Result(Code: ResultCode.NotFound);
        }

        Log.Information("Storage '{StorageExternalId}' default trash policy was updated to '{Policy}'",
            storageExternalId,
            Json.Serialize(policy));

        return new Result(
            Code: ResultCode.Ok,
            Name: result.Value.Name,
            Type: result.Value.Type);
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        string? Name = null,
        StorageType? Type = null);

    private readonly record struct StorageRow(string Name, StorageType Type);
}
