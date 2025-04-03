using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.GeneralSettings;
using PlikShare.Users.Cache;
using PlikShare.Users.Id;
using PlikShare.Users.UpdatePermissionsAndRoles;
using Serilog;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace PlikShare.Core.IdentityProvider
{
    public class SqLiteIdentityUserStore<TUserClaim, TUserRole, TUserLogin, TRoleClaim> : UserStoreBase<ApplicationUser, ApplicationRole, string, TUserClaim, TUserRole, TUserLogin, ApplicationUserToken, TRoleClaim>,
            IProtectedUserStore<ApplicationUser>
        where TUserClaim : IdentityUserClaim<string>, new()
        where TUserRole : IdentityUserRole<string>, new()
        where TUserLogin : IdentityUserLogin<string>, new()
        where TRoleClaim : IdentityRoleClaim<string>, new()
    {
        private readonly PlikShareDb _plikShareDb;
        private readonly UserCache _userCache;
        private readonly AppSettings _appSettings;
        private readonly DbWriteQueue _dbWriteQueue;

        public SqLiteIdentityUserStore(
            IdentityErrorDescriber describer,
            PlikShareDb plikShareDb,
            DbWriteQueue dbWriteQueue,
            UserCache userCache,
            AppSettings appSettings) : base(describer)
        {
            _plikShareDb = plikShareDb;
            _userCache = userCache;
            _appSettings = appSettings;
            _dbWriteQueue = dbWriteQueue;
        }

        public override IQueryable<ApplicationUser> Users => throw new NotSupportedException();
        
        public override async Task AddClaimsAsync(
            ApplicationUser user, 
            IEnumerable<Claim> claims, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (claims == null) {
                throw new ArgumentNullException(nameof(claims), $"Parameter {nameof(claims)} cannot be null.");
            }

            throw new NotImplementedException();
        }
        
        public override async Task AddLoginAsync(
            ApplicationUser user, 
            UserLoginInfo login, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (login == null) {
                throw new ArgumentNullException(nameof(login), $"Parameter {nameof(login)} cannot be null.");
            }

            throw new NotImplementedException();
        }
        
        public override async Task AddToRoleAsync(
            ApplicationUser user, 
            string normalizedRoleName, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (string.IsNullOrEmpty(normalizedRoleName)) {
                throw new ArgumentException($"Parameter {nameof(normalizedRoleName)} cannot be null or empty.");
            }
            
            var roleEntity = await FindRoleAsync(normalizedRoleName, cancellationToken);
            
            if (roleEntity == null) {
                throw new InvalidOperationException($"Role '{normalizedRoleName}' was not found.");
            }
                
            throw new NotImplementedException();
        }
        
        public override async Task<IdentityResult> CreateAsync(
            ApplicationUser user, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            var result = await _dbWriteQueue.Execute(
                operationToEnqueue: context => ExecuteCreateUser(
                    dbWriteContext: context,
                    user: user),
                cancellationToken: cancellationToken);

            if (result.Succeeded)
            {
                await _userCache.InvalidateEntry(
                    userId: user.DatabaseId,
                    cancellationToken: cancellationToken);
            }

            return result;
        }

        private IdentityResult ExecuteCreateUser(
            DbWriteQueue.Context dbWriteContext, 
            ApplicationUser user)
        {
            using var transaction = dbWriteContext.Connection.BeginTransaction();

            try
            {
                var wasUserInvited = TryGetUserInvitation(
                    user,
                    dbWriteContext,
                    transaction,
                    out var invitation);

                if (wasUserInvited)
                {
                    user.Id = invitation.UserExternalId.Value;
                    user.DatabaseId = invitation.UserId;

                    UpdateInvitedUser(
                       user,
                       dbWriteContext,
                       transaction);
                }
                else
                {
                    user.Id = UserExtId.NewId().Value;

                    var newUser = CreateNewUser(
                        user,
                        dbWriteContext,
                        transaction);

                    user.DatabaseId = newUser.Id;
                }

                if (user.SelectedCheckboxIds is { Count: > 0 })
                {
                    SaveUserSelectedCheckboxIds(
                        user,
                        dbWriteContext,
                        transaction);
                }

                transaction.Commit();

                return IdentityResult.Success;
            }
            catch (Exception e)
            {
                transaction.Rollback();

                Log.Error(e, "Something went wrong while creating user.");

                return IdentityResult.Failed(new IdentityError
                {
                    Code = string.Empty,
                    Description = $"User '{user.UserName}' could not be created."
                });
            }
        }

        private static void SaveUserSelectedCheckboxIds(
            ApplicationUser user,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            dbWriteContext
                .Cmd(sql: """
                        INSERT INTO usuc_user_sign_up_checkboxes (
                            usuc_user_id,
                            usuc_sign_up_checkbox_id
                        )
                        SELECT
                            $userId,
                            value
                        FROM 
                            json_each($checkboxIds)
                        RETURNING
                            usuc_sign_up_checkbox_id
                        """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$userId", user.DatabaseId)
                .WithJsonParameter("$checkboxIds", user.SelectedCheckboxIds)
                .Execute();
        }

        private static void UpdateInvitedUser(
            ApplicationUser user,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            dbWriteContext
                .OneRowCmd(
                    sql: """
                        UPDATE u_users
                        SET 
                            u_user_name = $userName,
                            u_normalized_user_name = $normalizedUserName,
                            u_email = $email,
                            u_normalized_email = $normalizedEmail,
                            u_email_confirmed = $emailConfirmed,
                            u_password_hash = $passwordHash,
                            u_security_stamp = $securityStamp,
                            u_concurrency_stamp = $concurrencyStamp,
                            u_phone_number = $phoneNumber,
                            u_phone_number_confirmed = $phoneNumberConfirmed,
                            u_two_factor_enabled = $twoFactorEnabled,
                            u_lockout_end = $lockoutEnd,
                            u_lockout_enabled = $lockoutEnabled,
                            u_access_failed_count = $accessFailedCount,
                            u_is_invitation = FALSE
                        WHERE u_id = $invitedUserId
                        RETURNING u_id;
                        """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$invitedUserId", user.DatabaseId)
                .WithParameter("$userName", user.UserName)
                .WithParameter("$normalizedUserName", user.NormalizedUserName)
                .WithParameter("$email", user.Email)
                .WithParameter("$normalizedEmail", user.NormalizedEmail)
                .WithParameter("$emailConfirmed", user.EmailConfirmed)
                .WithParameter("$passwordHash", user.PasswordHash)
                .WithParameter("$securityStamp", user.SecurityStamp)
                .WithParameter("$concurrencyStamp", user.ConcurrencyStamp)
                .WithParameter("$phoneNumber", user.PhoneNumber)
                .WithParameter("$phoneNumberConfirmed", user.PhoneNumberConfirmed)
                .WithParameter("$twoFactorEnabled", user.TwoFactorEnabled)
                .WithParameter("$lockoutEnd", user.LockoutEnd)
                .WithParameter("$lockoutEnabled", user.LockoutEnabled)
                .WithParameter("$accessFailedCount", user.AccessFailedCount)
                .ExecuteOrThrow();
        }

        private NewUser CreateNewUser(
            ApplicationUser user,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction)
        {
            var maxWorkspaceNumber = user.IsAppOwner
                ? null
                : _appSettings.NewUserDefaultMaxWorkspaceNumber.Value;

            var defaultMaxWorkspaceSizeInBytes = user.IsAppOwner
                ? null
                : _appSettings.NewUserDefaultMaxWorkspaceSizeInBytes.Value;

            var newUser = dbWriteContext
                .OneRowCmd(
                    sql: """
                        INSERT INTO u_users(
                            u_external_id,
                            u_user_name,
                            u_normalized_user_name,
                            u_email,
                            u_normalized_email,
                            u_email_confirmed,
                            u_password_hash,
                            u_security_stamp,
                            u_concurrency_stamp,
                            u_phone_number,
                            u_phone_number_confirmed,
                            u_two_factor_enabled,
                            u_lockout_end,
                            u_lockout_enabled,
                            u_access_failed_count,
                            u_is_invitation,
                            u_invitation_code,
                            u_max_workspace_number,
                            u_default_max_workspace_size_in_bytes
                        ) VALUES (
                            $externalId,
                            $userName,
                            $normalizedUserName,
                            $email,
                            $normalizedEmail,
                            $emailConfirmed,
                            $passwordHash,
                            $securityStamp,
                            $concurrencyStamp,
                            $phoneNumber,
                            $phoneNumberConfirmed,
                            $twoFactorEnabled,
                            $lockoutEnd,
                            $lockoutEnabled,
                            $accessFailedCount,
                            FALSE,
                            NULL,
                            $maxWorkspaceNumber,
                            $defaultMaxWorkspaceSizeInBytes
                        )
                        RETURNING u_id;
                        """,
                    readRowFunc: reader => new NewUser(
                        Id: reader.GetInt32(0)),
                    transaction: transaction)
                .WithParameter("$externalId", user.Id)
                .WithParameter("$userName", user.UserName)
                .WithParameter("$normalizedUserName", user.NormalizedUserName)
                .WithParameter("$email", user.Email)
                .WithParameter("$normalizedEmail", user.NormalizedEmail)
                .WithParameter("$emailConfirmed", user.EmailConfirmed)
                .WithParameter("$passwordHash", user.PasswordHash)
                .WithParameter("$securityStamp", user.SecurityStamp)
                .WithParameter("$concurrencyStamp", user.ConcurrencyStamp)
                .WithParameter("$phoneNumber", user.PhoneNumber)
                .WithParameter("$phoneNumberConfirmed", user.PhoneNumberConfirmed)
                .WithParameter("$twoFactorEnabled", user.TwoFactorEnabled)
                .WithParameter("$lockoutEnd", user.LockoutEnd)
                .WithParameter("$lockoutEnabled", user.LockoutEnabled)
                .WithParameter("$accessFailedCount", user.AccessFailedCount)
                .WithParameter("$maxWorkspaceNumber", maxWorkspaceNumber)
                .WithParameter("$defaultMaxWorkspaceSizeInBytes", defaultMaxWorkspaceSizeInBytes)
                .ExecuteOrThrow();

            //we don't assign any permissions and roles for app owners
            //as this logic is controlled internally by other settings
            if (user.IsAppOwner)
                return newUser;

            var defaultIsAdmin = _appSettings
                .NewUserDefaultPermissionsAndRoles
                .IsAdmin;

            if (defaultIsAdmin)
            {
                UpdateUserPermissionsAndRoleQuery.AddAdminRole(
                    userId: newUser.Id,
                    adminRoleId: _appSettings.AdminRoleId,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            }

            var defaultPermissions = _appSettings
                .NewUserDefaultPermissionsAndRoles
                .GetPermissions();

            if(defaultPermissions.Any())
            {
                UpdateUserPermissionsAndRoleQuery.AddPermissions(
                    userId: newUser.Id,
                    isAdmin: defaultIsAdmin,
                    permissions: defaultPermissions,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);
            }

            return newUser;
        }

        private static bool TryGetUserInvitation(
            ApplicationUser user,
            DbWriteQueue.Context dbWriteContext,
            SqliteTransaction transaction,
            out UserInivation userInvitation)
        {
            var result = dbWriteContext
                .OneRowCmd(
                    sql: """
                        SELECT 
                            u_id,
                            u_external_id
                        FROM u_users                            
                        WHERE 
                            u_normalized_user_name = $normalizedUserName
                            AND u_is_invitation = TRUE
                        LIMIT 1                 
                        """,
                    readRowFunc: reader => new UserInivation(
                        UserId: reader.GetInt32(0),
                        UserExternalId: reader.GetExtId<UserExtId>(1)),
                    transaction: transaction)
                .WithParameter("$normalizedUserName", user.NormalizedUserName)
                .Execute();

            if(result.IsEmpty)
            {
                userInvitation = default;
                return false;
            }

            userInvitation = result.Value;
            return true;
        }

        public override async Task<IdentityResult> DeleteAsync(
            ApplicationUser user, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            // const string sql = @"
            //     DELETE
            //     FROM users
            //     WHERE id = @Id;
            // ";

            // await using var connection = new NpgsqlConnection(
            //     connectionString: _config.DbConnectionString);
            //
            // await connection.OpenAsync(cancellationToken);
            //
            // var rowsDeleted = await connection.ExecuteAsync(
            //     sql: sql, 
            //     param: new
            //     {
            //         Id = user.Id
            //     });
            //
            // return  rowsDeleted == 1 ? IdentityResult.Success : IdentityResult.Failed(new IdentityError {
            //     Code = string.Empty,
            //     Description = $"User '{user.UserName}' could not be deleted."
            // });

            return IdentityResult.Success;
        }
        
        public override async Task<ApplicationUser?> FindByEmailAsync(
            string normalizedEmail, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .OneRowCmd(
                    sql: @"
                        SELECT
                            u_id,
                            u_external_id,
                            u_user_name,
                            u_normalized_user_name,
                            u_email,
                            u_normalized_email,
                            u_email_confirmed,
                            u_password_hash,
                            u_security_stamp,
                            u_concurrency_stamp,
                            u_phone_number,
                            u_phone_number_confirmed,
                            u_two_factor_enabled,
                            u_lockout_end,
                            u_lockout_enabled,
                            u_access_failed_count
                        FROM u_users
                        WHERE 
                            u_normalized_email = $userNormalizedEmail
                            AND u_is_invitation = FALSE
                        LIMIT 1
                    ",
                    readRowFunc: reader => new ApplicationUser
                    {
                        DatabaseId = reader.GetInt32(0),
                        Id = reader.GetString(1),
                        UserName = reader.GetString(2),
                        NormalizedUserName = reader.GetString(3),
                        Email = reader.GetString(4),
                        NormalizedEmail = reader.GetString(5),
                        EmailConfirmed = reader.GetBoolean(6),
                        PasswordHash = reader.GetString(7),
                        SecurityStamp = reader.GetString(8),
                        ConcurrencyStamp = reader.GetString(9),
                        PhoneNumber = reader.GetStringOrNull(10),
                        PhoneNumberConfirmed = reader.GetBoolean(11),
                        TwoFactorEnabled = reader.GetBoolean(12),
                        LockoutEnd = reader.GetDateTimeOffsetOrNull(13),
                        LockoutEnabled = reader.GetBoolean(14),
                        AccessFailedCount = reader.GetInt32(15)
                    })
                .WithParameter("$userNormalizedEmail", normalizedEmail)
                .Execute();

            return result.IsEmpty
                ? null
                : result.Value;
        }
        
        public override async Task<ApplicationUser?> FindByIdAsync(
            string userId, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .OneRowCmd(
                    sql: @"
                        SELECT
                            u_id,
                            u_external_id,
                            u_user_name,
                            u_normalized_user_name,
                            u_email,
                            u_normalized_email,
                            u_email_confirmed,
                            u_password_hash,
                            u_security_stamp,
                            u_concurrency_stamp,
                            u_phone_number,
                            u_phone_number_confirmed,
                            u_two_factor_enabled,
                            u_lockout_end,
                            u_lockout_enabled,
                            u_access_failed_count
                        FROM u_users
                        WHERE 
                            u_external_id = $userExternalId
                            AND u_is_invitation = FALSE
                        LIMIT 1
                    ",
                    readRowFunc: reader => new ApplicationUser
                    {
                        DatabaseId = reader.GetInt32(0),
                        Id = reader.GetString(1),
                        UserName = reader.GetString(2),
                        NormalizedUserName = reader.GetString(3),
                        Email = reader.GetString(4),
                        NormalizedEmail = reader.GetString(5),
                        EmailConfirmed = reader.GetBoolean(6),
                        PasswordHash = reader.GetString(7),
                        SecurityStamp = reader.GetString(8),
                        ConcurrencyStamp = reader.GetString(9),
                        PhoneNumber = reader.GetStringOrNull(10),
                        PhoneNumberConfirmed = reader.GetBoolean(11),
                        TwoFactorEnabled = reader.GetBoolean(12),
                        LockoutEnd = reader.GetDateTimeOffsetOrNull(13),
                        LockoutEnabled = reader.GetBoolean(14),
                        AccessFailedCount = reader.GetInt32(15)
                    })
                .WithParameter("$userExternalId", userId)
                .Execute();

            return result.IsEmpty
                ? null
                : result.Value;
        }
        
        protected override Task<ApplicationUser?> FindUserAsync(
            string userId, 
            CancellationToken cancellationToken) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            return FindByIdAsync(userId, cancellationToken);
        }
        
        public override async Task<ApplicationUser?> FindByNameAsync(
            string normalizedUserName, 
            CancellationToken cancellationToken = default) 
        {
              cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .OneRowCmd(
                    sql: @"
                        SELECT
                            u_id,
                            u_external_id,
                            u_user_name,
                            u_normalized_user_name,
                            u_email,
                            u_normalized_email,
                            u_email_confirmed,
                            u_password_hash,
                            u_security_stamp,
                            u_concurrency_stamp,
                            u_phone_number,
                            u_phone_number_confirmed,
                            u_two_factor_enabled,
                            u_lockout_end,
                            u_lockout_enabled,
                            u_access_failed_count
                        FROM u_users
                        WHERE 
                            u_normalized_user_name = $normalizedUserName
                            AND u_is_invitation = FALSE
                        LIMIT 1
                    ",
                    readRowFunc: reader => new ApplicationUser
                    {
                        DatabaseId = reader.GetInt32(0),
                        Id = reader.GetString(1),
                        UserName = reader.GetString(2),
                        NormalizedUserName = reader.GetString(3),
                        Email = reader.GetString(4),
                        NormalizedEmail = reader.GetString(5),
                        EmailConfirmed = reader.GetBoolean(6),
                        PasswordHash = reader.GetString(7),
                        SecurityStamp = reader.GetString(8),
                        ConcurrencyStamp = reader.GetString(9),
                        PhoneNumber = reader.GetStringOrNull(10),
                        PhoneNumberConfirmed = reader.GetBoolean(11),
                        TwoFactorEnabled = reader.GetBoolean(12),
                        LockoutEnd = reader.GetDateTimeOffsetOrNull(13),
                        LockoutEnabled = reader.GetBoolean(14),
                        AccessFailedCount = reader.GetInt32(15)
                    })
                .WithParameter("$normalizedUserName", normalizedUserName)
                .Execute();

            return result.IsEmpty
                ? null
                : result.Value;
        }
        
        public override async Task<IList<Claim>> GetClaimsAsync(
            ApplicationUser user, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .Cmd(
                    sql: @"
                        SELECT 
                            uc_claim_type,
                            uc_claim_value
                        FROM uc_user_claims
                        WHERE uc_user_id = $userId
                    ",
                    readRowFunc: reader => new Claim(
                        type: reader.GetString(0),
                        value: reader.GetString(1)))
                .WithParameter("$userId", user.DatabaseId)
                .Execute();

            return result;
        }
        
        public override async Task<IList<UserLoginInfo>> GetLoginsAsync(
            ApplicationUser user,
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            return [];
        }
        
        public override async Task<ApplicationUser?> FindByLoginAsync(
            string loginProvider, 
            string providerKey, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            return null;
        }
        
        public override async Task<IList<string>> GetRolesAsync(
            ApplicationUser user, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .Cmd(
                    sql: @"
                        SELECT r_name
                        FROM r_roles
                        INNER JOIN ur_user_roles
                            ON ur_role_id = r_id
                        WHERE ur_user_id = $userId
                    ",
                    readRowFunc: reader => reader.GetString(0))
                .WithParameter("$userId", user.DatabaseId)
                .Execute();

            return result;
        }
        
        public override async Task<IList<ApplicationUser>> GetUsersForClaimAsync(
            Claim claim, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (claim == null) {
                throw new ArgumentNullException(nameof(claim), $"Parameter {nameof(claim)} cannot be null.");
            }

            return [];
        }

        
        public override async Task<IList<ApplicationUser>> GetUsersInRoleAsync(
            string normalizedRoleName, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (string.IsNullOrEmpty(normalizedRoleName)) {
                throw new ArgumentNullException(nameof(normalizedRoleName));
            }

            return [];
        }

        
        public override async Task<bool> IsInRoleAsync(
            ApplicationUser user, 
            string normalizedRoleName, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (string.IsNullOrEmpty(normalizedRoleName)) {
                throw new ArgumentException(nameof(normalizedRoleName));
            }

            return false;
        }

        
        public override async Task RemoveClaimsAsync(
            ApplicationUser user, 
            IEnumerable<Claim> claims, 
            CancellationToken cancellationToken = default) 
        {
            ThrowIfDisposed();
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (claims == null) {
                throw new ArgumentNullException(nameof(claims), $"Parameter {nameof(claims)} cannot be null.");
            }

            throw new NotImplementedException();
        }

        
        public override async Task RemoveFromRoleAsync(
            ApplicationUser user, 
            string normalizedRoleName,
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (string.IsNullOrEmpty(normalizedRoleName)) {
                throw new ArgumentException(nameof(normalizedRoleName));
            }
        }
        
        public override async Task RemoveLoginAsync(
            ApplicationUser user, 
            string loginProvider, 
            string providerKey, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            throw new NotImplementedException();
        }
        
        public override async Task ReplaceClaimAsync(
            ApplicationUser user,
            Claim claim, 
            Claim newClaim, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            if (claim == null) {
                throw new ArgumentNullException(nameof(claim), $"Parameter {nameof(claim)} cannot be null.");
            }

            if (newClaim == null) {
                throw new ArgumentNullException(nameof(newClaim), $"Parameter {nameof(newClaim)} cannot be null.");
            }

            throw new NotImplementedException();
        }
        
        public override async Task<IdentityResult> UpdateAsync(
            ApplicationUser user, 
            CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (user == null) {
                throw new ArgumentNullException(nameof(user), $"Parameter {nameof(user)} cannot be null.");
            }

            using var connection = _plikShareDb.OpenConnection();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var newConcurrencyStamp = Guid.NewGuid().ToString().ToUpperInvariant();
                
                var result = connection
                    .OneRowCmd(
                        sql: @"                 
                            UPDATE u_users
                            SET 
                                u_user_name = $userName,
                                u_normalized_user_name = $normalizedUserName,
                                u_email = $email,
                                u_normalized_email = $normalizedEmail,
                                u_email_confirmed = $emailConfirmed,
                                u_password_hash = $passwordHash,
                                u_security_stamp = $securityStamp,
                                u_concurrency_stamp = $newConcurrencyStamp,
                                u_phone_number = $phoneNumber,
                                u_phone_number_confirmed = $phoneNumberConfirmed,
                                u_two_factor_enabled = $twoFactorEnabled,
                                u_lockout_end = $lockoutEnd,
                                u_lockout_enabled = $lockoutEnabled,
                                u_access_failed_count = $accessFailedCount
                            WHERE 
                                u_id = $userId
                                AND u_concurrency_stamp = $concurrencyStamp
                                AND u_is_invitation = FALSE
                            RETURNING u_id
                        ",
                        readRowFunc: reader => reader.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$userId", user.DatabaseId)
                    .WithParameter("$concurrencyStamp", user.ConcurrencyStamp)
                    .WithParameter("$newConcurrencyStamp", newConcurrencyStamp)
                    .WithParameter("$userName", user.UserName)
                    .WithParameter("$normalizedUserName", user.NormalizedUserName)
                    .WithParameter("$email", user.Email)
                    .WithParameter("$normalizedEmail", user.NormalizedEmail)
                    .WithParameter("$emailConfirmed", user.EmailConfirmed)
                    .WithParameter("$passwordHash", user.PasswordHash)
                    .WithParameter("$securityStamp", user.SecurityStamp)
                    .WithParameter("$phoneNumber", user.PhoneNumber)
                    .WithParameter("$phoneNumberConfirmed", user.PhoneNumberConfirmed)
                    .WithParameter("$twoFactorEnabled", user.TwoFactorEnabled)
                    .WithParameter("$lockoutEnd", user.LockoutEnd)
                    .WithParameter("$lockoutEnabled", user.LockoutEnabled)
                    .WithParameter("$accessFailedCount", user.AccessFailedCount)
                    .Execute();

                if (result.IsEmpty || result.Value != user.DatabaseId)
                {
                    throw new InvalidOperationException("Something went wrong while updating a user");
                }
               
                transaction.Commit();
                
                user.ConcurrencyStamp = newConcurrencyStamp;
                
                await _userCache.InvalidateEntry(
                    userId: result.Value,
                    cancellationToken: cancellationToken);

                return IdentityResult.Success;
            }
            catch (Exception e)
            {
                transaction.Rollback();
                
                Log.Error(e, "Something went wrong while updating user.");
            
                return IdentityResult.Failed(new IdentityError
                {
                    Code = string.Empty,
                    Description = $"User '{user.UserName}' could not be updated."
                });
            }
        }

        
        protected override async Task<ApplicationUserToken?> FindTokenAsync(
            ApplicationUser user, 
            string loginProvider, 
            string name, 
            CancellationToken cancellationToken) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using var connection = _plikShareDb.OpenConnection();

            var tokenResult = connection
                .OneRowCmd(
                    sql: @"
                        SELECT ut_value
                        FROM ut_user_tokens
                        WHERE
                            ut_user_id = $userId
                            AND ut_login_provider = $loginProvider
                            AND ut_name = $tokenName
                        LIMIT 1
                    ",
                    readRowFunc: reader => reader.GetString(0))
                .WithParameter("$userId", user.DatabaseId)
                .WithParameter("$loginProvider", loginProvider)
                .WithParameter("$tokenName", name)
                .Execute();

            return tokenResult.IsEmpty
                ? null
                : new ApplicationUserToken
                {
                    Value = tokenResult.Value,
                    LoginProvider = loginProvider,
                    Name = name,
                    UserId = user.Id
                };
        }
        
        protected override async Task AddUserTokenAsync(ApplicationUserToken token) {
            if (token == null) {
                throw new ArgumentNullException(nameof(token), $"Parameter {nameof(token)} cannot be null.");
            }

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .OneRowCmd(
                    sql: @"
                        INSERT INTO ut_user_tokens(
                            ut_user_id,
                            ut_login_provider, 
                            ut_name,
                            ut_value
                        ) VALUES (
                            (SELECT u_id FROM u_users WHERE u_external_id = $userExternalId),
                            $loginProvider,
                            $tokenName,
                            $tokenValue
                        )
                        ON CONFLICT(ut_user_id, ut_login_provider, ut_name)
                        DO UPDATE SET ut_value = $tokenValue
                        RETURNING 
                            ut_user_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0))
                .WithParameter("$userExternalId", token.UserId)
                .WithParameter("$loginProvider", token.LoginProvider)
                .WithParameter("$tokenName", token.Name)
                .WithParameter("$tokenValue", token.Value)
                .Execute();

            if (result.IsEmpty)
            {
                throw new InvalidOperationException(
                    $"Something went wrong while inserting token '{token.LoginProvider}:{token.Name}' for user '{token.UserId}'");
            }
        }

        public override Task SetTokenAsync(
            ApplicationUser user, 
            string loginProvider, 
            string name, 
            string? value,
            CancellationToken cancellationToken)
        {
            //its implemented like this because in original implementation
            //set token was checking if token exists, if not it was adding it and if so
            //it was only updating its value in memory, without saving to database
            //my implementation of AddUserTokenAsync can handle upserts correctly
            //so i simply call it
            return AddUserTokenAsync(new ApplicationUserToken
            {
                Value = value,
                LoginProvider = loginProvider,
                Name = name,
                UserId = user.Id
            });
        }

        protected override async Task RemoveUserTokenAsync(ApplicationUserToken token) {
            throw new NotImplementedException();
        }
        
        protected override async Task<ApplicationRole?> FindRoleAsync(
            string normalizedRoleName, 
            CancellationToken cancellationToken) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            return null;
        }

        
        protected override async Task<TUserRole?> FindUserRoleAsync(
            string userId, 
            string roleId, 
            CancellationToken cancellationToken) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            return null;
        }
        
        protected override async Task<TUserLogin?> FindUserLoginAsync(
            string loginProvider, 
            string providerKey, 
            CancellationToken cancellationToken) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            return null;
        }

        
        protected override async Task<TUserLogin?> FindUserLoginAsync(
            string userId, 
            string loginProvider, 
            string providerKey, 
            CancellationToken cancellationToken) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            return null;
        }

        public readonly record struct UserInivation(
            int UserId,
            UserExtId UserExternalId);

        public readonly record struct NewUser(
            int Id);
    }
}
