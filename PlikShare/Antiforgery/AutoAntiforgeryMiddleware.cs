using Microsoft.AspNetCore.Antiforgery;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Antiforgery;

public class AutoAntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Skip validation for non-state-changing methods
        var method = context.Request.Method;
        if (method == HttpMethods.Get || method == HttpMethods.Head || method == HttpMethods.Options || method == HttpMethods.Trace)
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
}