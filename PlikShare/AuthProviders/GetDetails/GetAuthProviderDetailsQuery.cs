using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.AuthProviders.GetDetails;

public class GetAuthProviderDetailsQuery(
    PlikShareDb plikShareDb,
    IMasterDataEncryption masterDataEncryption)
{
    public AuthProviderDetails? Execute(AuthProviderExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         ap_id,
                         ap_external_id,
                         ap_name,
                         ap_type,
                         ap_is_active,
                         ap_client_id,
                         ap_client_secret_encrypted,
                         ap_issuer_url,
                         ap_auto_discovery_url
                     FROM ap_auth_providers
                     WHERE ap_external_id = $externalId
                     """,
                readRowFunc: reader => new AuthProviderDetails(
                    Id: reader.GetInt32(0),
                    ExternalId: reader.GetExtId<AuthProviderExtId>(1),
                    Name: reader.GetString(2),
                    Type: reader.GetString(3),
                    IsActive: reader.GetBoolean(4),
                    ClientId: reader.GetString(5),
                    ClientSecret: masterDataEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(6)),
                    IssuerUrl: reader.GetString(7),
                    AutoDiscoveryUrl: reader.GetString(8)))
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    public record AuthProviderDetails(
        int Id,
        AuthProviderExtId ExternalId,
        string Name,
        string Type,
        bool IsActive,
        string ClientId,
        string ClientSecret,
        string IssuerUrl,
        string AutoDiscoveryUrl);
}
