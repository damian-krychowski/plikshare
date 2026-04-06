using Microsoft.Data.Sqlite;
using PlikShare.AuthProviders.Entities;
using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using Serilog;

namespace PlikShare.AuthProviders.Update;

public class UpdateAuthProviderQuery(
    DbWriteQueue dbWriteQueue,
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory)
{
    public async Task<ResultCode> Execute(
        AuthProviderExtId externalId,
        string name,
        string clientId,
        string clientSecret,
        string issuerUrl,
        CancellationToken cancellationToken)
    {
        var derivedEncryption = await masterDataEncryptionBufferedFactory.Take(
            cancellationToken: cancellationToken);

        var autoDiscoveryUrl = OidcUrls.GetDiscoveryUrl(issuerUrl);

        return await dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteOperation(
                dbWriteContext: context,
                externalId: externalId,
                name: name,
                clientId: clientId,
                clientSecret: clientSecret,
                issuerUrl: issuerUrl,
                autoDiscoveryUrl: autoDiscoveryUrl,
                derivedEncryption: derivedEncryption),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        AuthProviderExtId externalId,
        string name,
        string clientId,
        string clientSecret,
        string issuerUrl,
        string autoDiscoveryUrl,
        IDerivedMasterDataEncryption derivedEncryption)
    {
        try
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE ap_auth_providers
                         SET ap_name = $name,
                             ap_client_id = $clientId,
                             ap_client_secret_encrypted = $clientSecret,
                             ap_issuer_url = $issuerUrl,
                             ap_auto_discovery_url = $autoDiscoveryUrl
                         WHERE ap_external_id = $externalId
                         RETURNING ap_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", externalId.Value)
                .WithParameter("$name", name)
                .WithParameter("$clientId", clientId)
                .WithParameter("$clientSecret", derivedEncryption.Encrypt(clientSecret))
                .WithParameter("$issuerUrl", issuerUrl)
                .WithParameter("$autoDiscoveryUrl", autoDiscoveryUrl)
                .Execute();

            if (result.IsEmpty)
            {
                return ResultCode.NotFound;
            }

            Log.Information(
                "Auth Provider '{AuthProviderExternalId}' was updated.",
                externalId);

            return ResultCode.Ok;
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(
                    tableName: "ap_auth_providers",
                    columnName: "ap_name"))
            {
                return ResultCode.NameNotUnique;
            }

            Log.Error(
                e,
                "Something went wrong while updating Auth Provider '{AuthProviderExternalId}'",
                externalId);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Something went wrong while updating Auth Provider '{AuthProviderExternalId}'",
                externalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound,
        NameNotUnique
    }
}
