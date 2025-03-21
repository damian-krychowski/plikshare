using Microsoft.AspNetCore.Antiforgery;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Antiforgery;

public class AutoAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsMethodExcluded(context) || IsEndpointExcluded(context))
        {
            await next(context);
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context);
            await next(context);
        }
        catch (AntiforgeryValidationException ex)
        {
            Log.Warning(ex, "Anti-forgery token validation failed. Url {RequestUrl}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var error = HttpErrors.Antiforgery.InvalidAntiforgeryToken();
            var problem = Json.Serialize(error);

            await context.Response.WriteAsync(problem);
        }
    }

    private static bool IsMethodExcluded(HttpContext context)
    {
        var method = context.Request.Method;

        return method == HttpMethods.Get || 
               method == HttpMethods.Head ||
               method == HttpMethods.Options || 
               method == HttpMethods.Trace;
    }

    private static bool IsEndpointExcluded(HttpContext context)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint is null)
            return false;

        return endpoint
            .Metadata
            .FirstOrDefault(m => m is DisableAutoAntiforgeryCheck) != null;
    }
}

public class DisableAutoAntiforgeryCheck;