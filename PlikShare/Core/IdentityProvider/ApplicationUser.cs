using Microsoft.AspNetCore.Identity;

namespace PlikShare.Core.IdentityProvider;

public class ApplicationUser: IdentityUser
{
    public int DatabaseId { get; set; }

    public ApplicationUser()
    {
        SecurityStamp = Guid.NewGuid().ToString();
    }
    
    public ApplicationUser(string userName)
        : this()
    {
        UserName = userName;
    }
}