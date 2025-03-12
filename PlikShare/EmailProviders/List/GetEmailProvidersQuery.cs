using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.EmailProviders.Id;

namespace PlikShare.EmailProviders.List;

public class GetEmailProvidersQuery(PlikShareDb plikShareDb)
{
    public List<EmailProvider> Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT 
                         ep_external_id,
                         ep_type,
                         ep_name,
                         ep_email_from,
                         ep_is_confirmed,
                         ep_is_active
                     FROM ep_email_providers
                     ORDER BY ep_id ASC
                     """,
                readRowFunc: reader => new EmailProvider(
                    ExternalId: reader.GetExtId<EmailProviderExtId>(0),
                    Type: reader.GetString(1),
                    Name: reader.GetString(2),
                    EmailFrom: reader.GetString(3),
                    IsConfirmed: reader.GetBoolean(4),
                    IsActive: reader.GetBoolean(5)))
            .Execute();
    }

    public record EmailProvider(
        EmailProviderExtId ExternalId,
        string Type,
        string Name,
        string EmailFrom,
        bool IsConfirmed,
        bool IsActive);
}