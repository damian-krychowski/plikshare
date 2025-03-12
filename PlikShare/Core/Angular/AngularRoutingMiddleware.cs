namespace PlikShare.Core.Angular;

public class AngularRoutingMiddleware(RequestDelegate next)
{
    private readonly string[] _fileExtensions = [
        "js", "css", "png", "jpg", "jpeg", "svg", "woff", "woff2", "ico", "map", "json", "webp", "webm", "txt", "xml", "pdf"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path.Value;

        // Only intervene for specific paths
        if (requestPath != null && !requestPath.StartsWith("/api/") && !IsRequestingFile(requestPath))
        {
            context.Request.Path = "/index.html";
        }
        
        await next(context);
    }
    
    private bool IsRequestingFile(string url)
    {
        return _fileExtensions.Any(ext => url.EndsWith("." + ext, StringComparison.OrdinalIgnoreCase));
    }
}