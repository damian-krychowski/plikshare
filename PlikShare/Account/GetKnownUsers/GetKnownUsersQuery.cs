using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;

namespace PlikShare.Account.GetKnownUsers;

public class GetKnownUsersQuery(PlikShareDb plikShareDb)
{
    public List<User> Execute(
        UserContext user)
    {
        return user.HasAdminRole
            ? GetAllUsers(user.Id)
            : GetUsersKnowsFromCommonWorkspaces(user.Id);
    }

    private List<User> GetAllUsers(int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: @"
                    SELECT 
                        u_external_id,
                        u_email
                    FROM u_users
                    WHERE u_id != $userId
                    ORDER BY u_email ASC
                ",
                readRowFunc: reader => new User(
                    ExternalId: reader.GetExtId<UserExtId>(0),
                    Email: reader.GetString(1)))
            .WithParameter("$userId", userId)
            .Execute();
    }

    private List<User> GetUsersKnowsFromCommonWorkspaces(
        int userId)
    {
        using var connection = plikShareDb.OpenConnection();

        var workspaceIds = connection
            .Cmd(
                sql: @"
                    SELECT w_id AS id
                    FROM w_workspaces
                    WHERE w_owner_id = $userId
                    UNION
                    SELECT wm_workspace_id AS id
                    FROM wm_workspace_membership
                    WHERE wm_member_id = $userId
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$userId", userId)
            .Execute()
            .Distinct()
            .ToList();

        return connection
            .Cmd(
                sql: @"
                    WITH userIds AS (
                        SELECT w_owner_id AS id
                        FROM w_workspaces
                        WHERE w_id IN (
                            SELECT value FROM json_each($workspaceIds)
                        )
                        UNION
                        SELECT wm_member_id AS id
                        FROM wm_workspace_membership
                        WHERE 
                            wm_workspace_id IN (
                                SELECT value FROM json_each($workspaceIds)
                            )
                            AND wm_was_invitation_accepted = TRUE
                    )
                    SELECT 
                        u_external_id,
                        u_email
                    FROM userIds AS user
                    INNER JOIN u_users
                        ON u_id = user.id
                        AND u_id != $userId
                    ORDER BY u_email ASC
                ",
                readRowFunc: reader => new User(
                    ExternalId: reader.GetExtId<UserExtId>(0),
                    Email: reader.GetString(1)))
            .WithParameter("$userId", userId)
            .WithJsonParameter("$workspaceIds", workspaceIds)
            .Execute();
    }

    public record User(
        UserExtId ExternalId,
        string Email);
}