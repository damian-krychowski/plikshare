using PlikShare.AuthProviders.Id;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuthProviders.List;

public class GetActiveAuthProvidersPublicQuery(PlikShareDb plikShareDb)
{
    public List<ActiveAuthProvider> Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT
                         ap_external_id,
                         ap_name,
                         ap_type
                     FROM ap_auth_providers
                     WHERE ap_is_active = TRUE
                     ORDER BY ap_id ASC
                     """,
                readRowFunc: reader => new ActiveAuthProvider(
                    ExternalId: reader.GetExtId<AuthProviderExtId>(0),
                    Name: reader.GetString(1),
                    Type: reader.GetString(2)))
            .Execute();
    }

    public record ActiveAuthProvider(
        AuthProviderExtId ExternalId,
        string Name,
        string Type);
}
