namespace PlikShare.Core.CorrelationId;

public static class HttpContextCorrelationIdExtensions
{
    public static Guid GetCorrelationId(this HttpContext context)
    {
        if (Guid.TryParse(context.TraceIdentifier, out var correlationId))
        {
            return correlationId;
        }
        
        throw new InvalidOperationException("CorrelationId is missing in the httpContext");
    }
}