using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using Serilog;

namespace PlikShare.Storages.ResetMasterPassword;

public class UpdateStorageEncryptionDetailsQuery(
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory,
    DbWriteQueue dbWriteQueue)
{
    public Task<ResultCode> Execute(
        StorageExtId externalId,
        StorageEncryptionDetails newEncryptionDetails,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: (context, ct) => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId,
                newEncryptionDetails: newEncryptionDetails,
                cancellationToken: ct),
            cancellationToken: cancellationToken);
    }

    private async ValueTask<ResultCode> ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        StorageExtId externalId,
        StorageEncryptionDetails newEncryptionDetails,
        CancellationToken cancellationToken)
    {
        var derivedEncryption = await masterDataEncryptionBufferedFactory.Take(
            cancellationToken);

        var updated = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE s_storages
                     SET
                         s_encryption_details_encrypted = $encryptionDetails
                     WHERE
                         s_external_id = $externalId
                         AND s_encryption_type = $fullType
                     RETURNING s_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$fullType", StorageEncryptionType.Full.ToDbValue())
            .WithParameter("$encryptionDetails", newEncryptionDetails.EncryptJson(derivedEncryption))
            .Execute();

        if (updated.IsEmpty)
            return ResultCode.NotFound;

        Log.Information(
            "Storage '{StorageExternalId}' full-encryption details updated (master password reset).",
            externalId);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok,
        NotFound
    }
}
