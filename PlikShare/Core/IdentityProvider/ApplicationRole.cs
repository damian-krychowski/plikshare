using Microsoft.AspNetCore.Identity;

namespace PlikShare.Core.IdentityProvider;

public class ApplicationRole : IdentityRole
{
    public int DatabaseId { get; set; }
}

public class ApplicationUserToken : IdentityUserToken<string>
{
    
}