using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;

namespace PlikShare.Users.UserIdentityResolver;

public class UserIdentityResolver(PlikShareDb plikShareDb)
{
    public BulkResult Resolve<T>(List<T> identities) where T: IUserIdentity
    {
        var result = new BulkResult();

        var boxLinkIdentities = identities
            .Where(x => x.IdentityType == BoxLinkSessionUserIdentity.Type)
            .Select(x => x.Identity)
            .Distinct();

        foreach (var boxLinkIdentity in boxLinkIdentities)
        {
            result.Add(
                identity: boxLinkIdentity,
                identityType: BoxLinkSessionUserIdentity.Type,
                result: new Result(
                    IsAnonymous: true,
                    WasDeleted: false,
                    DisplayText: "anonymous"));
        }

        var userExternalIds = identities
            .Where(x => x.IdentityType == UserIdentity.Type)
            .Select(x => x.Identity)
            .Distinct()
            .ToList();

        var users = GetUserEmails(userExternalIds);

        foreach (var userExternalId in userExternalIds)
        {
            var user = users.FirstOrDefault(u => u.ExternalId == userExternalId);

            if (user is null)
            {
                result.Add(
                    identity: userExternalId,
                    identityType: UserIdentity.Type,
                    result: new Result(
                        IsAnonymous: false,
                        WasDeleted: true,
                        DisplayText: "account was deleted"));
            }
            else
            {
                result.Add(
                    identity: userExternalId,
                    identityType: UserIdentity.Type,
                    result: new Result(
                        IsAnonymous: false,
                        WasDeleted: false,
                        DisplayText: user.Email));
            }
        }

        var integrationExternalIds = identities
            .Where(x => x.IdentityType == IntegrationUserIdentity.Type)
            .Select(x => x.Identity)
            .Distinct()
            .ToList();

        var integrations = GetIntegrations(integrationExternalIds);

        foreach (var integrationExternalId in integrationExternalIds)
        {
            var integration = integrations.FirstOrDefault(u => u.ExternalId == integrationExternalId);

            if (integration is null)
            {
                result.Add(
                    identity: integrationExternalId,
                    identityType: IntegrationUserIdentity.Type,
                    result: new Result(
                        IsAnonymous: false,
                        WasDeleted: true,
                        DisplayText: "integration was deleted"));
            }
            else
            {
                result.Add(
                    identity: integrationExternalId,
                    identityType: IntegrationUserIdentity.Type,
                    result: new Result(
                        IsAnonymous: false,
                        WasDeleted: false,
                        DisplayText: integration.Name));
            }
        }

        return result;
    }


    public record Result(
        bool IsAnonymous,
        bool WasDeleted,
        string DisplayText);

    private List<Integration> GetIntegrations(List<string> externalIds)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT 
                         i_external_id,
                         i_name
                     FROM i_integrations
                     WHERE i_external_id IN (
                         SELECT value FROM json_each($externalIds)
                     )
                     """,
                readRowFunc: reader => new Integration(
                    ExternalId: reader.GetString(0),
                    Name: reader.GetString(1)))
            .WithJsonParameter("$externalIds", externalIds)
            .Execute();
    }

    private List<User> GetUserEmails(List<string> externalIds)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: """
                     SELECT 
                         u_external_id,
                         u_email
                     FROM u_users
                     WHERE u_external_id IN (
                         SELECT value FROM json_each($externalIds)
                     )
                     """,
                readRowFunc: reader => new User(
                    ExternalId: reader.GetString(0),
                    Email: reader.GetString(1)))
            .WithJsonParameter("$externalIds", externalIds)
            .Execute();
    }

    private record Integration(string ExternalId, string Name);
    private record User(string ExternalId, string Email);

    public class BulkResult
    {
        public static BulkResult Empty { get; } = new();

        private readonly Dictionary<string, Result> _results = new();

        private string Key(string identity, string identityType) => $"Identity: {identity}; Type: {identityType}";

        public void Add(string identity, string identityType, Result result)
        {
            _results.Add(Key(identity, identityType), result);
        }

        public Result GetOrThrow(IUserIdentity identity)
        {
            var key = Key(
                identity.Identity, 
                identity.IdentityType);

            var result = _results[key];

            if (result is null)
            {
                throw new InvalidOperationException(
                    $"Could not find resolved identity for: {identity.IdentityType}: {identity}");
            }

            return result;
        }
    }
}