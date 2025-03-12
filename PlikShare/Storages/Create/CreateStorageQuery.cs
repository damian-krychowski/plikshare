using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using Serilog;

namespace PlikShare.Storages.Create;

public class CreateStorageQuery(
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory,
    DbWriteQueue dbWriteQueue)
{
    public async Task<Result> Execute(
        string name,
        string storageType,
        string detailsJson,
        StorageEncryptionType encryptionType,
        StorageManagedEncryptionDetails? encryptionDetails,
        CancellationToken cancellationToken)
    {
        var derivedEncryption = await masterDataEncryptionBufferedFactory.Take(
            cancellationToken: cancellationToken);

        return await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                name,
                storageType,
                detailsJson,
                encryptionType,
                encryptionDetails,
                derivedEncryption),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        string name,
        string storageType,
        string detailsJson,
        StorageEncryptionType encryptionType,
        StorageManagedEncryptionDetails? encryptionDetails,
        IDerivedMasterDataEncryption derivedEncryption)
    {
        try
        {
            var storageExternalId = StorageExtId.NewId();

            var storageId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO s_storages(
                             s_external_id,
                             s_type,
                             s_name,
                             s_details_encrypted,
                             s_encryption_type,
                             s_encryption_details_encrypted
                         ) VALUES (
                             $externalId,
                             $type,
                             $name,
                             $details,
                             $encryptionType,
                             $encryptionDetails
                         ) 
                         RETURNING s_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", storageExternalId.Value)
                .WithParameter("$type", storageType)
                .WithParameter("$name", name)
                .WithParameter("$details", derivedEncryption.Encrypt(detailsJson))
                .WithParameter("$encryptionType", encryptionType.ToDbValue())
                .WithParameter("$encryptionDetails", encryptionDetails is null
                    ? null
                    : derivedEncryption.EncryptJson(encryptionDetails))
                .ExecuteOrThrow();


            Log.Information("Storage#{StorageId} '{StorageName}' of type {StorageType} with ExternalId '{StorageExternalId}' was created.",
                storageId,
                name,
                storageType,
                storageExternalId);

            return new Result(
                Code: ResultCode.Ok,
                StorageId: storageId,
                StorageExternalId: storageExternalId);
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "s_storages", columnName: "s_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
            }

            Log.Error(e, "Something went wrong while creating {StorageType} storage '{StorageName}'",
                storageType,
                name);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while creating {StorageType} storage '{StorageName}'",
                storageType,
                name);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok,
        NameNotUnique
    }

    public readonly record struct Result(
        ResultCode Code,
        int StorageId = 0,
        StorageExtId StorageExternalId = default);
}