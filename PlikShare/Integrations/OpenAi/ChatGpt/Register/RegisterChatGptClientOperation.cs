using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Integrations.OpenAi.ChatGpt.Register;

public class RegisterChatGptClientOperation(
    PlikShareDb plikShareDb,
    WorkspaceCache workspaceCache,
    IMasterDataEncryption masterDataEncryption,
    ChatGptClientStore clientStore)
{
    public void ExecuteOrThrow(int integrationId)
    {
        using var connection = plikShareDb.OpenConnection();

        var integration = connection
            .OneRowCmd(
                sql: @"
                    SELECT
                        i_external_id,
                        i_name,
                        i_details_encrypted,
                        w_id,
                        w_storage_id
                    FROM i_integrations
                    INNER JOIN w_workspaces
                        ON w_id = i_workspace_id
                    WHERE
                        i_is_active = TRUE
                        AND i_id = $integrationId
                        AND i_type = $integrationType
                    ORDER BY 1 DESC                    
                ",
                readRowFunc: reader => new
                {
                    ExternalId = reader.GetExtId<IntegrationExtId>(0),
                    Name = reader.GetString(1),
                    Details = masterDataEncryption.DecryptJson<ChatGptDetails>(
                        versionedEncryptedBytes: reader.GetFieldValue<byte[]>(2)),
                    WorkspaceId = reader.GetInt32(3),
                    StorageId = reader.GetInt32(4)
                })
            .WithParameter("$integrationId", integrationId)
            .WithEnumParameter("$integrationType", IntegrationType.OpenaiChatgpt)
            .ExecuteOrThrow();

        clientStore.RegisterClient(new ChatGptClient(
            workspaceCache: workspaceCache,
            apiKey: integration.Details.ApiKey,
            integrationId: integrationId,
            workspaceId: integration.WorkspaceId,
            storageId: integration.StorageId,
            externalId: integration.ExternalId,
            name: integration.Name));
    }
}