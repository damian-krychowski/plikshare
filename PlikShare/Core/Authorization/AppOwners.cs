using PlikShare.Users.Cache;
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

    public async ValueTask<List<UserContext>> OwnerContexts(
        UserCache cache,
        CancellationToken cancellationToken)
    {
        var result = new List<UserContext>();

        foreach (var owner in owners)
        {
            var context = await cache.GetOrThrow(
                email: owner,
                cancellationToken: cancellationToken);

            result.Add(context);
        }

        return result;
    }

    public bool IsAppOwner(string email) 
        => owners.Any(owner => owner.IsEqualTo(email));
    
    public bool IsAppOwner(Email email) 
        => owners.Any(owner => owner == email);
}