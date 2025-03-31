using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;
using PlikShare.Users.Sql;

namespace PlikShare.Users.Cache;

public class UserCache(
    PlikShareDb plikShareDb,
    AppOwners appOwners,
    HybridCache cache,
    UserService userService)
{
    private readonly ConcurrentDictionary<int, string> _userIdEmailMap = new();
    private readonly ConcurrentDictionary<UserExtId, int> _userIdMap = new();

    private static string UserEmailKey(string email) => $"user:email:{email}";
    
    public async ValueTask<UserContext> GetOrCreateUserInvitationByEmail(
        Email email,
        CancellationToken cancellationToken)
    {
        var user = await cache.GetOrCreateAsync<UserContext>(
            key: UserEmailKey(email.Value),
            factory: async ct =>
            {
                var user = await userService.GetOrCreateUserInvitation(
                    email: email,
                    cancellationToken: ct);

                UpdateEmailMap(user);

                return user;
            }, 
            cancellationToken: cancellationToken);

        if (user is null)
            throw new InvalidOperationException(
                $"Cannot obtain User for email '{email}' from cache");

        return user;
    }
    
    public async ValueTask<UserContext?> TryGetUser(
        int userId,
        CancellationToken cancellationToken)
    {
        if (_userIdEmailMap.TryGetValue(userId, out var email))
        {
            return await cache.GetOrCreateAsync(
                key: UserEmailKey(email),
                factory: _ => ValueTask.FromResult(GetUser(userId)),
                cancellationToken: cancellationToken);
        }

        var user = GetUser(userId);
        
        if (user is null)
            return null;
        
        await StoreUserInCache(
            user, 
            cancellationToken);

        return user;
    }

    private UserContext? GetUser(int userId)
    {
        using var connection = plikShareDb.OpenConnection();
        
        var result = connection
            .OneRowCmd(
                sql: $"""
                SELECT
                	u_email,
                	u_is_invitation,
                	u_external_id,
                	u_email_confirmed,
                	u_security_stamp,
                	u_concurrency_stamp,
                	({UserSql.HasRole(Roles.Admin)}) AS u_is_admin,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.AddWorkspace)}) AS u_can_add_workspace,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageGeneralSettings)}) AS u_can_manage_general_settings,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageUsers)}) AS u_can_manage_users,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageStorages)}) AS u_can_manage_storages,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers,
                	u_invitation_code,
                    u_max_workspace_number,
                    u_default_max_workspace_size_in_bytes   
                FROM u_users
                WHERE u_id = $userId
                LIMIT 1
                """,
                readRowFunc: reader =>
                {
                    var email = reader.GetEmail(0);

                    return new UserContext(
                        Status: reader.GetBoolean(1)
                            ? UserStatus.Invitation 
                            : UserStatus.Registered,
                        Id: userId,
                        ExternalId: reader.GetExtId<UserExtId>(2),
                        Email: email,
                        IsEmailConfirmed: reader.GetBoolean(3),
                        Stamps: new UserSecurityStamps(
                            Security: reader.GetString(4),
                            Concurrency: reader.GetString(5)),
                        Roles: new UserRoles(
                            IsAppOwner: appOwners.IsAppOwner(email),
                            IsAdmin: reader.GetBoolean(6)),
                        Permissions: new UserPermissions(
                            CanAddWorkspace: reader.GetBoolean(7),
                            CanManageGeneralSettings: reader.GetBoolean(8),
                            CanManageUsers: reader.GetBoolean(9),
                            CanManageStorages: reader.GetBoolean(10),
                            CanManageEmailProviders: reader.GetBoolean(11)),
                        Invitation: reader.GetBoolean(1)
                            ? new UserInvitation(
                                Code: reader.GetString(12)) 
                            : null,
                        MaxWorkspaceNumber: reader.GetInt32OrNull(13),
                        DefaultMaxWorkspaceSizeInBytes: reader.GetInt64OrNull(14));
                })
            .WithParameter("$userId", userId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    public async ValueTask<UserContext?> TryGetUser(
        UserExtId userExternalId,
        CancellationToken cancellationToken)
    {
        if (_userIdMap.TryGetValue(userExternalId, out var userId))
            return await TryGetUser(userId, cancellationToken);

        var user = GetUser(userExternalId);

        if (user is null)
            return null;

        await StoreUserInCache(
            user, 
            cancellationToken);

        return user;
    }
    
    private UserContext? GetUser(
        UserExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();
        
        var result = connection
            .OneRowCmd(
                sql: $"""
                SELECT
                	u_email,
                	u_is_invitation,	
                	u_id,
                	u_email_confirmed,			        
                	u_security_stamp,
                	u_concurrency_stamp,
                	({UserSql.HasRole(Roles.Admin)}) AS u_is_admin,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.AddWorkspace)}) AS u_can_add_workspace,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageGeneralSettings)}) AS u_can_manage_general_settings,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageUsers)}) AS u_can_manage_users,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageStorages)}) AS u_can_manage_storages,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers,
                	u_invitation_code,
                    u_max_workspace_number,
                    u_default_max_workspace_size_in_bytes   
                FROM u_users
                WHERE u_external_id = $userExternalId
                LIMIT 1
                """,
                readRowFunc: reader =>
                {
                    var email = reader.GetEmail(0);

                    return new UserContext(
                        Status: reader.GetBoolean(1) ? UserStatus.Invitation : UserStatus.Registered,
                        Id: reader.GetInt32(2),
                        ExternalId: externalId,
                        Email: email,
                        IsEmailConfirmed: reader.GetBoolean(3),
                        Stamps: new UserSecurityStamps(
                            Security: reader.GetString(4),
                            Concurrency: reader.GetString(5)),
                        Roles: new UserRoles(
                            IsAppOwner: appOwners.IsAppOwner(email),
                            IsAdmin: reader.GetBoolean(6)),
                        Permissions: new UserPermissions(
                            CanAddWorkspace: reader.GetBoolean(7),
                            CanManageGeneralSettings: reader.GetBoolean(8),
                            CanManageUsers: reader.GetBoolean(9),
                            CanManageStorages: reader.GetBoolean(10),
                            CanManageEmailProviders: reader.GetBoolean(11)),
                        Invitation: reader.GetBoolean(1)
                            ? new UserInvitation(
                                Code: reader.GetString(12))
                            : null,
                        MaxWorkspaceNumber: reader.GetInt32OrNull(13),
                        DefaultMaxWorkspaceSizeInBytes: reader.GetInt64OrNull(14));
                })
            .WithParameter("$userExternalId", externalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
    
    private ValueTask StoreUserInCache(
        UserContext user,
        CancellationToken cancellationToken)
    {
        UpdateEmailMap(user);
        
        return cache.SetAsync(
            key: UserEmailKey(user.Email.Value),
            value: user, 
            cancellationToken: cancellationToken);
    }

    
    private void UpdateEmailMap(UserContext user)
    {
        _userIdEmailMap.AddOrUpdate(
            key: user.Id,
            addValueFactory: _ => user.Email.Value,
            updateValueFactory: (_, _) => user.Email.Value);

        _userIdMap.AddOrUpdate(
            key: user.ExternalId,
            addValueFactory: _ => user.Id,
            updateValueFactory: (_, _) => user.Id);
    }

    public async ValueTask InvalidateEntry(
        int userId, 
        CancellationToken cancellationToken)
    {
        if (_userIdEmailMap.Remove(userId, out var email))
        {
            await cache.RemoveAsync(
                UserEmailKey(email),
                cancellationToken);
        }
    }
}