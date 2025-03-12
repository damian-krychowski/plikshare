using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Integrations.Aws.Textract.Id;
using PlikShare.Integrations.Aws.Textract.Jobs.InitiateTextractAnalysis;
using PlikShare.Integrations.Aws.Textract.Jobs.UpdateJobTextractFileAndStatus;
using PlikShare.Storages.FileCopying;
using PlikShare.Storages.FileCopying.BulkInitiateCopyFiles;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract.Jobs.StartJob;

public class StartTextractJobOperation(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        TextractFeature[] features,
        IUserIdentity userIdentity,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (workspace.Integrations.Textract is null)
            return new Result(
                Code: ResultCode.TextractIntegrationNotAvailable);

        var fileId = TryGetFileId(
            fileExternalId: fileExternalId,
            workspaceId: workspace.Id);

        if (fileId is null)
            return new Result(
                Code: ResultCode.FileNotFound);

        return await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                workspace: workspace, 
                textractClient: workspace.Integrations.Textract,
                fileId: fileId.Value, 
                features: features, 
                userIdentity: userIdentity, 
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        DbWriteQueue.Context dbWriteContext,
        WorkspaceContext workspace, 
        TextractClient textractClient,
        int fileId, 
        TextractFeature[] features,
        IUserIdentity userIdentity, 
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();
        
        var isTextractFileCopyRequired = workspace.Storage.StorageId != textractClient.StorageId;
        
        try
        {
            int textractJobId;
            var externalId = TextractJobExtId.NewId();

            if (isTextractFileCopyRequired)
            {
                textractJobId = CreateTextractJob(
                    dbWriteContext: dbWriteContext,
                    externalId: externalId,
                    originalWorkspaceId: workspace.Id,
                    originalFileId: fileId,
                    textractWorkspaceId: textractClient.WorkspaceId,
                    textractIntegrationId: textractClient.IntegrationId,
                    textractFileId: null,
                    initialStatus: TextractJobStatus.WaitsForFile,
                    features: features,
                    userIdentity: userIdentity,
                    transaction: transaction);

                var bulkInitiateCopyFilesQueueJob = queue.EnqueueOrThrow(
                    correlationId: correlationId,
                    jobType: BulkInitiateCopyFilesQueueJobType.Value,
                    definition: new BulkInitiateCopyFilesQueueJobDefinition
                    {
                        Files = [new BulkInitiateCopyFilesQueueJobDefinition.FileIdAndHandler
                        {
                            Id = fileId,
                            OnCompleted = new CopyFileQueueOnCompletedActionDefinition
                            {
                                HandlerType = UpdateTextractJobFileAndStatusOnCompletedFileCopyHandler.Type,
                                ActionHandlerDefinition = Json.Serialize(new UpdateTextractJobFileAndStatusOnCompletedFileCopyHandler.Definition
                                {
                                    TextractJobId = textractJobId
                                })
                            }
                        }],
                        SourceWorkspaceId = workspace.Id,
                        DestinationWorkspaceId = textractClient.WorkspaceId,
                        UserIdentity = userIdentity.Identity,
                        UserIdentityType = userIdentity.IdentityType
                    },
                    executeAfterDate: clock.UtcNow,
                    debounceId: null,
                    sagaId: null,
                    dbWriteContext,
                    transaction);

                transaction.Commit();

                Log.Information(
                    "Created Textract job {JobId} with file copy. Source: File {FileId} (Workspace {WorkspaceId}, QueueJob {CopyFileQueueJobId})",
                    textractJobId,
                    fileId,
                    workspace.Id,
                    bulkInitiateCopyFilesQueueJob.Value);
            }
            else
            {

                textractJobId = CreateTextractJob(
                    dbWriteContext: dbWriteContext,
                    externalId: externalId,
                    originalWorkspaceId: workspace.Id,
                    originalFileId: fileId,
                    textractWorkspaceId: workspace.Id,
                    textractIntegrationId: textractClient.IntegrationId,
                    textractFileId: fileId,
                    initialStatus: TextractJobStatus.Pending,
                    features: features,
                    userIdentity: userIdentity,
                    transaction: transaction);

                var queueJob = queue.EnqueueOrThrow(
                    correlationId: correlationId,
                    jobType: InitiateTextractAnalysisQueueJobType.Value,
                    definition: new InitiateTextractAnalysisQueueJobDefinition
                    {
                        TextractJobId = textractJobId
                    },
                    executeAfterDate: clock.UtcNow,
                    debounceId: null,
                    sagaId: null,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                transaction.Commit();

                Log.Information(
                    "Created Textract job {JobId} for File {FileId} (Initiate textract analysis QueueJob {QueueJobId})",
                    textractJobId,
                    fileId,
                    queueJob.Value);
            }

            return new Result(
                Code: ResultCode.Ok,
                TextractJob: new TextractJob(
                    Id: textractJobId,
                    ExternalId: externalId));
        }
        catch (Exception e)
        {
            transaction.Rollback();
            
            Log.Error(
                e,
                "Failed to create Textract job for file. Workspace: {WorkspaceId}, " +
                "File: {FileId}, Features: {Features}, RequiresCopy: {RequiresCopy}",
            workspace.Id,
                fileId,
                string.Join(", ", features),
                isTextractFileCopyRequired);

            throw;
        }
    }

    private int CreateTextractJob(
        DbWriteQueue.Context dbWriteContext,
        TextractJobExtId externalId,
        int originalWorkspaceId,
        int originalFileId,
        int textractWorkspaceId,
        int textractIntegrationId,
        int? textractFileId,
        TextractJobStatus initialStatus,
        TextractFeature[] features,
        IUserIdentity userIdentity,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: """
                     INSERT INTO itj_integrations_textract_jobs (
                         itj_external_id,
                         itj_original_file_id,
                         itj_original_workspace_id,
                         itj_textract_workspace_id,
                         itj_textract_integration_id,
                         itj_textract_file_id,
                         itj_textract_analysis_job_id,
                         itj_status,
                         itj_definition,
                         itj_owner_identity_type,
                         itj_owner_identity,
                         itj_created_at
                     ) VALUES (
                         $externalId,
                         $originalFileId,
                         $originalWorkspaceId,
                         $textractWorkspaceId,
                         $textractIntegrationId,
                         $textractFileId,
                         NULL,
                         $initialStatus,
                         $definition,
                         $ownerIdentityType,
                         $ownerIdentity,
                         $createdAt
                     )
                     RETURNING itj_id
                     """,
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$originalFileId", originalFileId)
            .WithParameter("$originalWorkspaceId", originalWorkspaceId)
            .WithParameter("$textractFileId", textractFileId)
            .WithParameter("$textractWorkspaceId", textractWorkspaceId)
            .WithParameter("$textractIntegrationId", textractIntegrationId)
            .WithEnumParameter("$initialStatus", initialStatus)
            .WithJsonParameter("$definition", new TextractJobDefinitionEntity
            {
                Features = features
            })
            .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
            .WithParameter("$ownerIdentity", userIdentity.Identity)
            .WithParameter("$createdAt", clock.UtcNow)
            .ExecuteOrThrow();
    }

    private int? TryGetFileId(
        FileExtId fileExternalId,
        int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        var file = connection
            .OneRowCmd(
                sql: """
                     SELECT fi_id
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                         AND fi_external_id = $fileExternalId                
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        return file.IsEmpty ? null : file.Value;
    }

    public enum ResultCode
    {
        Ok = 0,
        TextractIntegrationNotAvailable,
        FileNotFound
    }

    public readonly record struct Result(
        ResultCode Code,
        TextractJob TextractJob = default);

    public readonly record struct TextractJob(
        int Id,
        TextractJobExtId ExternalId);
}