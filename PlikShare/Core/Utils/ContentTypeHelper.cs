namespace PlikShare.Core.Utils;

public enum FileType
{
    Image,
    Video,
    Other,
    Pdf,
    Audio,
    Text,
    Archive,
    Markdown
}

public static class ContentTypeHelper
{
    public const string Json = ".json";
    public const string Markdown = ".md";

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".webm", ".ogg", ".m4v", ".mkv"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".m4a", ".aac"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".csv", Json, ".xml", ".html", ".js", ".css", ".py", ".java", ".cs",
        ".ts", ".scss", ".yml", ".sh", ".sln", ".ps1",
        ".rb", ".go", ".php", ".sql", ".r", ".c", ".cpp", ".h", ".swift", ".kt", ".rs", ".lua", ".pl",
        ".ini", ".env", ".toml", ".conf", ".properties", ".yaml", ".lock", ".gitignore", ".editorconfig",
        ".graphql", ".proto", ".rst", ".tex", ".adoc"
    };
    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        // Images
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" },
        { ".gif", "image/gif" },
        { ".bmp", "image/bmp" },
        { ".webp", "image/webp" },
        { ".svg", "image/svg+xml" },
        // Video
        { ".mp4", "video/mp4" },
        { ".mov", "video/quicktime" },
        { ".webm", "video/webm" },
        { ".ogg", "video/ogg" },
        { ".m4v", "video/x-m4v" },
        { ".mkv", "video/x-matroska" },
        // PDF
        { ".pdf", "application/pdf" },
        // Archive
        { ".zip", "application/zip" },
        // Audio
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".m4a", "audio/m4a" },
        { ".aac", "audio/aac" },
        // Text
        { ".txt", "text/plain" },
        { ".csv", "text/csv" },
        { Json, "application/json" },
        { ".xml", "application/xml" },
        { Markdown, "text/markdown" },
        { ".html", "text/html" },
        { ".js", "application/javascript" },
        { ".css", "text/css" },
        { ".py", "text/x-python" },
        { ".java", "text/x-java-source" },
        { ".cs", "text/x-csharp" },
        { ".ts", "application/typescript" },
        { ".scss", "text/x-scss" },
        { ".yml", "text/yaml" },
        { ".yaml", "text/yaml" },
        { ".sh", "text/x-sh" },
        { ".sln", "text/plain" },
        { ".ps1", "text/plain" },
        { ".rb", "text/x-ruby" },
        { ".go", "text/x-go" },
        { ".php", "application/x-httpd-php" },
        { ".sql", "application/sql" },
        { ".r", "text/x-r" },
        { ".c", "text/x-c" },
        { ".cpp", "text/x-c++src" },
        { ".h", "text/x-c" },
        { ".swift", "text/x-swift" },
        { ".kt", "text/x-kotlin" },
        { ".rs", "text/x-rust" },
        { ".lua", "text/x-lua" },
        { ".pl", "text/x-perl" },
        { ".ini", "text/plain" },
        { ".env", "text/plain" },
        { ".toml", "application/toml" },
        { ".conf", "text/plain" },
        { ".properties", "text/plain" },
        { ".lock", "text/plain" },
        { ".gitignore", "text/plain" },
        { ".editorconfig", "text/plain" },
        { ".graphql", "application/graphql" },
        { ".proto", "text/plain" },
        { ".rst", "text/x-rst" },
        { ".tex", "application/x-tex" },
        { ".adoc", "text/asciidoc" }
    };

    // Maps file extensions to language identifiers for markdown code blocks
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "csharp" },
        { ".vb", "vb" },
        { ".js", "javascript" },
        { ".ts", "typescript" },
        { ".py", "python" },
        { ".java", "java" },
        { ".html", "html" },
        { ".css", "css" },
        { ".scss", "scss" },
        { ".xml", "xml" },
        { ".json", "json" },
        { ".md", "markdown" },
        { ".sql", "sql" },
        { ".sh", "bash" },
        { ".ps1", "powershell" },
        { ".yml", "yaml" },
        { ".yaml", "yaml" },
        { ".rb", "ruby" },
        { ".go", "go" },
        { ".php", "php" },
        { ".r", "r" },
        { ".c", "c" },
        { ".cpp", "cpp" },
        { ".h", "c" },
        { ".swift", "swift" },
        { ".kt", "kotlin" },
        { ".rs", "rust" },
        { ".lua", "lua" },
        { ".pl", "perl" },
        { ".fs", "fsharp" },
        { ".ini", "ini" },
        { ".toml", "toml" },
        { ".graphql", "graphql" },
        { ".proto", "protobuf" },
        { ".tex", "latex" },
        { ".adoc", "asciidoc" },
        { ".rst", "rst" },
        { ".diff", "diff" },
        { ".dockerfile", "dockerfile" },
        { ".gitignore", "gitignore" },
        { ".txt", "text" },
        { ".csv", "csv" }
    };

    public static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return MimeTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
    public static string GetContentTypeFromExtension(string fileExtension)
    {
        var extension = fileExtension.ToLowerInvariant();

        return MimeTypes.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
    
    public static FileType GetFileTypeFromExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return FileType.Other;

        extension = extension.ToLowerInvariant();

        if (extension == Markdown)
            return FileType.Markdown;

        if (extension == ".pdf")
            return FileType.Pdf;

        if (extension == ".zip")
            return FileType.Archive;

        if (ImageExtensions.Contains(extension))
            return FileType.Image;

        if (VideoExtensions.Contains(extension))
            return FileType.Video;

        if (AudioExtensions.Contains(extension))
            return FileType.Audio;

        if (TextExtensions.Contains(extension))
            return FileType.Text;

        return FileType.Other;
    }

    /// <summary>
    /// Gets the markdown language identifier for a given file extension.
    /// Used for formatting code blocks with language and filename in markdown.
    /// </summary>
    /// <param name="extension">The file extension including the dot (e.g., ".cs")</param>
    /// <returns>The language identifier for markdown code blocks (e.g., "csharp")</returns>
    public static string GetMarkdownLanguageIdentifier(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return "text";

        extension = extension.ToLowerInvariant();

        // Handle special cases for files without extensions
        if (extension.EndsWith("dockerfile", StringComparison.OrdinalIgnoreCase))
            return "dockerfile";

        if (extension.EndsWith(".gitignore", StringComparison.OrdinalIgnoreCase))
            return "gitignore";

        // Check if we have a mapping for this extension
        if (LanguageMap.TryGetValue(extension, out var language))
            return language;

        // Default to text if we don't know the language
        return TextExtensions.Contains(extension) ? "text" : "text";
    }
}