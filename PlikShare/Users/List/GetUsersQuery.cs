using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Id;
using PlikShare.Users.Sql;

namespace PlikShare.Users.List;

public class GetUsersQuery(
    PlikShareDb plikShareDb,
    AppOwners appOwners)
{
    private static readonly string Sql = $"""
                                          SELECT
                                              u_external_id,
                                              u_email,
                                              u_email_confirmed,
                                              (
                                                  SELECT COUNT(*)
                                                  FROM w_workspaces
                                                  WHERE w_owner_id = u_id
                                              ) AS u_workspaces_count,
                                          	  ({UserSql.HasRole(Roles.Admin)}) AS u_is_admin,
                                              ({UserSql.HasClaim(Claims.Permission, Permissions.AddWorkspace)}) AS u_can_add_workspace,
                                              ({UserSql.HasClaim(Claims.Permission, Permissions.ManageGeneralSettings)}) AS u_can_manage_general_settings,
                                              ({UserSql.HasClaim(Claims.Permission, Permissions.ManageUsers)}) AS u_can_manage_users,
                                              ({UserSql.HasClaim(Claims.Permission, Permissions.ManageStorages)}) AS u_can_manage_storages,
                                              ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers
                                          FROM u_users
                                          ORDER BY u_id ASC
                                          """;
    public List<User> Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .Cmd(
                sql: Sql,
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<UserExtId>(0);
                    var email = reader.GetString(1);
                    var isEmailConfirmed = reader.GetBoolean(2);
                    var workspacesCount = reader.GetInt32(3);
                    var isAdmin = reader.GetBoolean(4);
                    var canAddWorkspace = reader.GetBoolean(5);
                    var canManageGeneralSettings = reader.GetBoolean(6);
                    var canManageUsers = reader.GetBoolean(7);
                    var canManageStorages = reader.GetBoolean(8);
                    var canManageEmailProviders = reader.GetBoolean(9);

                    return new User(
                        ExternalId: externalId,
                        Email: email,
                        IsEmailConfirmed: isEmailConfirmed,
                        WorkspacesCount: workspacesCount,
                        IsAppOwner: appOwners.IsAppOwner(email),
                        IsAdmin: isAdmin,
                        CanAddWorkspace: canAddWorkspace,
                        CanManageGeneralSettings: canManageGeneralSettings,
                        CanManageUsers: canManageUsers,
                        CanManageStorages: canManageStorages,
                        CanManageEmailProviders: canManageEmailProviders);
                })
            .Execute();
    }

    public record User(
        UserExtId ExternalId,
        string Email,
        bool IsEmailConfirmed,
        int WorkspacesCount,
        bool IsAppOwner,
        bool IsAdmin,
        bool CanAddWorkspace,
        bool CanManageGeneralSettings,
        bool CanManageUsers,
        bool CanManageStorages,
        bool CanManageEmailProviders);
}