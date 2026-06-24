using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Mcp.BoxAccess;

/// <summary>
/// Resolves a box's current name from its external id, so box-access approval details can show the
/// human-readable box (and a clickable link to it) instead of a raw id — mirroring how workspace
/// approval details surface real names. Returns null when the box no longer exists.
/// </summary>
public class BoxApprovalNameResolver(PlikShareDb plikShareDb)
{
    public string? GetBoxName(string boxExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT bo_name
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", boxExternalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
}
