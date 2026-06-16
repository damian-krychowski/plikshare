using PlikShare.Antiforgery;
using PlikShare.Core.Authorization;
using PlikShare.Mcp.BulkDelete;
using PlikShare.Mcp.Files.BulkDownloadLink;
using PlikShare.Mcp.Files.Create;
using PlikShare.Mcp.Files.DownloadLink;
using PlikShare.Mcp.Files.Get;
using PlikShare.Mcp.Files.Read;
using PlikShare.Mcp.Files.Rename;
using PlikShare.Mcp.Folders.Create;
using PlikShare.Mcp.Folders.Rename;
using PlikShare.Mcp.Search;
using PlikShare.Mcp.ShareLinks.Create;
using PlikShare.Mcp.ShareLinks.Delete;
using PlikShare.Mcp.ShareLinks.Get;
using PlikShare.Mcp.ShareLinks.List;
using PlikShare.Mcp.ShareLinks.Update;
using PlikShare.Mcp.Workspaces.Content;
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
            .WithTools<ListWorkspaceContentTool>()
            .WithTools<SearchTool>()
            .WithTools<GetFileTool>()
            .WithTools<ReadFileTool>()
            .WithTools<GetFileDownloadLinkTool>()
            .WithTools<GetBulkDownloadLinkTool>()
            .WithTools<CreateFileTool>()
            .WithTools<RenameFileTool>()
            .WithTools<CreateFolderTool>()
            .WithTools<RenameFolderTool>()
            .WithTools<BulkDeleteTool>()
            .WithTools<CreateShareLinkTool>()
            .WithTools<ListShareLinksTool>()
            .WithTools<GetShareLinkTool>()
            .WithTools<UpdateShareLinkTool>()
            .WithTools<DeleteShareLinkTool>();
    }

    public static void MapPlikShareMcp(this WebApplication app)
    {
        app.MapMcp("/mcp")
            .RequireAuthorization(AuthPolicy.AgentToken)
            .WithMetadata(new DisableAutoAntiforgeryCheck());
    }
}
