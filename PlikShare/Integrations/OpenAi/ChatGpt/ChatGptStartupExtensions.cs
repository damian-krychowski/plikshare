using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Integrations.OpenAi.ChatGpt;

public static class ChatGptStartupExtensions
{
    public static void InitializeChatGptIntegrations(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clientStore = app
            .Services
            .GetRequiredService<ChatGptClientStore>();

        var plikShareDb = app
            .Services
            .GetRequiredService<PlikShareDb>();

        var workspaceCache = app
            .Services
            .GetRequiredService<WorkspaceCache>();

        var masterDataEncryption = app
            .Services
            .GetRequiredService<IMasterDataEncryption>();

        using var connection = plikShareDb.OpenConnection();

        var integrations = connection
            .Cmd(
                sql: @"
                    SELECT
                        i_id,
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
                        AND i_type = $integrationType
                    ORDER BY 1 DESC                    
                ",
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<IntegrationExtId>(1),
                    Name = reader.GetString(2),
                    Details = masterDataEncryption.DecryptJson<ChatGptDetails>(
                        versionedEncryptedBytes: reader.GetFieldValue<byte[]>(3)),
                    WorkspaceId = reader.GetInt32(4),
                    StorageId = reader.GetInt32(5)
                })
            .WithEnumParameter("$integrationType", IntegrationType.OpenaiChatgpt)
            .Execute();

        foreach (var integration in integrations)
        {
            clientStore.RegisterClient(new ChatGptClient(
                workspaceCache: workspaceCache,
                apiKey: integration.Details.ApiKey,
                integrationId: integration.Id,
                workspaceId: integration.WorkspaceId,
                storageId: integration.StorageId,
                externalId: integration.ExternalId,
                name: integration.Name));
        }

        if (integrations.Count == 0)
        {
            Log.Information(
                "[INITIALIZATION] No ChatGPT integrations were found. ChatGTP initialization skipped.");
        }
        else
        {
            Log.Information(
                "[INITIALIZATION] ChatGPT Client Store initialization finished (Clients: {ChatGPTClientsCount}).",
                integrations.Count);
        }
    }
}