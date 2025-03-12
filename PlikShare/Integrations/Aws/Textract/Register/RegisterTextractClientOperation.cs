using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Integrations.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Integrations.Aws.Textract.Register;

public class RegisterTextractClientOperation(
    PlikShareDb plikShareDb,
    WorkspaceCache workspaceCache,
    IMasterDataEncryption masterDataEncryption,
    TextractClientStore textractClientStore)
{
    public void ExecuteOrThrow(int integrationId)
    {
        using var connection = plikShareDb.OpenConnection();

        var integration = connection
            .OneRowCmd(
                sql: """
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
                     """,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<IntegrationExtId>(0);
                    var name = reader.GetString(1);
                    var details = reader.GetFieldValue<byte[]>(2);
                    var workspaceId = reader.GetInt32(3);
                    var storageId = reader.GetInt32(4);

                    return new
                    {
                        ExternalId = externalId,
                        Name = name,
                        Details = masterDataEncryption.DecryptJson<AwsTextractDetails>(
                            versionedEncryptedBytes: details),
                        WorkspaceId = workspaceId,
                        StorageId = storageId
                    };
                })
            .WithParameter("$integrationId", integrationId)
            .WithEnumParameter("$integrationType", IntegrationType.AwsTextract)
            .ExecuteOrThrow();

        textractClientStore.RegisterClient(new TextractClient(
            workspaceCache: workspaceCache,
            awsClient: new AwsTextractClient(
                accessKey: integration.Details.AccessKey,
                secretAccessKey: integration.Details.SecretAccessKey,
                region: integration.Details.Region),
            integrationId: integrationId,
            workspaceId: integration.WorkspaceId,
            storageId: integration.StorageId,
            externalId: integration.ExternalId,
            name: integration.Name));
    }
}