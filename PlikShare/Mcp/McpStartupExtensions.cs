using PlikShare.Antiforgery;
using PlikShare.Core.Authorization;
using PlikShare.Mcp.Folders.Create;
using PlikShare.Mcp.Folders.Rename;
using PlikShare.Mcp.Workspaces.List;

namespace PlikShare.Mcp;

public static class McpStartupExtensions
{
    public static void AddPlikShareMcp(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();

        builder
            .Services
            .AddMcpServer()
            .WithHttpTransport()
            .WithTools<ListWorkspacesTool>()
            .WithTools<CreateFolderTool>()
            .WithTools<RenameFolderTool>();
    }

    public static void MapPlikShareMcp(this WebApplication app)
    {
        app.MapMcp("/mcp")
            .RequireAuthorization(AuthPolicy.AgentToken)
            .WithMetadata(new DisableAutoAntiforgeryCheck());
    }
}
