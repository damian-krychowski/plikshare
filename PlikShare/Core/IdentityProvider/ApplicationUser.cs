using Microsoft.AspNetCore.Identity;

namespace PlikShare.Core.IdentityProvider;

public class ApplicationUser: IdentityUser
{
    public int DatabaseId { get; set; }
    public List<int> SelectedCheckboxIds { get; set; }
    public bool IsAppOwner { get; set; } = false;

    public ApplicationUser()
    {
        SecurityStamp = Guid.NewGuid().ToString();
        SelectedCheckboxIds = [];
    }
    
    public ApplicationUser(string userName)
        : this()
    {
        UserName = userName;
    }
}