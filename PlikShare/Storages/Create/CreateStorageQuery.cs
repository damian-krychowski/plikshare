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
    UpsertStorageEncryptionKeyQuery upsertStorageEncryptionKeyQuery,
    DbWriteQueue dbWriteQueue)
{
    public async Task<Result> Execute(
        string name,
        string storageType,
        string detailsJson,
        StorageEncryptionType encryptionType,
        StorageEncryptionDetails? encryptionDetails,
        OwnerEncryptionKeyData[] ownerKeyDataList,
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
                ownerKeyDataList,
                derivedEncryption),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        string name,
        string storageType,
        string detailsJson,
        StorageEncryptionType encryptionType,
        StorageEncryptionDetails? encryptionDetails,
        OwnerEncryptionKeyData[] ownerKeyDataList,
        IDerivedMasterDataEncryption derivedEncryption)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

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
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", storageExternalId.Value)
                .WithParameter("$type", storageType)
                .WithParameter("$name", name)
                .WithParameter("$details", derivedEncryption.Encrypt(detailsJson))
                .WithParameter("$encryptionType", encryptionType.ToDbValue())
                .WithParameter("$encryptionDetails", encryptionDetails.EncryptJson(derivedEncryption))
                .ExecuteOrThrow();

            foreach (var ownerKeyData in ownerKeyDataList)
            {
                upsertStorageEncryptionKeyQuery.ExecuteTransaction(
                    dbWriteContext: dbWriteContext,
                    storageId: storageId,
                    userId: ownerKeyData.UserId,
                    storageDekVersion: 0,
                    wrappedStorageDek: ownerKeyData.WrappedStorageDek,
                    wrappedByUserId: ownerKeyData.UserId,
                    transaction: transaction);
            }

            transaction.Commit();

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
            transaction.Rollback();

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
            transaction.Rollback();

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

public record OwnerEncryptionKeyData(
    int UserId,
    byte[] WrappedStorageDek);