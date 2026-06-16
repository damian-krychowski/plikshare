using System.Text;

namespace PlikShare.Mcp.Workspaces.Content;

public enum WorkspaceContentPhase
{
    Folder,
    File
}

public enum WorkspaceContentTypeFilter
{
    All,
    Folder,
    File
}

public sealed record WorkspaceContentCursor(
    WorkspaceContentPhase Phase,
    int LastId)
{
    public string Encode()
    {
        var prefix = Phase == WorkspaceContentPhase.Folder ? "fo" : "fi";
        var raw = $"{prefix}:{LastId}";

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(raw));
    }

    public static WorkspaceContentCursor? TryDecode(string cursor)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(
                Convert.FromBase64String(cursor));

            var parts = raw.Split(':', 2);

            if (parts.Length != 2 || !int.TryParse(parts[1], out var lastId) || lastId < 0)
                return null;

            return parts[0] switch
            {
                "fo" => new WorkspaceContentCursor(WorkspaceContentPhase.Folder, lastId),
                "fi" => new WorkspaceContentCursor(WorkspaceContentPhase.File, lastId),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
