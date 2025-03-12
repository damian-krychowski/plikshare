using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.AiConversation;
using PlikShare.Files.Artifacts;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.ArtificialIntelligence.GetFileArtifact
{
    public class GetFileArtifactWithAiConversationQuery(PlikShareDb plikShareDb)
    {
        public FileArtifact? Execute(
            WorkspaceContext workspace,
            FileExtId fileExternalId,
            FileArtifactExtId fileArtifactExternalId)
        {
            using var connection = plikShareDb.OpenConnection();

            return Execute(
                workspace: workspace,
                fileExternalId: fileExternalId,
                fileArtifactExternalId: fileArtifactExternalId,
                connection: connection);
        }

        public FileArtifact? Execute(
            WorkspaceContext workspace,
            FileExtId fileExternalId,
            FileArtifactExtId fileArtifactExternalId,
            SqliteConnection connection)
        {
            var fileArtifact = connection
                .OneRowCmd(
                    sql: @"
                    SELECT 
                        fa_id,
                        fa_content
                    FROM fa_file_artifacts
                    INNER JOIN fi_files
                        ON fi_id = fa_file_id
                    WHERE
                        fa_workspace_id = $workspaceId
                        AND fa_type = $aiConversationType
                        AND fa_external_id = $fileArtifactExternalId
                        AND fi_external_id = $fileExternalId
                        AND fi_workspace_id = $workspaceId
                ",
                    readRowFunc: reader => new FileArtifact
                    {
                        Id = reader.GetInt32(0),
                        AiConversationEntity = Json.Deserialize<FileAiConversationArtifactEntity>(
                            reader.GetString(1))!
                    })
                .WithParameter("$workspaceId", workspace.Id)
                .WithEnumParameter("$aiConversationType", FileArtifactType.AiConversation)
                .WithParameter("$fileArtifactExternalId", fileArtifactExternalId.Value)
                .WithParameter("$fileExternalId", fileExternalId.Value)
                .Execute();

            return fileArtifact.IsEmpty
                ? null
                : fileArtifact.Value;
        }

        public class FileArtifact
        {
            public required int Id { get; init; }
            public required FileAiConversationArtifactEntity AiConversationEntity { get; init; }
        }
    }
}
