using System.Globalization;
using System.Text;

namespace PlikShare.Core.Utils;

public static class ContentDispositionHelper
{
    public const string Attachment = "attachment";
    public const string Inline = "inline";

    private static readonly HashSet<char> InvalidFileNameChars = [..Path.GetInvalidFileNameChars()];

    public static string CreateContentDisposition(
        string fileName,
        ContentDispositionType disposition = ContentDispositionType.Attachment)
    {
        var sanitizedFileName = SanitizeFileName(fileName);

        // Convert to ASCII for the simple "filename" parameter
        var asciiFileName = ConvertToAscii(sanitizedFileName);

        // For filename*, use proper UTF-8 encoding and percent-encoding
        var encodedFileName = System.Net.WebUtility.UrlEncode(sanitizedFileName)
            .Replace("+", "%20"); // Replace spaces correctly

        // RFC 6266 format with both ASCII and UTF-8 versions
        return $"{ContentDispositionTypeToString(disposition)}; " +
               $"filename=\"{EscapeQuotes(asciiFileName)}\"; " +
               $"filename*=UTF-8''{encodedFileName}";
    }

    private static string ConvertToAscii(string input)
    {
        // Remove diacritics and convert to closest ASCII equivalent
        var normalizedString = input.Normalize(NormalizationForm.FormD);

        var asciiChars = normalizedString
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .Select(c => c < 128 ? c : '-')
            .ToArray();

        return new string(asciiChars);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return "file";

        // Replace invalid chars with dash
        var safeFileName = new string(fileName.Select(c => InvalidFileNameChars.Contains(c) ? '-' : c).ToArray());

        // Remove consecutive dashes
        safeFileName = string.Join("-", safeFileName.Split(['-'], StringSplitOptions.RemoveEmptyEntries));

        // Trim dashes from ends
        safeFileName = safeFileName.Trim('-');

        return safeFileName.Length == 0 ? "file" : safeFileName;
    }

    private static string EscapeQuotes(string fileName)
    {
        return fileName.Replace("\"", "\\\"");
    }

    public static string ContentDispositionTypeToString(ContentDispositionType value)
    {
        return value switch
        {
            ContentDispositionType.Attachment => Attachment,
            ContentDispositionType.Inline => Inline,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
        };
    }

    public static bool TryParse(string value, out ContentDispositionType result)
    {
        switch (value)
        {
            case Attachment:
                result = ContentDispositionType.Attachment;
                return true;

            case Inline:
                result = ContentDispositionType.Inline;
                return true;

            default:
                result = default;
                return false;
        }
    }
}

public enum ContentDispositionType
{
    Attachment = 0,
    Inline
}