using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Id;

namespace PlikShare.AuthProviders.PasswordLogin;

public class CheckUserHasSsoLoginQuery(PlikShareDb plikShareDb)
{
    public bool Execute(UserExtId userExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT 1
                     FROM ul_user_logins
                     INNER JOIN u_users ON u_id = ul_user_id
                     WHERE u_external_id = $userExternalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$userExternalId", userExternalId.Value)
            .Execute();

        return !result.IsEmpty;
    }
}