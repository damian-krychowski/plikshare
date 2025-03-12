using Microsoft.AspNetCore.Identity;

namespace PlikShare.Core.IdentityProvider
{
    /// <summary>
    /// Extension methods on <see cref="IdentityBuilder"/> class.
    /// </summary>
    public static class SQLiteIdentityBuilderExtensions
    {
        public static IdentityBuilder AddSQLiteStores(
            this IdentityBuilder builder)
        {
            builder.Services.AddScoped(
                typeof(IRoleStore<ApplicationRole>),
                typeof(SQLiteIdentityRoleStore<IdentityUserRole<string>, IdentityRoleClaim<string>>));

            builder.Services.AddScoped(
                typeof(IUserStore<ApplicationUser>), 
                typeof(SqLiteIdentityUserStore<IdentityUserClaim<string>,IdentityUserRole<string>,IdentityUserLogin<string>,IdentityRoleClaim<string>>));
            
            return builder;
        }
        
        public static IdentityBuilder AddPlikShareSecurityStampValidator(
            this IdentityBuilder builder)
        {
            builder.Services.AddScoped(
                typeof(ISecurityStampValidator),
                typeof(PlikShareSecurityStampValidator));
            
            return builder;
        }
    }
}
