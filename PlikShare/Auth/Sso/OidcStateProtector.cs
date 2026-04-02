using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace PlikShare.Auth.Sso;

public class OidcStateProtector
{
    private readonly IDataProtector _protector;
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    public OidcStateProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(
            "PlikShare.Sso.OidcState");
    }

    public string CreateState(
        string providerExternalId,
        string nonce,
        string codeVerifier)
    {
        var payload = JsonSerializer.Serialize(
            new OidcState(
                ProviderExternalId: providerExternalId,
                Nonce: nonce,
                CodeVerifier: codeVerifier,
                CreatedAtUtc: DateTime.UtcNow));

        return _protector.Protect(payload);
    }

    public OidcState? ValidateState(string protectedState)
    {
        try
        {
            var json = _protector.Unprotect(protectedState);
            var state = JsonSerializer.Deserialize<OidcState>(json);

            if (state is null)
            {
                return null;
            }

            if (DateTime.UtcNow - state.CreatedAtUtc > MaxAge)
            {
                return null;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }
}

public record OidcState(
    string ProviderExternalId,
    string Nonce,
    string CodeVerifier,
    DateTime CreatedAtUtc);
