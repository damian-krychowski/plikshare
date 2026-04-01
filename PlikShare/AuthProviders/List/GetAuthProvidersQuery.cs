using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuthProviders.List;

public class GetAuthProvidersQuery(PlikShareDb plikShareDb)
{
    public List<AuthProvider> Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         ap_external_id,
                         ap_name,
                         ap_type,
                         ap_is_active,
                         ap_client_id,
                         ap_issuer_url
                     FROM ap_auth_providers
                     ORDER BY ap_id ASC
                     """,
                readRowFunc: reader => new AuthProvider(
                    ExternalId: reader.GetExtId<AuthProviderExtId>(0),
                    Name: reader.GetString(1),
                    Type: reader.GetString(2),
                    IsActive: reader.GetBoolean(3),
                    ClientId: reader.GetString(4),
                    IssuerUrl: reader.GetString(5)))
            .Execute();
    }

    public record AuthProvider(
        AuthProviderExtId ExternalId,
        string Name,
        string Type,
        bool IsActive,
        string ClientId,
        string IssuerUrl);
}
