using System.Text;

namespace PlikShare.Users.Entities;

public record Email
{
    public string Value { get; }
    
    public Email(string email)
    {
        Value = email.ToLowerInvariant();
    }
    
    public string Anonymize() => EmailAnonymization.Anonymize(Value);
    public string Normalized => Normalize(Value);

    public bool IsEqualTo(string email) => email.Equals(Value, StringComparison.InvariantCultureIgnoreCase);
    public static string Normalize(string email) => email.ToUpperInvariant();
}

public static class EmailAnonymization
{
    private static readonly string[] AtOrEncodedAt = ["@", "%40"];

    public static string Anonymize(string email)
    {
        var parts = email.Split(
            AtOrEncodedAt, 
            StringSplitOptions.None);

        if (parts.Length < 2)
            throw new ArgumentException("Invalid email format.", nameof(email));

        var anonymizedLocal = AnonymizeLocal(
            local: parts[0]);

        var anonymizedDomain = AnonymizedDomain(
            domain: parts[1]);

        return $"{anonymizedLocal}@{anonymizedDomain}";
    }

    private static string AnonymizeLocal(string local)
    {
        if (local.Length < 4)
            return local;

        var firstPartEnd = (int)Math.Floor(local.Length * 0.25);
        var lastPartStart = (int)Math.Ceiling(local.Length * 0.75);

        var firstPart = local.Substring(0, firstPartEnd);
        var middlePart = new string('*', lastPartStart - firstPartEnd);
        var lastPart = local.Substring(lastPartStart, local.Length - lastPartStart);

        var anonymizedLocal = $"{firstPart}{middlePart}{lastPart}";
        return anonymizedLocal;
    }

    private static string AnonymizedDomain(string domain)
    {
        var parts = domain.Split('.');

        var firstPart = parts[0].Length > 2
            ? $"{parts[0][0]}{new string('*', parts[0].Length - 2)}{parts[0][^1]}"
            : parts[0];

        var builder = new StringBuilder(domain.Length);

        builder.Append(firstPart);

        for (var i = 1; i < parts.Length; i++)
        {
            builder.Append(".");
            builder.Append(parts[i]);
        }

        return builder.ToString();
    }
}