using PlikShare.Users.Cache;
using PlikShare.Users.Entities;
using PlikShare.Users.Id;

namespace PlikShare.Core.Authorization;

public class AppOwner(Email email)
{
    private UserExtId? _externalId;

    public Email Email { get; } = email;

    public UserExtId ExternalId =>
        _externalId ?? throw new InvalidOperationException(
            $"AppOwner '{Email.Value}' ExternalId has not been initialized yet. " +
            $"Make sure InitializeAppOwners has been called during application startup before accessing ExternalId.");

    internal void SetExternalId(UserExtId externalId)
    {
        if (_externalId is not null)
            throw new InvalidOperationException(
                $"AppOwner '{Email.Value}' ExternalId has already been set and cannot be changed.");

        _externalId = externalId;
    }
}

public class AppOwners
{
    private readonly List<AppOwner> _owners;

    public string InitialPassword { get; }

    public AppOwners(List<Email> emails, string initialPassword)
    {
        _owners = emails.Select(e => new AppOwner(e)).ToList();
        InitialPassword = initialPassword;
    }

    public IEnumerable<AppOwner> Owners()
    {
        foreach (var owner in _owners)
        {
            yield return owner;
        }
    }

    public IEnumerable<Email> Emails()
    {
        foreach (var owner in _owners)
        {
            yield return owner.Email;
        }
    }

    public async ValueTask<List<UserContext>> OwnerContexts(
        UserCache cache,
        CancellationToken cancellationToken)
    {
        var result = new List<UserContext>();

        foreach (var owner in _owners)
        {
            var context = await cache.GetOrThrow(
                userExternalId: owner.ExternalId,
                cancellationToken: cancellationToken);

            result.Add(context);
        }

        return result;
    }

    public bool IsAppOwner(string email)
        => _owners.Any(owner => owner.Email.IsEqualTo(email));

    public bool IsAppOwner(Email email)
        => _owners.Any(owner => owner.Email == email);
}