namespace PlikShare.Core.CorrelationId;

public class HttpCorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeaderName = "x-correlation-id";
            
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public Task Invoke(HttpContext context)
    {
        var correlationId = ApplyAndGetCorrelationId(context);

        // apply the correlation ID to the response header for client side tracking
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(
                CorrelationIdHeaderName, 
                new[]
                {
                    correlationId.ToString()
                });
                    
            return Task.CompletedTask;
        });

        //using (LogContext.PushProperty(LogContextProperty.CorrelationId, correlationId))
        //{
            return _next(context);
        //}
    }

    private static Guid ApplyAndGetCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId))
        {
            if (Guid.TryParse(correlationId, out var guidCorrelationId))
            {
                context.TraceIdentifier = guidCorrelationId.ToString();
                return guidCorrelationId;
            }

            throw new InvalidOperationException($"CorrelationId should be a Guid, but found: '{correlationId}'");
        }

        var newCorrelationId = Guid.NewGuid();
        context.TraceIdentifier = newCorrelationId.ToString();

        return newCorrelationId;
    }
}