using System.Text.RegularExpressions;

namespace PlikShare.Core.Utils;

public static class EnumUtils
{
    /// <summary>
    /// Converts an enum value to a kebab-case string.
    /// Example: UserAccessLevel.ReadOnly -> "read-only"
    /// </summary>
    /// <typeparam name="T">The enum type</typeparam>
    /// <param name="value">The enum value to convert</param>
    /// <returns>A kebab-case string representation of the enum value</returns>
    public static string ToKebabCase<T>(this T value) where T : Enum
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        // Convert enum value to string and handle special cases
        var enumString = value.ToString();

        // Insert hyphens between words (handling both PascalCase and camelCase)
        var pattern = @"(?<!^)(?=[A-Z][a-z])|(?<!^)(?=[A-Z]{2,})";
        var hyphenated = Regex.Replace(enumString, pattern, "-");

        // Convert to lowercase and handle consecutive uppercase letters
        return hyphenated.ToLowerInvariant();
    }

    /// <summary>
    /// Converts a kebab-case string to an enum value.
    /// Example: "read-only" -> UserAccessLevel.ReadOnly
    /// </summary>
    /// <typeparam name="T">The enum type</typeparam>
    /// <param name="kebabCase">The kebab-case string to convert</param>
    /// <returns>The corresponding enum value</returns>
    /// <exception cref="ArgumentException">Thrown when the string cannot be converted to the specified enum type</exception>
    public static T FromKebabCase<T>(string kebabCase) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(kebabCase))
            throw new ArgumentNullException(nameof(kebabCase));

        // Convert kebab-case to PascalCase
        var pascalCase = string.Join("", kebabCase.Split('-').Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant()));

        // Try to parse the string to enum
        if (Enum.TryParse<T>(pascalCase, true, out T result))
            return result;

        throw new ArgumentException($"Cannot convert '{kebabCase}' to enum type {typeof(T).Name}");
    }
}