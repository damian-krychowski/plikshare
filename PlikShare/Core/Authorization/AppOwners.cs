using PlikShare.Core.IdentityProvider;
using PlikShare.Users.Entities;

namespace PlikShare.Core.Authorization;

public class AppOwners(List<Email> owners, string initialPassword)
{
    public string InitialPassword { get; } = initialPassword;

    public IEnumerable<Email> Owners()
    {
        foreach (var owner in owners)
        {
            yield return owner;
        }
    }

    public bool IsAppOwner(string email) 
        => owners.Any(owner => owner.IsEqualTo(email));
    
    public bool IsAppOwner(Email email) 
        => owners.Any(owner => owner == email);
}