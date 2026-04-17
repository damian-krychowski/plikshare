using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Authorization;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Users.Entities;
using PlikShare.Users.GetOrCreate;
using PlikShare.Users.Id;
using PlikShare.Users.Sql;

namespace PlikShare.Users.Cache;

public class UserCache(
    PlikShareDb plikShareDb,
    AppOwners appOwners,
    HybridCache cache,
    GetOrCreateUserInvitationQuery getOrCreateUserInvitationQuery)
{
    // Probe options: read from cache without writing anything back when the factory returns null.
    private static readonly HybridCacheEntryOptions ProbeOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
              | HybridCacheEntryFlags.DisableDistributedCacheWrite
    };

    private static string UserEmailKey(string email) => $"user:email:{email}";
    private static string UserIdKey(int userId) => $"user:id:{userId}";
    private static string UserExtIdKey(UserExtId extId) => $"user:extid:{extId.Value}";
    private static string UserTag(int userId) => $"user-{userId}";

    public async ValueTask<UserContext> GetOrCreateUserInvitationByEmail(
        Email email,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            UserEmailKey(email.Value),
            cancellationToken);

        if (cached is not null)
            return cached;

        var user = await getOrCreateUserInvitationQuery.Execute(
            email,
            cancellationToken);

        await StoreInAllKeys(
            user,
            cancellationToken);

        return user;
    }

    public async ValueTask<UserContext?> TryGetUser(
        int userId,
        CancellationToken cancellationToken)
    {
        return await GetOrLoadAsync(
            primaryKey: UserIdKey(userId),
            loader: () => GetUser(UserLookup.ById(userId)),
            cancellationToken: cancellationToken);
    }

    public async ValueTask<UserContext?> TryGetUser(
        UserExtId userExternalId,
        CancellationToken cancellationToken)
    {
        return await GetOrLoadAsync(
            primaryKey: UserExtIdKey(userExternalId),
            loader: () => GetUser(UserLookup.ByExternalId(userExternalId)),
            cancellationToken: cancellationToken);
    }

    public async ValueTask<UserContext?> TryGetUser(
        Email email,
        CancellationToken cancellationToken)
    {
        return await GetOrLoadAsync(
            primaryKey: UserEmailKey(email.Value),
            loader: () => GetUser(UserLookup.ByEmail(email)),
            cancellationToken: cancellationToken);
    }

    public async ValueTask<UserContext> GetOrThrow(
        Email email,
        CancellationToken cancellationToken)
    {
        var user = await TryGetUser(email, cancellationToken);

        return user ?? throw new InvalidOperationException(
            $"User with email '{email.Anonymize()}' was not found.");
    }

    public async ValueTask<UserContext> GetOrThrow(
        UserExtId userExternalId,
        CancellationToken cancellationToken)
    {
        var user = await TryGetUser(userExternalId, cancellationToken);

        return user ?? throw new InvalidOperationException(
            $"User with external id '{userExternalId.Value}' was not found.");
    }

    private async ValueTask<UserContext?> GetOrLoadAsync(
        string primaryKey,
        Func<UserContext?> loader,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            primaryKey,
            cancellationToken);

        if (cached is not null)
            return cached;

        var user = loader();

        if (user is null)
            return null;

        await StoreInAllKeys(
            user,
            cancellationToken);

        return user;
    }

    // Reads the cache without polluting it with a null entry on miss.
    private ValueTask<UserContext?> ProbeCache(
        string key,
        CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync<UserContext?>(
            key: key,
            factory: _ => ValueTask.FromResult<UserContext?>(null),
            options: ProbeOptions,
            cancellationToken: cancellationToken);
    }

    private async ValueTask StoreInAllKeys(
        UserContext user,
        CancellationToken cancellationToken)
    {
        var tags = new[] { UserTag(user.Id) };

        await cache.SetAsync(
            UserIdKey(user.Id),
            user,
            tags: tags,
            cancellationToken: cancellationToken);

        await cache.SetAsync(
            UserExtIdKey(user.ExternalId),
            user,
            tags: tags,
            cancellationToken: cancellationToken);

        await cache.SetAsync(
            UserEmailKey(user.Email.Value),
            user,
            tags: tags,
            cancellationToken: cancellationToken);
    }

    public ValueTask InvalidateEntry(
        int userId,
        CancellationToken cancellationToken)
    {
        // A single tag removal clears every key associated with the user.
        return cache.RemoveByTagAsync(
            UserTag(userId),
            cancellationToken);
    }

    public async ValueTask InvalidateEntry(
        UserExtId userExternalId,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            UserExtIdKey(userExternalId),
            cancellationToken);

        if (cached is not null)
        {
            await InvalidateEntry(
                cached.Id,
                cancellationToken);
        }
        else
        {
            // Fallback: at least drop the pointer key if the user is gone from the DB.
            await cache.RemoveAsync(
                UserExtIdKey(userExternalId),
                cancellationToken);
        }
    }

    public async ValueTask InvalidateEntry(
        Email email,
        CancellationToken cancellationToken)
    {
        var cached = await ProbeCache(
            UserEmailKey(email.Value),
            cancellationToken);

        if (cached is not null)
        {
            await InvalidateEntry(
                cached.Id,
                cancellationToken);
        }
        else
        {
            await cache.RemoveAsync(
                UserEmailKey(email.Value),
                cancellationToken);
        }
    }

    public ValueTask InvalidateAllEntries(CancellationToken cancellationToken)
    {
        return cache.RemoveByTagAsync("*", cancellationToken);
    }

    private UserContext? GetUser(UserLookup lookup)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: $"""
                SELECT
                    u_email,
                    u_is_invitation,
                    u_external_id,
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
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageAuth)}) AS u_can_manage_auth,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageIntegrations)}) AS u_can_manage_integrations,
                    ({UserSql.HasClaim(Claims.Permission, Permissions.ManageAuditLog)}) AS u_can_manage_audit_log,
                    u_invitation_code,
                    u_max_workspace_number,
                    u_default_max_workspace_size_in_bytes,
                    u_default_max_workspace_team_members,
                    (u_password_hash IS NOT NULL) AS u_has_password,
                    u_encryption_public_key,
                    u_encryption_encrypted_private_key,
                    u_encryption_kdf_salt,
                    u_encryption_kdf_params,
                    u_encryption_verify_hash,
                    u_encryption_recovery_wrapped_private_key,
                    u_encryption_recovery_verify_hash
                FROM u_users
                WHERE {lookup.WhereClause}
                LIMIT 1
                """,
                readRowFunc: reader =>
                {
                    var email = reader.GetEmail(0);
                    var isInvitation = reader.GetBoolean(1);
                    var encryptionPublicKey = reader.GetFieldValueOrNull<byte[]>(21);

                    return new UserContext
                    {
                        Status = isInvitation ? UserStatus.Invitation : UserStatus.Registered,
                        ExternalId = reader.GetExtId<UserExtId>(2),
                        Id = reader.GetInt32(3),
                        Email = email,
                        IsEmailConfirmed = reader.GetBoolean(4),

                        Stamps = new UserSecurityStamps
                        {
                            Security = reader.GetString(5),
                            Concurrency = reader.GetString(6)
                        },

                        Roles = new UserRoles
                        {
                            IsAppOwner = appOwners.IsAppOwner(email),
                            IsAdmin = reader.GetBoolean(7)
                        },

                        Permissions = new UserPermissions
                        {
                            CanAddWorkspace = reader.GetBoolean(8),
                            CanManageGeneralSettings = reader.GetBoolean(9),
                            CanManageUsers = reader.GetBoolean(10),
                            CanManageStorages = reader.GetBoolean(11),
                            CanManageEmailProviders = reader.GetBoolean(12),
                            CanManageAuth = reader.GetBoolean(13),
                            CanManageIntegrations = reader.GetBoolean(14),
                            CanManageAuditLog = reader.GetBoolean(15)
                        },

                        Invitation = isInvitation
                            ? new UserInvitation { Code = reader.GetString(16) }
                            : null,

                        MaxWorkspaceNumber = reader.GetInt32OrNull(17),
                        DefaultMaxWorkspaceSizeInBytes = reader.GetInt64OrNull(18),
                        DefaultMaxWorkspaceTeamMembers = reader.GetInt32OrNull(19),
                        HasPassword = reader.GetBoolean(20),

                        EncryptionMetadata = encryptionPublicKey is null
                            ? null
                            : new UserEncryptionMetadata
                            {
                                PublicKey = encryptionPublicKey,
                                EncryptedPrivateKey = reader.GetFieldValue<byte[]>(22),
                                KdfSalt = reader.GetFieldValue<byte[]>(23),
                                KdfParams = EncryptionPasswordKdf.DeserializeParams(reader.GetString(24)),
                                VerifyHash = reader.GetFieldValue<byte[]>(25),
                                RecoveryWrappedPrivateKey = reader.GetFieldValue<byte[]>(26),
                                RecoveryVerifyHash = reader.GetFieldValue<byte[]>(27)
                            }
                    };
                })
            .WithParameter(lookup.ParamName, lookup.ParamValue)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private readonly record struct UserLookup(
        string WhereClause,
        string ParamName,
        object ParamValue)
    {
        public static UserLookup ById(int userId) =>
            new("u_id = $userId", "$userId", userId);

        public static UserLookup ByExternalId(UserExtId extId) =>
            new("u_external_id = $userExternalId", "$userExternalId", extId.Value);

        public static UserLookup ByEmail(Email email) =>
            new("u_normalized_email = $normalizedEmail", "$normalizedEmail", email.Normalized);
    }
}