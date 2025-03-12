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
                sql: $@"
                    SELECT
	                    u_external_id,
				        u_email,
				        u_email_confirmed,
				        u_is_invitation,
				        u_security_stamp,
				        u_concurrency_stamp,
				        ({UserSql.HasRole(Roles.Admin)}) AS u_is_admin,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.AddWorkspace)}) AS u_can_add_workspace,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageGeneralSettings)}) AS u_can_manage_general_settings,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageUsers)}) AS u_can_manage_users,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageStorages)}) AS u_can_manage_storages,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers,
				        u_invitation_code
	                FROM u_users
	                WHERE u_id = $userId
			        LIMIT 1
                ",
                readRowFunc: reader =>
                {
                    var externalId = reader.GetExtId<UserExtId>(0);
                    var email = reader.GetEmail(1);
                    var isEmailConfirmed = reader.GetBoolean(2);
                    var isInvitation = reader.GetBoolean(3);
                    var securityStamp = reader.GetString(4);
                    var concurrencyStamp = reader.GetString(5);
                    var isAdmin = reader.GetBoolean(6);
                    var canAddWorkspace = reader.GetBoolean(7);
                    var canManageGeneralSettings = reader.GetBoolean(8);
                    var canManageUsers = reader.GetBoolean(9);
                    var canManageStorages = reader.GetBoolean(10);
                    var canManageEmailProviders = reader.GetBoolean(11);

                    return new UserContext(
                        Status: isInvitation 
                            ? UserStatus.Invitation 
                            : UserStatus.Registered,
                        Id: userId,
                        ExternalId: externalId,
                        Email: email,
                        IsEmailConfirmed: isEmailConfirmed,
                        Stamps: new UserSecurityStamps(
                            Security: securityStamp,
                            Concurrency: concurrencyStamp),
                        Roles: new UserRoles(
                            IsAppOwner: appOwners.IsAppOwner(email),
                            IsAdmin: isAdmin),
                        Permissions: new UserPermissions(
                            CanAddWorkspace: canAddWorkspace,
                            CanManageGeneralSettings: canManageGeneralSettings,
                            CanManageUsers: canManageUsers,
                            CanManageStorages: canManageStorages,
                            CanManageEmailProviders: canManageEmailProviders),
                        Invitation: isInvitation 
                            ? new UserInvitation(
                                Code: reader.GetString(12)) 
                            : null);
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
                sql: $@"
                    SELECT
	                    u_id,
				        u_email,
				        u_email_confirmed,
				        u_is_invitation,				        
				        u_security_stamp,
				        u_concurrency_stamp,
				        ({UserSql.HasRole(Roles.Admin)}) AS u_is_admin,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.AddWorkspace)}) AS u_can_add_workspace,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageGeneralSettings)}) AS u_can_manage_general_settings,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageUsers)}) AS u_can_manage_users,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageStorages)}) AS u_can_manage_storages,
                        ({UserSql.HasClaim(Claims.Permission, Permissions.ManageEmailProviders)}) AS u_can_manage_email_providers,
				        u_invitation_code
	                FROM u_users
	                WHERE u_external_id = $userExternalId
			        LIMIT 1
                ",
                readRowFunc: reader => 
                {
                    var id = reader.GetInt32(0);
                    var email = reader.GetEmail(1);
                    var isEmailConfirmed = reader.GetBoolean(2);
                    var isInvitation = reader.GetBoolean(3);
                    var securityStamp = reader.GetString(4);
                    var concurrencyStamp = reader.GetString(5);
                    var isAdmin = reader.GetBoolean(6);
                    var canAddWorkspace = reader.GetBoolean(7);
                    var canManageGeneralSettings = reader.GetBoolean(8);
                    var canManageUsers = reader.GetBoolean(9);
                    var canManageStorages = reader.GetBoolean(10);
                    var canManageEmailProviders = reader.GetBoolean(11);
                    
                    return new UserContext(
                        Status: isInvitation 
                            ? UserStatus.Invitation 
                            : UserStatus.Registered,
                        Id: id,
                        ExternalId: externalId,
                        Email: email,
                        IsEmailConfirmed: isEmailConfirmed,
                        Stamps: new UserSecurityStamps(
                            Security: securityStamp,
                            Concurrency: concurrencyStamp),
                        Roles: new UserRoles(
                            IsAppOwner: appOwners.IsAppOwner(email),
                            IsAdmin: isAdmin),
                        Permissions: new UserPermissions(
                            CanAddWorkspace: canAddWorkspace,
                            CanManageGeneralSettings: canManageGeneralSettings,
                            CanManageUsers: canManageUsers,
                            CanManageStorages: canManageStorages,
                            CanManageEmailProviders: canManageEmailProviders),
                        Invitation: isInvitation 
                            ? new UserInvitation(
                                Code: reader.GetString(12)) 
                            : null);
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