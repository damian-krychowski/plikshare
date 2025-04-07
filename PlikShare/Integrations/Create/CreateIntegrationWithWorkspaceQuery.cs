using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.Create;
using PlikShare.Workspaces.Id;
using Serilog;

namespace PlikShare.Integrations.Create;

public class CreateIntegrationWithWorkspaceQuery(
    DbWriteQueue dbWriteQueue,
    CreateWorkspaceQuery createWorkspaceQuery,
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
        DbWriteQueue.Context dbWriteContext,
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

            var workspaceResult = createWorkspaceQuery.ExecuteTransaction(
                dbWriteContext: dbWriteContext,
                storageExternalId: details.StorageExternalId,
                ownerId: ownerId,
                name: workspaceName,
                maxSizeInBytes: null,
                maxTeamMembers: null,
                correlationId: correlationId,
                transaction: transaction);

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
        StorageNotFound
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