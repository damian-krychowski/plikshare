using PlikShare.Core.IdentityProvider;
using PlikShare.Users.Entities;

namespace PlikShare.Core.Authorization;

public class AppOwners
{
    private readonly List<Email> _owners;
    public string InitialPassword { get; }

    public AppOwners(List<Email> owners, string initialPassword)
    {
        _owners = owners;
        InitialPassword = initialPassword;
    }

    public IEnumerable<Email> Owners()
    {
        foreach (var owner in _owners)
        {
            yield return owner;
        }
    }

    public bool IsAppOwner(string email) 
        => _owners.Any(owner => owner.IsEqualTo(email));
    
    public bool IsAppOwner(Email email) 
        => _owners.Any(owner => owner == email);
    
    public bool IsAppOwner(ApplicationUser user) 
        => user.Email is not null && _owners.Any(owner => owner.IsEqualTo(user.Email));
}