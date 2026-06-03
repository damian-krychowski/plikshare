using PlikShare.Boxes.Id;
using PlikShare.Folders.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Boxes.Cache;

public record BoxContext(
    int Id,
    BoxExtId ExternalId,
    string Name,
    bool IsEnabled,
    bool IsBeingDeleted,
    WorkspaceContext Workspace,
    FolderContext? Folder,
    BoxViewMode DefaultViewMode,
    BoxSortMode DefaultSortMode,
    BoxSortDirection DefaultSortDirection,
    bool DefaultThumbnailsEnabled);
    
public record FolderContext(
    int Id,
    FolderExtId ExternalId);