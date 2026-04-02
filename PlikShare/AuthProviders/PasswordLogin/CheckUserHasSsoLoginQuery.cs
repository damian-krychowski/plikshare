using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.AuthProviders.PasswordLogin;

public class CheckUserHasSsoLoginQuery(PlikShareDb plikShareDb)
{
    public bool Execute(int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT 1
                     FROM ul_user_logins
                     WHERE ul_user_id = $userId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$userId", userId)
            .Execute();

        return !result.IsEmpty;
    }
}
