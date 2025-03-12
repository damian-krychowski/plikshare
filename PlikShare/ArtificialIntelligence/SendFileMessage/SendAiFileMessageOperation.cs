using Microsoft.Data.Sqlite;
using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Cache;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.ArtificialIntelligence.SendFileMessage.Contracts;
using PlikShare.ArtificialIntelligence.SendFileMessage.QueueJob;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.AiConversation;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.ArtificialIntelligence.SendFileMessage;

public class SendAiFileMessageOperation(
    AiConversationCache aiConversationCache,
    PlikShareDb plikShareDb,
    AiDbWriteQueue aiDbWriteQueue,
    DbWriteQueue dbWriteQueue,
    IClock clock,
    IQueue queue,
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory)
{
    public async Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        SendAiFileMessageRequestDto request,
        IUserIdentity userIdentity,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (!HasUserRightsToAllIncludes(workspace, request.Includes))
            return ResultCode.FileNotFound;

        var aiConversationContext = await aiConversationCache.TryGetAiConversation(
            externalId: request.ConversationExternalId,
            cancellationToken: cancellationToken);

        var aiMessage = await SaveAiMessage(
            request,
            userIdentity,
            aiConversationContext,
            cancellationToken);

        if (aiMessage.WasCounterStale)
            return ResultCode.StaleCounter;

        try
        {
            await StoreConversationAsArtifactAndTriggerQueue(
                workspaceId: workspace.Id, 
                fileExternalId: fileExternalId,
                fileArtifactExternalId: request.FileArtifactExternalId,
                aiConversationExternalId: request.ConversationExternalId,
                aiMessageExternalId: request.MessageExternalId,
                userIdentity: userIdentity, 
                correlationId: correlationId, 
                cancellationToken: cancellationToken);

            return ResultCode.Ok;
        }
        catch (FileNotFoundDomainException)
        {
            await CleanUpAiMessage(
                aiMessage: aiMessage,
                cancellationToken: cancellationToken);

            return ResultCode.FileNotFound;
        }
        catch (Exception)
        {
            await CleanUpAiMessage(
                aiMessage: aiMessage,
                cancellationToken: cancellationToken);

            throw;
        }
    }

    private bool HasUserRightsToAllIncludes(
        WorkspaceContext workspace,
        List<AiInclude> includes)
    {
        var fileExternalIds = includes
            .Select(i => i switch
            {
                AiCommentsInclude aiCommentsInclude => aiCommentsInclude.ExternalId,
                AiFileInclude aiFileInclude => aiFileInclude.ExternalId,
                AiNotesInclude aiNotesInclude => aiNotesInclude.ExternalId,
                _ => throw new ArgumentOutOfRangeException(nameof(i))
            })
            .Distinct()
            .Select(externalId => externalId.Value)
            .ToList();

        using var connection = plikShareDb.OpenConnection();

        var count = connection
            .OneRowCmd(
                sql: """
                     SELECT COUNT(*)
                     FROM fi_files
                     WHERE fi_workspace_id = $workspaceId
                        AND fi_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$workspaceId", workspace.Id)
            .WithJsonParameter("$externalIds", fileExternalIds)
            .ExecuteOrThrow();

        return count == fileExternalIds.Count;
    }
    
    private Task<AiMessage> SaveAiMessage(
        SendAiFileMessageRequestDto request,
        IUserIdentity userIdentity,
        AiConversationContext? aiConversationContext,
        CancellationToken cancellationToken)
    {
        return aiDbWriteQueue.Execute(
            operationToEnqueue: (context, ct) => ExecuteSaveAiMessage(
                dbWriteContext: context,
                userIdentity: userIdentity,
                aiConversationContext: aiConversationContext,
                request: request,
                cancellationToken: ct),
            cancellationToken: cancellationToken);
    }

    private  async ValueTask<AiMessage> ExecuteSaveAiMessage(
        AiDbWriteQueue.Context dbWriteContext,
        IUserIdentity userIdentity,
        AiConversationContext? aiConversationContext,
        SendAiFileMessageRequestDto request,
        CancellationToken cancellationToken)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var (conversationId, wasConversationCreated) = aiConversationContext is null
                ? (
                    Id: InsertConversation(
                        externalId: request.ConversationExternalId,
                        integrationExternalId: request.AiIntegrationExternalId,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction),

                    WasCreated: true
                )
                : (
                    Id: UpdateConversation(
                        conversationId: aiConversationContext.Id,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction),

                    WasCreated: false
                );

            var topConversationCounter = wasConversationCreated
                ? -1
                : dbWriteContext
                    .OneRowCmd(
                        sql: @"
                            SELECT aim_conversation_counter
                            FROM aim_ai_messages
                            WHERE aim_ai_conversation_id = $conversationId
                            ORDER BY aim_conversation_counter DESC
                            LIMIT 1
                        ",
                        readRowFunc: reader => reader.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$conversationId", conversationId)
                    .ExecuteOrValue(valueIfEmpty: -1);

            var currentCounter = topConversationCounter + 1;

            if (currentCounter != request.ConversationCounter)
            {
                transaction.Rollback();

                return new AiMessage(-1, -1, true);
            }

            var derivedEncryption = aiConversationContext
                ?.DerivedEncryption ?? await masterDataEncryptionBufferedFactory.Take(cancellationToken);

            var messageId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        INSERT INTO aim_ai_messages (
                            aim_external_id,
                            aim_ai_conversation_id,
                            aim_conversation_counter,           
                            aim_message_encrypted,
                            aim_includes_encrypted,
                            aim_ai_model,
                            aim_user_identity_type,
                            aim_user_identity,
                            aim_created_at
                        )
                        VALUES (
                            $externalId,
                            $conversationId,
                            $conversationCounter,
                            $messageEncrypted,
                            $includesEncrypted,
                            $aiModel,
                            $userIdentityType,
                            $userIdentity,
                            $createdAt
                        )
                        RETURNING
                            aim_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", request.MessageExternalId.Value)
                .WithParameter("$conversationId", conversationId)
                .WithParameter("$conversationCounter", currentCounter)
                .WithParameter("$messageEncrypted", derivedEncryption.Encrypt(request.Message))
                .WithParameter("$includesEncrypted", derivedEncryption.EncryptJson(request.Includes))
                .WithParameter("$userIdentityType", userIdentity.IdentityType)
                .WithParameter("$userIdentity", userIdentity.Identity)
                .WithParameter("$createdAt", clock.UtcNow)
                .WithParameter("$aiModel", request.AiModel)
                .ExecuteOrThrow();
            
            transaction.Commit();

            if (wasConversationCreated)
            {
                await aiConversationCache.InvalidateEntry(
                    externalId: request.ConversationExternalId,
                    cancellationToken: cancellationToken);
            }

            Log.Information("AiMessage '{AiMessageExternalId} ({AiMessageId})' with counter {ConversationCounter} was added to AiConversation '{AiConversationExternalId} ({AiConversationId})'.",
                request.MessageExternalId,
                messageId,
                currentCounter,
                request.ConversationExternalId,
                conversationId);

            return new AiMessage(
                MessageId: messageId,
                ConversationId: conversationId,
                WasCounterStale: false);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Something went wrong while saving AiMessage '{AiMessageExternalId}' (Counter: {ConversationCounter}) for conversation '{AiConversationExternalId}'",
                request.MessageExternalId,
                request.ConversationCounter,
                request.ConversationExternalId);

            throw;
        }
    }

    private int InsertConversation(
        AiConversationExtId externalId,
        IntegrationExtId integrationExternalId,
        AiDbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: @"
                        INSERT INTO aic_ai_conversations (
                            aic_external_id,
                            aic_integration_external_id,
                            aic_is_waiting_for_ai_response,
                            aic_name
                        )
                        VALUES (
                            $externalId,
                            $integrationExternalId,
                            TRUE,
                            NULL
                        )
                        RETURNING aic_id
                    ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$externalId", externalId.Value)
            .WithParameter("$integrationExternalId", integrationExternalId.Value)
            .ExecuteOrThrow();
    }

    private int UpdateConversation(
        int conversationId,
        AiDbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction)
    {
        return dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE aic_ai_conversations
                    SET aic_is_waiting_for_ai_response = TRUE
                    WHERE aic_id = $id
                    RETURNING aic_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$id", conversationId)
            .ExecuteOrThrow();
    }

    private Task StoreConversationAsArtifactAndTriggerQueue(
        int workspaceId,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        AiConversationExtId aiConversationExternalId,
        AiMessageExtId aiMessageExternalId,
        IUserIdentity userIdentity,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteStoreConversationAsArtifactAndTriggerQueue(
                dbWriteContext: context,
                workspaceId: workspaceId,
                fileExternalId: fileExternalId,
                fileArtifactExternalId: fileArtifactExternalId,
                aiConversationExternalId: aiConversationExternalId,
                aiMessageExternalId: aiMessageExternalId,
                userIdentity: userIdentity,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteStoreConversationAsArtifactAndTriggerQueue(
        DbWriteQueue.Context dbWriteContext,
        int workspaceId,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        AiConversationExtId aiConversationExternalId,
        AiMessageExtId aiMessageExternalId,
        IUserIdentity userIdentity,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var artifact = InsertConversationArtifactOrThrow(
                dbWriteContext: dbWriteContext,
                workspaceId: workspaceId,
                fileExternalId: fileExternalId,
                fileArtifactExternalId: fileArtifactExternalId,
                aiConversationExternalId: aiConversationExternalId,
                userIdentity: userIdentity,
                transaction: transaction);

            var jobId = queue.EnqueueOrThrow(
                correlationId: correlationId,
                jobType: SendAiMessageQueueJobType.Value,
                definition: new SendAiMessageQueueJobDefinition
                {
                    AiMessageExternalId = aiMessageExternalId
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext,
                transaction);

            transaction.Commit();

            Log.Information("AiConversation '{AiConversationExternalId}' was added to File '{FileExternalId}' as FileArtifact#{FileArtifactId}",
                aiConversationExternalId,
                fileExternalId,
                artifact.Id);
        }
        catch (FileNotFoundDomainException)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Something went wrong while inserting AiConversation '{AiConversationExternalId}' as file artifact for file '{FileExternalId}'",
                aiConversationExternalId,
                fileExternalId);

            throw;
        }
    }

    private ConversationArtifact InsertConversationArtifactOrThrow(
        DbWriteQueue.Context dbWriteContext,
        int workspaceId,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        AiConversationExtId aiConversationExternalId,
        IUserIdentity userIdentity,
        SqliteTransaction transaction)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    INSERT INTO fa_file_artifacts (
                        fa_external_id,
                        fa_workspace_id,
                        fa_file_id,
                        fa_type,
                        fa_content,
                        fa_owner_identity_type,
                        fa_owner_identity,
                        fa_created_at,
                        fa_uniqueness_id
                    )
                    SELECT
                        $externalId,
                        fi_workspace_id,
                        fi_id,
                        $fileArtifactType,
                        $content,
                        $ownerIdentityType,
                        $ownerIdentity,
                        $createdAt,
                        $uniquenessId
                    FROM fi_files
                    WHERE 
                        fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId
                    ON CONFLICT (fa_uniqueness_id)
                    DO UPDATE SET 
                        fa_content = EXCLUDED.fa_content,
                        fa_created_at = EXCLUDED.fa_created_at,
                        fa_owner_identity_type = EXCLUDED.fa_owner_identity_type,
                        fa_owner_identity = EXCLUDED.fa_owner_identity
                    RETURNING
                        fa_id
                ",
                readRowFunc: reader => new ConversationArtifact(
                    Id: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$externalId", fileArtifactExternalId.Value)
            .WithEnumParameter("$fileArtifactType", FileArtifactType.AiConversation)
            .WithBlobParameter("$content", Json.Serialize(new FileAiConversationArtifactEntity
            {
                AiConversationExternalId = aiConversationExternalId
            }))
            .WithParameter("$ownerIdentityType", userIdentity.IdentityType)
            .WithParameter("$ownerIdentity", userIdentity.Identity)
            .WithParameter("$createdAt", clock.UtcNow)
            .WithParameter("$uniquenessId", $"ai_conversation_{aiConversationExternalId}")
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$workspaceId", workspaceId)
            .Execute();

        if (result.IsEmpty)
            throw new FileNotFoundDomainException(
                $"File '{fileExternalId}' was not found");

        return result.Value;
    }

    private Task CleanUpAiMessage(
        AiMessage aiMessage,
        CancellationToken cancellationToken)
    {
        return aiDbWriteQueue.Execute(
            operationToEnqueue: (context, ct) => ExecuteCleanUpAiMessage(
                dbWriteContext: context,
                aiMessage: aiMessage,
                cancellationToken: ct),
            cancellationToken: cancellationToken);
    }

    private async ValueTask ExecuteCleanUpAiMessage(
        AiDbWriteQueue.Context dbWriteContext,
        AiMessage aiMessage,
        CancellationToken cancellationToken)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var deletedMessageConversationId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM aim_ai_messages
                        WHERE aim_id = $id
                        RETURNING aim_ai_conversation_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$id", aiMessage.MessageId)
                .ExecuteOrThrow();

            var otherMessages = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        SELECT COUNT(*)
                        FROM aim_ai_messages
                        WHERE aim_ai_conversation_id = $conversationId
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$conversationId", deletedMessageConversationId)
                .ExecuteOrThrow();

            AiConversationExtId? deletedConversationExtId = null;

            if (otherMessages == 0)
            {
                deletedConversationExtId = dbWriteContext
                    .OneRowCmd(
                        sql: @"
                            DELETE FROM aic_ai_conversations
                            WHERE aic_id = $conversationId
                            RETURNING aic_external_id
                        ",
                        readRowFunc: reader => reader.GetExtId<AiConversationExtId>(0),
                        transaction: transaction)
                    .WithParameter("$conversationId", deletedMessageConversationId)
                    .ExecuteOrThrow();
            }

            transaction.Commit();

            if (deletedConversationExtId is not null)
            {
                await aiConversationCache.InvalidateEntry(
                    externalId: deletedConversationExtId.Value,
                    cancellationToken: cancellationToken);


                Log.Warning("AiMessage#{AiMessageId} and AiConversation{AiConversationId} were deleted to clean up after failed SendAiMessage process.",
                    aiMessage.MessageId, 
                    deletedMessageConversationId);

            }
            else
            {
                Log.Warning("AiMessage#{AiMessageId} was deleted from AiConversation{AiConversationId} to clean up after failed SendAiMessage process.",
                    aiMessage.MessageId,
                    deletedMessageConversationId);
            }
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Console.WriteLine(e);
            throw;
        }
    }

    public enum ResultCode
    {
        Ok,
        FileNotFound,
        StaleCounter
    }

    private readonly record struct AiMessage(
        int MessageId,
        int ConversationId,
        bool WasCounterStale);

    private readonly record struct ConversationArtifact(
        int Id);

    private class FileNotFoundDomainException(string message) : DomainException(message);
}