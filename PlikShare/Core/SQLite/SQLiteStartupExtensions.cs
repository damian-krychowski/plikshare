using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.DataProtection;
using Serilog;

namespace PlikShare.Core.SQLite;

public static class SqLiteStartupExtensions
{
    public static void UseSqLite(this WebApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        
        app.Services.AddSingleton<SQLiteInitialization>();
        app.Services.AddSingleton<PlikShareDb>();
        app.Services.AddSingleton<DbWriteQueue>();

        app.Services.AddSingleton<PlikShareAiDb>();
        app.Services.AddSingleton<AiDbWriteQueue>();
        
        Log.Information("[SETUP] SQLite setup finished.");
    }

    public static void UseSqLiteForDataProtection(this WebApplicationBuilder app)
    {
        app.Services.AddSingleton<IXmlRepository, SQLiteDataProtectionRepository>();
        app.Services.AddSingleton<IXmlEncryptor, PlikShareXmlEncryptor>();
        app.Services.AddSingleton<IXmlDecryptor, PlikShareXmlDecryptor>();

        app.Services
            .AddSingleton<IConfigureOptions<KeyManagementOptions>>(services =>
            {
                return new ConfigureOptions<KeyManagementOptions>(options =>
                {
                    options.XmlRepository = services.GetRequiredService<IXmlRepository>();
                    options.XmlEncryptor = services.GetRequiredService<IXmlEncryptor>();
                });
            });
        
        Log.Information("[SETUP] SQLite setup for DataProtection finished.");
    }

    public static void InitializeSqLite(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Services
            .GetService<SQLiteInitialization>()
            !.Initialize();
        
        Log.Information("[INITIALIZATION] SQLite database initialization finished.");
    }
 }