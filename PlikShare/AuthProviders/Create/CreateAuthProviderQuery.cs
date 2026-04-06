using Microsoft.Data.Sqlite;
using PlikShare.AuthProviders.Entities;
using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Clock;
using Serilog;

namespace PlikShare.AuthProviders.Create;

public class CreateAuthProviderQuery(
    DbWriteQueue dbWriteQueue,
    MasterDataEncryptionBufferedFactory masterDataEncryptionBufferedFactory,
    IClock clock)
{
    public async Task<Result> Execute(
        string name,
        AuthProviderType type,
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
                name: name,
                type: type,
                clientId: clientId,
                clientSecret: clientSecret,
                issuerUrl: issuerUrl,
                autoDiscoveryUrl: autoDiscoveryUrl,
                derivedEncryption: derivedEncryption),
            cancellationToken: cancellationToken);
    }

    private Result ExecuteOperation(
        SqliteWriteContext dbWriteContext,
        string name,
        AuthProviderType type,
        string clientId,
        string clientSecret,
        string issuerUrl,
        string autoDiscoveryUrl,
        IDerivedMasterDataEncryption derivedEncryption)
    {
        try
        {
            var externalId = AuthProviderExtId.NewId();

            var authProviderId = dbWriteContext
                .OneRowCmd(
                    sql: """
                         INSERT INTO ap_auth_providers(
                             ap_external_id,
                             ap_name,
                             ap_type,
                             ap_is_active,
                             ap_client_id,
                             ap_client_secret_encrypted,
                             ap_issuer_url,
                             ap_auto_discovery_url,
                             ap_created_at
                         ) VALUES (
                             $externalId,
                             $name,
                             $type,
                             FALSE,
                             $clientId,
                             $clientSecret,
                             $issuerUrl,
                             $autoDiscoveryUrl,
                             $createdAt
                         )
                         RETURNING ap_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$externalId", externalId.Value)
                .WithParameter("$name", name)
                .WithParameter("$type", type.Value)
                .WithParameter("$clientId", clientId)
                .WithParameter("$clientSecret", derivedEncryption.Encrypt(clientSecret))
                .WithParameter("$issuerUrl", issuerUrl)
                .WithParameter("$autoDiscoveryUrl", autoDiscoveryUrl)
                .WithParameter("$createdAt", clock.UtcNow.ToString("o"))
                .ExecuteOrThrow();

            Log.Information(
                "{AuthProviderType} AuthProvider#{AuthProviderId} '{AuthProviderName}' with ExternalId '{AuthProviderExternalId}' was created.",
                type,
                authProviderId,
                name,
                externalId);

            return new Result(
                Code: ResultCode.Ok,
                ExternalId: externalId);
        }
        catch (SqliteException e)
        {
            if (e.HasUniqueConstraintFailed(tableName: "ap_auth_providers", columnName: "ap_name"))
            {
                return new Result(Code: ResultCode.NameNotUnique);
            }

            Log.Error(
                e,
                "Something went wrong while creating {AuthProviderType} auth provider '{AuthProviderName}'",
                type,
                name);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(
                e,
                "Something went wrong while creating {AuthProviderType} auth provider '{AuthProviderName}'",
                type,
                name);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok,
        NameNotUnique
    }

    public readonly record struct Result(
        ResultCode Code,
        AuthProviderExtId? ExternalId = null);
}
