using PlikShare.Core.Utils;

namespace PlikShare.IntegrationTests.Infrastructure;

public class RandomGenerator
{
    public string Email(string? prefix = null)
    {
        var pref = prefix is null ? "" : $"{prefix}_";

        return $"{pref}email_{Guid.NewGuid().ToBase62().ToLowerInvariant()}@plikshare.com";
    }

    public string InvitationCode(string? prefix = null)
    {
        var pref = prefix is null ? "" : $"{prefix}_";

        return $"{pref}invitation_code_{Guid.NewGuid().ToBase62()}";
    }

    public string Password()
    {
        return $"password_{Guid.NewGuid().ToBase62()}_%Aa123";
    }

    public string Name(string baseName)
    {
        return $"{baseName}-{Guid.NewGuid().ToBase62()}";
    }
}