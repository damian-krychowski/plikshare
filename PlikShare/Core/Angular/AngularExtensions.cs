namespace PlikShare.Core.Angular;

public static class AngularExtensions
{
    private static readonly string[] CacheableExtensions = [
        ".png", ".jpg", ".jpeg", ".svg", ".woff", ".woff2", ".ico", ".webp", ".webm", ".pdf"
    ];

    private static readonly string[] CacheablePrefixes =
    [
        "chunk-", "styles-", "main-", "polyfills-", "scripts-"
    ];
    
    public static bool ShouldFileBeCached(string fileName)
    {
        if (CacheableExtensions.Any(fileName.EndsWith))
        {
            return true;
        }

        if (CacheablePrefixes.Any(fileName.StartsWith))
        {
            return true;
        }

        return false;
    }
}