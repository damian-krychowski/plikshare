using Microsoft.Data.Sqlite;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Files.AiConversation;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.Preview.Comment;
using PlikShare.Files.Preview.GetDetails.Contracts;
using PlikShare.Integrations.Aws.Textract;
using PlikShare.Integrations.Aws.Textract.Id;
using PlikShare.Integrations.Aws.Textract.Jobs;
using PlikShare.Integrations.Id;
using PlikShare.Users.UserIdentityResolver;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Preview.GetDetails;

public class GetFilePreviewDetailsQuery(
    PlikShareAiDb plikShareAiDb,
    PlikShareDb plikShareDb,
    UserIdentityResolver userIdentityResolver)
{
    public GetFilePreviewDetailsResponseDto Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        IUserIdentity userIdentity,
        FilePreviewDetailsField[] requestedFields)
    {
        var shouldGetAll = requestedFields.Length == 0;

        using var connection = plikShareDb.OpenConnection();

        var areArtifactsNeeded = shouldGetAll
                                 || requestedFields.Contains(FilePreviewDetailsField.Note)
                                 || requestedFields.Contains(FilePreviewDetailsField.Comments)
                                 || requestedFields.Contains(FilePreviewDetailsField.AiConversations);

        var areDependentFilesNeeded = shouldGetAll
                                      || requestedFields.Contains(FilePreviewDetailsField.Attachments)
                                      || requestedFields.Contains(FilePreviewDetailsField.TextractResultFiles);

        var (artifacts, resolvedIdentities) = areArtifactsNeeded
            ? GetArtifacts(
                workspace: workspace,
                fileExternalId: fileExternalId,
                connection: connection)
            : ([], UserIdentityResolver.BulkResult.Empty);


        var note = shouldGetAll || requestedFields.Contains(FilePreviewDetailsField.Note)
            ? TryGetNote(
                artifacts,
                resolvedIdentities)
            : null;

        var comments = shouldGetAll || requestedFields.Contains(FilePreviewDetailsField.Comments)
            ? GetComments(
                artifacts,
                resolvedIdentities)
            : null;

        var dependentFiles = areDependentFilesNeeded
            ? GetDependentFiles(
                workspace,
                fileExternalId,
                userIdentity,
                connection)
            : [];

        var textractResultFiles = shouldGetAll || requestedFields.Contains(FilePreviewDetailsField.TextractResultFiles)
            ? GetTextractResultFiles(
                workspace, 
                fileExternalId, 
                dependentFiles,
                userIdentity, 
                connection)
            : null;

        var attachmentFiles = shouldGetAll || requestedFields.Contains(FilePreviewDetailsField.Attachments)
            ? GetAttachmentFiles(
                workspace,
                fileExternalId,
                dependentFiles,
                userIdentity,
                connection)
            : null;

        var pendingTextractJobs = shouldGetAll || requestedFields.Contains(FilePreviewDetailsField.PendingTextractJobs)
            ? GetPendingTextractJobs(
                workspace, 
                fileExternalId, 
                connection)
            : null;

        var aiConversations = shouldGetAll || requestedFields.Contains(FilePreviewDetailsField.AiConversations)
            ? GetAiConversations(
                artifacts,
                resolvedIdentities)
            : null;
        
        return new GetFilePreviewDetailsResponseDto
        {
            Note = note,
            Comments = comments,
            TextractResultFiles = textractResultFiles,
            PendingTextractJobs = pendingTextractJobs,
            AiConversations = aiConversations,
            Attachments = attachmentFiles
        };
    }

    private static List<FilePreviewPendingTextractJob> GetPendingTextractJobs(
        WorkspaceContext workspace, 
        FileExtId fileExternalId,
        SqliteConnection connection)
    {
        var pendingTextractJobs = connection
            .Cmd(
                sql: @"
                    SELECT 
                        itj_external_id,
                        itj_status,
                        itj_definition
                    FROM itj_integrations_textract_jobs
                    INNER JOIN fi_files
                        ON fi_id = itj_original_file_id
                    WHERE
                        itj_original_workspace_id = $workspaceId
                        AND fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId
                    ORDER BY itj_id ASC
                ",
                readRowFunc: reader => new FilePreviewPendingTextractJob
                {
                    ExternalId = reader.GetExtId<TextractJobExtId>(0),
                    Status = reader.GetEnum<TextractJobStatus>(1),
                    Features = reader.GetFromJson<TextractJobDefinitionEntity>(2).Features
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        return pendingTextractJobs;
    }

    private static List<DependentFile> GetDependentFiles(
        WorkspaceContext workspace, 
        FileExtId fileExternalId, 
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {
        var dependentFiles = connection
            .Cmd(
                sql: @"
                    SELECT
                        child_fi.fi_id,
                        child_fi.fi_external_id,
                        child_fi.fi_name,
                        child_fi.fi_extension,
                        child_fi.fi_size_in_bytes,
                        child_fi.fi_metadata,
                        (
							child_fi.fi_uploader_identity_type = $uploaderIdentityType 
							AND child_fi.fi_uploader_identity =  $uploaderIdentity
						) AS fi_was_uploaded_by_user
                    FROM fi_files AS child_fi
                    INNER JOIN fi_files AS parent_fi
                        ON parent_fi.fi_id = child_fi.fi_parent_file_id
                    WHERE
                        child_fi.fi_workspace_id = $workspaceId
                        AND parent_fi.fi_workspace_id = $workspaceId
                        AND parent_fi.fi_external_id = $fileExternalId
                    ORDER BY child_fi.fi_id DESC
                ",
                readRowFunc: reader => new DependentFile
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<FileExtId>(1),
                    Name = reader.GetString(2),
                    Extension = reader.GetString(3),
                    SizeInBytes = reader.GetInt64(4),
                    Metadata = reader.GetFromJsonOrNull<FileMetadata>(5),
                    WasUploadedByUser = reader.GetBoolean(6)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .WithParameter("$uploaderIdentityType", userIdentity.IdentityType)
            .WithParameter("$uploaderIdentity", userIdentity.Identity)
            .Execute();

        return dependentFiles;
    }

    private List<FilePreviewAiConversation> GetAiConversations(
        List<Artifact> artifacts,
        UserIdentityResolver.BulkResult resolvedIdentities)
    {
        var rawAiConversations = artifacts
            .Where(a => a.Type == FileArtifactType.AiConversation)
            .ToList();

        var aiConversations = rawAiConversations
            .Select(rawAi => new
            {
                Artifact = rawAi,
                AiConversationExternalId = Json.Deserialize<FileAiConversationArtifactEntity>(rawAi.ContentString)!.AiConversationExternalId
            })
            .ToList();

        using var connection = plikShareAiDb.OpenConnection();

        var aiConversationEntities = connection
            .Cmd(
                sql: @"
                    SELECT 
                        aic_id,
                        aic_external_id,
                        aic_integration_external_id,
                        aic_is_waiting_for_ai_response,
                        aic_name,
                        (
                            SELECT MAX(aim_conversation_counter)                            
                            FROM aim_ai_messages
                            WHERE aim_ai_conversation_id = aic_id
                        ) AS aic_conversation_counter            
                    FROM aic_ai_conversations
                    WHERE aic_external_id IN (
                        SELECT value FROM json_each($externalIds)
                    )
                ",
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<AiConversationExtId>(1),
                    IntegrationExternalId = reader.GetExtId<IntegrationExtId>(2),
                    IsWaitingForAiResponse = reader.GetBoolean(3),
                    Name = reader.GetStringOrNull(4),
                    ConversationCounter = reader.GetInt32(5)
                })
            .WithJsonParameter("$externalIds", aiConversations.Select(x => x.AiConversationExternalId.Value).ToList())
            .Execute()
            .ToDictionary(
                keySelector: x => x.ExternalId,
                elementSelector: x => x);


        return aiConversations
            .SelectMany(conversation =>
            {
                if (aiConversationEntities.TryGetValue(conversation.AiConversationExternalId, out var entity))
                {
                    return
                    [
                        new FilePreviewAiConversation
                        {
                            FileArtifactExternalId = conversation.Artifact.ExternalId,
                            AiConversationExternalId = entity.ExternalId,
                            AiIntegrationExternalId = entity.IntegrationExternalId,
                            CreatedAt = conversation.Artifact.CreatedAt,
                            CreatedBy = resolvedIdentities
                                .GetOrThrow(conversation.Artifact.Owner)
                                .DisplayText,
                            IsWaitingForAiResponse = entity.IsWaitingForAiResponse,
                            Name = entity.Name,
                            ConversationCounter = entity.ConversationCounter
                        }
                    ];
                }

                return Array.Empty<FilePreviewAiConversation>();
            })
            .ToList();
    }

    private static List<FilePreviewCommentDto> GetComments(
        List<Artifact> artifacts, 
        UserIdentityResolver.BulkResult resolvedIdentities)
    {
        var rawComments = artifacts
            .Where(a => a.Type == FileArtifactType.Comment)
            .ToList();

        return rawComments
            .Select(rc =>
            {
                var commentContent = Json.Deserialize<CommentContentEntity>(
                    json: rc.ContentString);

                return new FilePreviewCommentDto
                {
                    ContentJson = commentContent!.ContentJson,
                    WasEdited = commentContent.WasEdited,

                    CreatedAt = rc.CreatedAt,
                    CreatedBy = resolvedIdentities
                        .GetOrThrow(rc.Owner)
                        .DisplayText,
                    ExternalId = rc.ExternalId
                };
            })
            .ToList();
    }

    private static FilePreviewNoteDto? TryGetNote(
        List<Artifact> artifacts, 
        UserIdentityResolver.BulkResult resolvedIdentities)
    {
        var rawNote = artifacts
            .FirstOrDefault(a => a.Type == FileArtifactType.Note);

        return rawNote is null
            ? null
            : new FilePreviewNoteDto
            {
                ChangedAt = rawNote.CreatedAt,
                ChangedBy = resolvedIdentities
                    .GetOrThrow(rawNote.Owner)
                    .DisplayText,
                ContentJson = rawNote.ContentString
            };
    }

    private (List<Artifact> Artifacts, UserIdentityResolver.BulkResult ResolvedIdentities) GetArtifacts(
        WorkspaceContext workspace, 
        FileExtId fileExternalId, 
        SqliteConnection connection)
    {
        var artifacts = connection
            .Cmd(
                sql: @"
                    SELECT
                        fa_external_id,
                        fa_content,
                        fa_created_at,
                        fa_owner_identity_type,
                        fa_owner_identity,
                        fa_type
                    FROM fa_file_artifacts
                    INNER JOIN fi_files
                        ON fi_id = fa_file_id
                        AND fi_workspace_id = $workspaceId
                    WHERE
                        fi_external_id = $fileExternalId
                        AND fa_workspace_id = $workspaceId
                    ORDER BY fa_id ASC
                ",
                readRowFunc: reader => new Artifact
                {
                    ExternalId = reader.GetExtId<FileArtifactExtId>(0),
                    ContentString = reader.GetStringFromBlob(1),
                    CreatedAt = reader.GetDateTimeOffset(2),
                    Owner = new GenericUserIdentity(
                        IdentityType: reader.GetString(3),
                        Identity: reader.GetString(4)),
                    Type = reader.GetEnum<FileArtifactType>(5)
                })
            .WithParameter("$workspaceId", workspace.Id)
            .WithParameter("$fileExternalId", fileExternalId.Value)
            .Execute();

        var resolvedIdentities = userIdentityResolver
            .Resolve(artifacts.Select(a => a.Owner).ToList());

        return (artifacts, resolvedIdentities);
    }

    private static List<FilePreviewTextractResultFile> GetTextractResultFiles(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        List<DependentFile> dependentFiles,
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {
        var textractResults = new List<FilePreviewTextractResultFile>();

        foreach (var dependentFile in dependentFiles)
        {
            if(dependentFile.Metadata is not TextractResultFileMetadata textractMetadata)
                continue;

            textractResults.Add(new FilePreviewTextractResultFile
            {
                ExternalId = dependentFile.ExternalId,
                Extension = dependentFile.Extension,
                Name = dependentFile.Name,
                SizeInBytes = dependentFile.SizeInBytes,
                WasUploadedByUser = dependentFile.WasUploadedByUser,

                Features = textractMetadata.Features
            });
        }

        return textractResults;
    }

    private static List<FilePreviewAttachmentFile> GetAttachmentFiles(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        List<DependentFile> dependentFiles,
        IUserIdentity userIdentity,
        SqliteConnection connection)
    {
        var results = new List<FilePreviewAttachmentFile>();

        foreach (var dependentFile in dependentFiles)
        {
            if (dependentFile.Metadata is not null)
                continue;

            results.Add(new FilePreviewAttachmentFile
            {
                ExternalId = dependentFile.ExternalId,
                Extension = dependentFile.Extension,
                Name = dependentFile.Name,
                SizeInBytes = dependentFile.SizeInBytes,
                WasUploadedByUser = dependentFile.WasUploadedByUser
            });
        }

        return results;
    }

    private class Artifact
    {
        public required FileArtifactExtId ExternalId { get; init; }
        public required string ContentString { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required IUserIdentity Owner { get; init; }
        public required FileArtifactType Type { get; init; }
    }

    private class DependentFile
    {
        public required int Id {get; init;}
        public required FileExtId ExternalId {get;init;}
        public required string Name {get;init;}
        public required string Extension {get;init;}
        public required long SizeInBytes { get; init; }
        public required FileMetadata? Metadata { get; init; }
        public required bool WasUploadedByUser { get; init; }
    }
}