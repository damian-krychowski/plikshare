using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract;

public static class TextractStartupExtensions
{
    public static void InitializeTextractIntegrations(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var clientStore = app
            .Services
            .GetRequiredService<TextractClientStore>();

        var plikshareDb = app
            .Services
            .GetRequiredService<PlikShareDb>();

        var workspaceCache = app
            .Services
            .GetRequiredService<WorkspaceCache>();

        var masterDataEncryption = app
            .Services
            .GetRequiredService<IMasterDataEncryption>();
        
        using var connection = plikshareDb.OpenConnection();

        var textractIntegrations = connection
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
                        AND i_type = $textractType
                    ORDER BY 1 DESC                    
                ",
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<IntegrationExtId>(1),
                    Name = reader.GetString(2),
                    Details = masterDataEncryption.DecryptJson<AwsTextractDetails>(
                        versionedEncryptedBytes: reader.GetFieldValue<byte[]>(3)),
                    WorkspaceId = reader.GetInt32(4),
                    StorageId = reader.GetInt32(5)
                })
            .WithEnumParameter("$textractType", IntegrationType.AwsTextract)
            .Execute();

        foreach (var textractIntegration in textractIntegrations)
        {
            clientStore.RegisterClient(new TextractClient(
                workspaceCache: workspaceCache,
                awsClient: new AwsTextractClient(
                    accessKey: textractIntegration.Details.AccessKey,
                    secretAccessKey: textractIntegration.Details.SecretAccessKey,
                    region: textractIntegration.Details.Region),
                integrationId: textractIntegration.Id,
                workspaceId: textractIntegration.WorkspaceId,
                storageId: textractIntegration.StorageId,
                externalId: textractIntegration.ExternalId,
                name: textractIntegration.Name));
        }

        if (textractIntegrations.Count == 0)
        {
            Log.Information(
                "[INITIALIZATION] No textract integrations were found. Textract initialization skipped.");
        }
        else
        {
            Log.Information(
                "[INITIALIZATION] Textract Client Store initialization finished (Clients: {TextractClientsCount}).",
                textractIntegrations.Count);
        }
    }
}