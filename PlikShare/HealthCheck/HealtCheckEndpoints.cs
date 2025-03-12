namespace PlikShare.HealthCheck;

public record HealthCheckResponse(string Message);

public static class HealthCheckEndpoints
{
    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/health-check")
            .WithTags("Health Check")
            .AllowAnonymous();

        group.MapGet("/", () => new HealthCheckResponse("App is running"))
            .WithName("GetHealthCheck");
    }
}