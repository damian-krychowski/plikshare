using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

// ReSharper disable InconsistentNaming
// ReSharper disable InvalidXmlDocComment
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace PlikShare.Core.IdentityProvider
{
    public class SQLiteIdentityRoleStore<TUserRole, TRoleClaim> : RoleStoreBase<ApplicationRole, string, TUserRole, TRoleClaim>
        where TUserRole : IdentityUserRole<string>, new()
        where TRoleClaim : IdentityRoleClaim<string>, new()
    {
        private readonly PlikShareDb _plikShareDb;


        /// <summary>
        /// Constructs a new instance of <see cref="SQLiteIdentityRoleStore{TRole,TKey,TUserRole,TRoleClaim}"/>.
        /// </summary>
        /// <param name="rolesTable">Abstraction for interacting with Roles table.</param>
        /// <param name="roleClaimsTable">Abstraction for interacting with RoleClaims table.</param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public SQLiteIdentityRoleStore(
            PlikShareDb plikShareDb,
            IdentityErrorDescriber describer) : base(describer)
        {
            _plikShareDb = plikShareDb;
            ErrorDescriber = describer ?? new IdentityErrorDescriber();
        }

        
        public override IQueryable<ApplicationRole> Roles => throw new NotSupportedException();

        
        public override async Task AddClaimAsync(ApplicationRole role, Claim claim, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            if (claim == null) {
                throw new ArgumentNullException(nameof(claim), $"Parameter {nameof(claim)} cannot be null.");
            }

            throw new NotImplementedException();
        }

        
        public override async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }
            
            throw new NotImplementedException();
        }

        
        public override async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }
            
            throw new NotImplementedException();
        }

        
        public override async Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            throw new NotImplementedException();
        }

        
        public override async Task<ApplicationRole?> FindByNameAsync(
            string normalizedName, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();

            using var connection = _plikShareDb.OpenConnection();

            var result = connection
                .OneRowCmd(
                    sql: @"
                        SELECT
                            r_id,
                            r_external_id,
                            r_name,
                            r_normalized_name,
                            r_concurrency_stamp
                        FROM r_roles
                        WHERE 
                            r_normalized_name = $normalizedName
                        LIMIT 1
                    ",
                    readRowFunc: reader => new ApplicationRole
                    {
                        DatabaseId = reader.GetInt32(0),
                        Id = reader.GetString(1),
                        Name = reader.GetString(2),
                        NormalizedName = reader.GetString(3),
                        ConcurrencyStamp = reader.GetString(4)
                    })
                .WithParameter("$normalizedName", normalizedName)
                .Execute();

            return result.IsEmpty
                ? null
                : result.Value;
        }

        
        public override async Task<IList<Claim>> GetClaimsAsync(
            ApplicationRole role, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            using var connection = _plikShareDb.OpenConnection();

            var claims = connection
                .Cmd(
                    sql: @" 
                        SELECT 
                            rc_claim_type,
                            rc_claim_value
                        FROM rc_role_claims
                        WHERE rc_role_id = $roleId
                    ",
                    readRowFunc: reader => new Claim(
                        type: reader.GetString(0),
                        value: reader.GetString(1)))
                .WithParameter("$roleId", role.Id)
                .Execute();

            return claims;
        }

        
        public override Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            return Task.FromResult(role.NormalizedName);
        }

        
        public override Task<string> GetRoleIdAsync(
            ApplicationRole role, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            return Task.FromResult(ConvertIdToString(role.Id)!);
        }

        
        public override Task<string?> GetRoleNameAsync(
            ApplicationRole role,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            return Task.FromResult(role.Name);
        }

        
        public override async Task RemoveClaimAsync(
            ApplicationRole role, 
            Claim claim, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            if (claim == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            
            throw new NotImplementedException();
        }

        
        public override Task SetNormalizedRoleNameAsync(
            ApplicationRole role, 
            string? normalizedName, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            role.NormalizedName = normalizedName;
            return Task.CompletedTask;
        }

        
        public override Task SetRoleNameAsync(
            ApplicationRole role, 
            string? roleName, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            role.Name = roleName;
            return Task.CompletedTask;
        }

        
        public override async Task<IdentityResult> UpdateAsync(
            ApplicationRole role, 
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            if (role == null) {
                throw new ArgumentNullException(nameof(role), $"Parameter {nameof(role)} cannot be null.");
            }

            
            throw new NotImplementedException();
        }
    }
}
