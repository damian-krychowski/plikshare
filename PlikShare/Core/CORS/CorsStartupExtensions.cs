using Microsoft.AspNetCore.Cors.Infrastructure;

namespace PlikShare.Core.CORS;

public static class CorsPolicies
{
    public const string PreSignedLink = "PreSignedLinksCorsPolicy";
    public const string BoxLink = "BoxLinkCorsPolicy";

}

public static class CorsStartupExtensions
{
    public static void SetupCors(this WebApplicationBuilder app)
    {
        var appUrl = app.Configuration.GetValue<string>("AppUrl");

        if (string.IsNullOrEmpty(appUrl))
        {
            throw new InvalidOperationException("Config for 'AppUrl' not found.");
        }

        app.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(appUrl)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        app.Services.AddSingleton<ICorsPolicyProvider, DynamicCorsPolicyProvider>();
    }
}