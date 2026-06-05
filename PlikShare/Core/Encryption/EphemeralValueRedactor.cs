using System.Text.RegularExpressions;

namespace PlikShare.Core.Encryption;

public static partial class EphemeralValueRedactor
{
    public static string? Redact(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return json;

        if (!json.Contains(EphemeralKeyRing.ReservedPrefix, StringComparison.Ordinal))
            return json;

        return EphemeralValueRegex().Replace(json, "eph:[redacted]");
    }

    [GeneratedRegex(@"eph:[A-Za-z0-9+/=]+")]
    private static partial Regex EphemeralValueRegex();
}
