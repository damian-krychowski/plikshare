using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Users.List.Contracts;
using PlikShare.Users.Sql;

namespace PlikShare.Users.List;

public class GetUsersQuery(
    PlikShareDb plikShareDb,
    AppOwners appOwners)
{
    private static readonly string Sql =
        $"""
           SELECT
               u_email,
               u_external_id,
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
               ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers,
               ({UserSql.HasClaim(Claims.Permission, Permissions.ManageAuth)}) AS u_can_manage_auth,
               ({UserSql.HasClaim(Claims.Permission, Permissions.ManageIntegrations)}) AS u_can_manage_integrations,
               u_max_workspace_number,
               u_default_max_workspace_size_in_bytes,
               u_default_max_workspace_team_members
           FROM u_users
           ORDER BY u_id ASC
         """;

    public GetUsersResponseDto Execute()
    {
        using var connection = plikShareDb.OpenConnection();

        var users = connection
            .Cmd(
                sql: Sql,
                readRowFunc: reader =>
                {
                    var email = reader.GetString(0);

                    return new GetUsersItemDto
                    {
                        Email = email,
                        ExternalId = reader.GetExtId<UserExtId>(1),
                        IsEmailConfirmed = reader.GetBoolean(2),
                        WorkspacesCount = reader.GetInt32(3),
                        Roles = new UserRoles
                        {
                            IsAppOwner = appOwners.IsAppOwner(email),
                            IsAdmin = reader.GetBoolean(4),
                        },
                        Permissions = new UserPermissions
                        {
                            CanAddWorkspace = reader.GetBoolean(5),
                            CanManageGeneralSettings = reader.GetBoolean(6),
                            CanManageUsers = reader.GetBoolean(7),
                            CanManageStorages = reader.GetBoolean(8),
                            CanManageEmailProviders = reader.GetBoolean(9),
                            CanManageAuth = reader.GetBoolean(10),
                            CanManageIntegrations = reader.GetBoolean(11),
                        },
                        MaxWorkspaceNumber = reader.GetInt32OrNull(12),
                        DefaultMaxWorkspaceSizeInBytes = reader.GetInt64OrNull(13),
                        DefaultMaxWorkspaceTeamMembers = reader.GetInt32OrNull(14)
                    };
                })
            .Execute();

        return new GetUsersResponseDto
        {
            Items = users
        };
    }
}