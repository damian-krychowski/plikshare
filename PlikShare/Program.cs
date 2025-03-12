using PlikShare;

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions() 
    {
        WebRootPath = "wwwroot/browser"
    });

builder
    .Configuration
    .AddEnvironmentVariables(prefix: "PlikShare_");

Startup.SetupWebAppBuilder(builder);

var app = builder.Build();

Startup.InitializeWebApp(app: app);

app.Run();
