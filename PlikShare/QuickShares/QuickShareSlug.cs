using System.Text.RegularExpressions;
using PlikShare.Core.Utils;

namespace PlikShare.QuickShares;

public static partial class QuickShareSlug
{
    public const int MinLength = 3;
    public const int MaxLength = 100;

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9\-]{1,98}[a-zA-Z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugRegex();

    public static bool IsValid(string slug)
    {
        if (slug.Length is < MinLength or > MaxLength)
            return false;

        return SlugRegex().IsMatch(slug);
    }

    public static string GenerateAuto() => Guid.NewGuid().ToBase62();
}
