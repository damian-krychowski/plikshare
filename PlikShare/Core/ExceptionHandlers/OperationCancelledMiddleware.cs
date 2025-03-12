namespace PlikShare.Core.ExceptionHandlers;

public class OperationCanceledMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Only set status code if response hasn't started
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
        }
    }
}

public static class OperationCanceledMiddlewareExtensions
{
    public static IApplicationBuilder UseOperationCanceledHandler(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<OperationCanceledMiddleware>();
    }
}
