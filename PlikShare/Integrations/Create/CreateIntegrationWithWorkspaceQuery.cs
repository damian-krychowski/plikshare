using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.Create;
using PlikShare.Workspaces.Encryption;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Integrations.Create;

public class CreateIntegrationWithWorkspaceQuery(
    DbWriteQueue dbWriteQueue,
    CreateWorkspaceQuery createWorkspaceQuery,
    WorkspaceCreationPreparation workspaceCreationPreparation,
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory)
{
    public async Task<Result> Execute<TDetails>(
        string name,
        IntegrationType type,
        TDetails details,
        int ownerId,
        Guid correlationId,
        CancellationToken cancellationToken) where TDetails: IIntegrationWithWorkspace
    {
        // Integrations do not carry a user encryption session, so we cannot build the per-user
        // Workspace DEK wrap that a full-encrypted storage requires. Reject up-front instead of
        // letting the write queue produce a half-formed workspace with no wrap row.
        var storageContext = workspaceCreationPreparation.ResolveStorageContextForRead(
            details.StorageExternalId);
            
        if (storageContext is null)
            return new Result(
                Code: ResultCode.StorageNotFound,
                MissingStorageExternalId: details.StorageExternalId);

        if (storageContext.Value.EncryptionType == StorageEncryptionType.Full)
            return new Result(Code: ResultCode.EncryptedStorageNotSupported);

        var derivedEncryption = await masterDataEncryptionBufferedFactory.Take(
            cancellationToken: cancellationToken);

        return await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                type: type,
                name: name,
                details: details,
                ownerId: ownerId,
                correlationId: correlationId,
                derivedEncryption: derivedEncryption),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation<TDetails>(
        SqliteWriteContext dbWriteContext,
        string name,
        IntegrationType type,
        TDetails details,
        int ownerId,
        Guid correlationId,
        IDerivedMasterDataEncryption derivedEncryption) where TDetails : IIntegrationWithWorkspace
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var externalId = IntegrationExtId.NewId();
            var workspaceName = $"{type.GetWorkspaceNamePrefix()} ({externalId})";

            // Full-encrypted storages are rejected in Execute before we reach this point, so
            // passing null artifacts here is the correct shape for None/Managed.
            var workspaceResult = createWorkspaceQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                storageExternalId: details.StorageExternalId,
                ownerId: ownerId,
                artifacts: null,
                name: workspaceName,
                maxSizeInBytes: null,
                maxTeamMembers: null,
                correlationId: correlationId,
                transaction: transaction);

            if (workspaceResult.Code != CreateWorkspaceQuery.ResultCode.Ok)
            {
                transaction.Rollback();

                return workspaceResult.Code == CreateWorkspaceQuery.ResultCode.StorageNotFound
                    ? new Result(Code: ResultCode.StorageNotFound, MissingStorageExternalId: details.StorageExternalId)
                    : new Result(Code: ResultCode.WorkspaceCreationFailed);
            }

            var integrationId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO i_integrations(
                             i_external_id,
                             i_type,
                             i_name,
                             i_is_active,
                             i_details_encrypted,
                             i_workspace_id
                         ) VALUES (
                             $externalId,
                             $type,
                             $name,
                             FALSE,
                             $details,
                             $workspaceId
                         ) 
                         RETURNING i_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", externalId.Value)
                .WithEnumParameter("$type", type)
                .WithParameter("$name", name)
                .WithParameter("$details", derivedEncryption.EncryptJson(details))
                .WithParameter("$workspaceId", workspaceResult.Workspace.Id)
                .ExecuteOrThrow();

            transaction.Commit();

            Log.Information("Integration#{IntegrationId} '{IntegrationName}' of type {IntegrationType} with ExternalId '{IntegrationExternalId}' was created. " +
                            "Created workspace: '{WorkspaceExternalId}'",
                integrationId,
                name,
                type,
                externalId,
                workspaceResult.Workspace.ExternalId);

            return new Result(
                Code: ResultCode.Ok,
                Integration: new Integration(
                    Id: integrationId,
                    ExternalId: externalId,
                    WorkspaceId: workspaceResult.Workspace.Id,
                    WorkspaceExternalId: workspaceResult.Workspace.ExternalId,
                    WorkspaceName: workspaceName));
        }
        catch (SqliteException e)
        {
            transaction.Rollback();

            if (e.HasUniqueConstraintFailed(
                    tableName: "i_integrations",
                    columnName: "i_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
            }

            if (e.HasForeignKeyFailed())
                return new Result(
                    Code: ResultCode.StorageNotFound,
                    MissingStorageExternalId: details.StorageExternalId);

            if (e.HasNotNullConstraintFailed(tableName: "w_workspaces", columnName: "w_storage_id"))
                return new Result(Code: ResultCode.StorageNotFound);

            Log.Error(e, "Something went wrong while creating {IntegrationType} integration '{IntegrationName}'",
                type,
                name);

            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while creating {IntegrationType} integration '{IntegrationName}'",
                type,
                name);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok,
        NameNotUnique,
        StorageNotFound,
        WorkspaceCreationFailed,
        EncryptedStorageNotSupported
    }

    public readonly record struct Result(
        ResultCode Code,
        Integration Integration = default,
        StorageExtId? MissingStorageExternalId = null);

    public readonly record struct Integration(
        int Id,
        IntegrationExtId ExternalId,
        int WorkspaceId,
        WorkspaceExtId WorkspaceExternalId,
        string WorkspaceName);
}