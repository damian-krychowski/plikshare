using System.Text;

namespace PlikShare.Mcp.Search;

public enum SearchPhase
{
    Folder,
    File
}

public sealed record SearchCursor(
    SearchPhase Phase,
    int LastId)
{
    public string Encode()
    {
        var prefix = Phase == SearchPhase.Folder ? "fo" : "fi";
        var raw = $"{prefix}:{LastId}";

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(raw));
    }

    public static SearchCursor? TryDecode(string cursor)
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
                "fo" => new SearchCursor(SearchPhase.Folder, lastId),
                "fi" => new SearchCursor(SearchPhase.File, lastId),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }
}
