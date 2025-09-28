using System.Text.Json;
using Flurl.Http;
using Flurl.Http.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails.Templates;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Core.Volumes;
using PlikShare.GeneralSettings;
using PlikShare.IntegrationTests.Infrastructure.Mocks;
using PlikShare.Users.Invite;

namespace PlikShare.IntegrationTests.Infrastructure;

public class HostFixture8081 : HostFixture
{
    public override int PortNumber => 8081;
}

public class HostFixture8082 : HostFixture
{
    public override int PortNumber => 8082;
}

public abstract class HostFixture: IAsyncDisposable, IDisposable
{
    private bool _disposed;
    public abstract int PortNumber { get; }
    
    public WebApplication App { get; }

    public IFlurlClient FlurlClient { get; }= PrepareFlurlClient();
    public string AppUrl { get; }
    
    public string MainVolumePathSuffix { get; }

    public ClockMock Clock { get; } = new();
    public OneTimeCodeMock OneTimeCode { get; } = new();
    public OneTimeInvitationCodeMock OneTimeInvitationCode { get; } = new();

    public ResendEmailServer ResendEmailServer { get; }
    
    public EmailTemplates EmailTemplates { get; }
    
    public PlikShareDb Db { get; }
    public AppSettings AppSettings { get; }

    protected HostFixture()
    {
        var resendPort = PortNumber + 1000;
        
        ResendEmailServer = new ResendEmailServer(
            portNumber: resendPort);
        
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions() 
            {
                WebRootPath = "wwwroot/browser",
                EnvironmentName = "IntegrationTests",
            });

        MainVolumePathSuffix = Guid.NewGuid().ToBase62();
        builder.Configuration["Volumes:Path"] = $"{builder.Configuration["Volumes:Path"]}_{MainVolumePathSuffix}";

        AppUrl = $"https://localhost:{PortNumber}";
        builder.Configuration["AppUrl"] = AppUrl;

        builder.Configuration["Resend:Endpoint"] = $"https://localhost:{resendPort}/emails";

        builder.WebHost.UseUrls(AppUrl);
        
        Startup.SetupWebAppBuilder(builder);

        builder.Services.AddSingleton<IClock>(Clock);
        builder.Services.AddSingleton<IOneTimeCode>(OneTimeCode);
        builder.Services.AddSingleton<IOneTimeInvitationCode>(OneTimeInvitationCode);

        App = builder.Build();

        Startup.InitializeWebApp(App);
        
        App.Start();

        EmailTemplates = new EmailTemplates(
            generic: App.Services.GetRequiredService<GenericEmailTemplate>());

        Db = App.Services.GetRequiredService<PlikShareDb>();
        AppSettings = App.Services.GetRequiredService<AppSettings>();
    }

    private static IFlurlClient PrepareFlurlClient()
    {
        var client = new FlurlClient();
        client.Settings.JsonSerializer = PrepareFlurlJsonSerializer();

        return client;
    }

    private static DefaultJsonSerializer PrepareFlurlJsonSerializer()
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        jsonSerializerOptions.AddConverters();

        return new DefaultJsonSerializer(jsonSerializerOptions);
    }

    private void RemoveVolumesFolder(string volumesPath)
    {
        var location = Location.Create(
            path: volumesPath);

        if (Directory.Exists(location.FullPath))
        {
            Directory.Delete(
                location.FullPath,
                recursive: true);
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        try
        {
            await ResendEmailServer.DisposeAsync();
            await App.StopAsync();
            await App.DisposeAsync();
            FlurlClient.Dispose();
            // RemoveVolumesFolder(App.Configuration["Volumes:Path"]);
        }
        finally
        {
            _disposed = true;
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    public void RemoveAllEmailProviders()
    {
        using var connection = Db.OpenConnection();

        var result = connection.Cmd(
                sql: @"
                DELETE FROM ep_email_providers
                RETURNING ep_id
            ",
                readRowFunc: reader => reader.GetInt32(0))
            .Execute();
        
        Console.WriteLine($"Deleted email providers count: {result.Count}");
    }
}

public class EmailTemplates(GenericEmailTemplate generic)
{
    public GenericEmailTemplate Generic { get; } = generic;
}