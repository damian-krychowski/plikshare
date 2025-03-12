namespace PlikShare.HealthCheck;

public static class HealthCheckUtils
{
    public static bool IsHealthCheckEndpoint(HttpContext ctx)
    {
        var path = ctx.Request.Path;

        if (!path.HasValue)
            return false;
        
        return path.Value.Equals("/api/health-check");
    }   
}