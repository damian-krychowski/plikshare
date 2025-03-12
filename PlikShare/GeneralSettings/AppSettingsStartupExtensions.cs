using Serilog;

namespace PlikShare.GeneralSettings;

public static class AppSettingsStartupExtensions
{   
    public static void UseAppSettings(this WebApplicationBuilder app)
    {
        app.Services.AddSingleton<AppSettings>();
        Log.Information("[SETUP] AppSettings setup finished.");
    }
    
    public static void InitializeAppSettings(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var appSettings = app
            .Services
            .GetRequiredService<AppSettings>();
        
        appSettings.Initialize();
        
        Log.Information("[INITIALIZATION] AppSettings initialization finished.");
    }
}