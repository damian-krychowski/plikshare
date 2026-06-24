using System.Text;

namespace PlikShare.Mcp.Files.Read;

/// <summary>
/// Shared text-reading helpers behind read_file and read_box_file: decides whether a file is text from its
/// content type / extension, and decodes a raw byte range as UTF-8 while trimming partial multibyte
/// sequences at page boundaries so paged reads never split a character.
/// </summary>
public static class AgentTextFileReader
{
    public static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static (int Start, int End) ComputeUtf8Boundaries(
        byte[] bytes,
        bool atFileStart,
        bool isEndOfFile)
    {
        var start = 0;

        if (!atFileStart)
        {
            while (start < bytes.Length && (bytes[start] & 0xC0) == 0x80)
                start++;
        }

        var end = bytes.Length;

        if (!isEndOfFile)
            end = TrimIncompleteTrailingSequence(bytes, start, bytes.Length);

        return (start, end);
    }

    private static int TrimIncompleteTrailingSequence(byte[] bytes, int start, int end)
    {
        var i = end - 1;

        while (i >= start && (bytes[i] & 0xC0) == 0x80)
            i--;

        if (i < start)
            return end;

        var lead = bytes[i];

        var sequenceLength =
            lead < 0x80 ? 1 :
            (lead & 0xE0) == 0xC0 ? 2 :
            (lead & 0xF0) == 0xE0 ? 3 :
            (lead & 0xF8) == 0xF0 ? 4 :
            1;

        var available = end - i;

        return available >= sequenceLength
            ? end
            : i;
    }

    private static readonly HashSet<string> TextApplicationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json", "application/ld+json", "application/xml", "application/xhtml+xml",
        "application/javascript", "application/ecmascript", "application/x-javascript",
        "application/yaml", "application/x-yaml", "application/x-ndjson",
        "application/csv", "application/sql", "application/graphql", "application/toml",
        "image/svg+xml"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "text", "md", "markdown", "rst", "adoc", "tex",
        "json", "jsonl", "ndjson", "xml", "html", "htm", "csv", "tsv",
        "yaml", "yml", "toml", "ini", "cfg", "conf", "env", "properties", "log",
        "js", "mjs", "cjs", "ts", "tsx", "jsx", "css", "scss", "less", "vue", "svelte",
        "py", "rb", "go", "rs", "java", "kt", "kts", "c", "h", "cpp", "hpp", "cc", "cs",
        "php", "sh", "bash", "zsh", "ps1", "sql", "graphql", "gql",
        "r", "lua", "pl", "swift", "dart", "scala", "clj", "ex", "exs"
    };

    public static bool IsLikelyText(string contentType, string extension)
    {
        var contentTypeValue = (contentType ?? string.Empty).Trim();

        if (contentTypeValue.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        var semicolon = contentTypeValue.IndexOf(';');

        var bareContentType = semicolon >= 0
            ? contentTypeValue[..semicolon].Trim()
            : contentTypeValue;

        if (TextApplicationTypes.Contains(bareContentType))
            return true;

        var bareExtension = (extension ?? string.Empty).TrimStart('.').Trim();

        return TextExtensions.Contains(bareExtension);
    }
}
