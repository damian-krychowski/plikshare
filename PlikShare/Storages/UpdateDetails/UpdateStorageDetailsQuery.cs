using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Entities;
using PlikShare.Storages.Id;
using PlikShare.Trash;
using Serilog;

namespace PlikShare.Storages.UpdateDetails;

public class UpdateStorageDetailsQuery(
    IMasterDataEncryption masterDataEncryption,
    DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        StorageExtId externalId,
        StorageType storageType,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId,
                storageType: storageType,
                detailsJson: detailsJson),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        StorageExtId externalId,
        StorageType storageType,
        string detailsJson)
    {
        var storage = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE s_storages
                     SET 
                         s_details_encrypted = $details
                     WHERE 
                         s_external_id = $externalId
                         AND s_type = $type
                     RETURNING
                         s_id,
                         s_encryption_type,
                         s_encryption_details_encrypted,
                         s_name,
                         s_default_trash_policy
                     """,
                readRowFunc: reader =>
                {
                    var storageId = reader.GetInt32(0);

                    var encryptionType = StorageEncryptionExtensions.FromDbValue(
                        dbValue: reader.GetStringOrNull(1));

                    return new StorageData(
                        Id: storageId,
                        EncryptionType: encryptionType,
                        EncryptionDetailsEncrypted: encryptionType == StorageEncryptionType.None
                            ? null
                            : reader.GetFieldValue<byte[]>(2),
                        Name: reader.GetString(3),
                        DefaultTrashPolicy: reader.GetFromJson<TrashPolicy>(4));
                })
            .WithParameter("$externalId", externalId.Value)
            .WithEnumParameter("$type", storageType)
            .WithParameter("$details", masterDataEncryption.EncryptString(detailsJson))
            .Execute();

        Log.Information("Storage '{StorageExternalId}' details were updated",
            externalId);

        if (storage.IsEmpty)
            return new Result(Code: ResultCode.NotFound);

        return new Result(
            Code: ResultCode.Ok,
            StorageData: storage.Value);
    }

    public readonly record struct Result(
        ResultCode Code,
        StorageData? StorageData = null);

    public enum ResultCode
    {
        Ok,
        NotFound
    }

    public record StorageData(
        int Id,
        StorageEncryptionType EncryptionType,
        byte[]? EncryptionDetailsEncrypted,
        string Name,
        TrashPolicy DefaultTrashPolicy);

}