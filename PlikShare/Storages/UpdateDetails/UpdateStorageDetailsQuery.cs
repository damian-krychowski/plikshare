using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using Serilog;

namespace PlikShare.Storages.UpdateDetails;

public class UpdateStorageDetailsQuery(
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory,
    DbWriteQueue dbWriteQueue)
{
    public Task<Result> Execute(
        StorageExtId externalId,
        string storageType,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: (context, ct) => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId,
                storageType: storageType,
                detailsJson: detailsJson,
                cancellationToken: ct),
            cancellationToken: cancellationToken);
    }

    private async ValueTask<Result> ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        StorageExtId externalId,
        string storageType,
        string detailsJson,
        CancellationToken cancellationToken)
    {
        var derivedEncryption = await masterDataEncryptionBufferedFactory.Take(
            cancellationToken);

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
                         s_encryption_details_encrypted
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
                            : reader.GetFieldValue<byte[]>(2));
                })
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$type", storageType)
            .WithParameter("$details", derivedEncryption.Encrypt(detailsJson))
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
        byte[]? EncryptionDetailsEncrypted);

}