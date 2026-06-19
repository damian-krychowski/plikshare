using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.BoxLinks.Update.Contracts;

namespace PlikShare.Mcp.BoxLinks.Update;

/// <summary>
/// Resolves an update_box_link operation's stored parameters into the box link's current name plus which
/// dimensions would change, so a human reviewing the approval sees exactly what would change.
/// </summary>
public class UpdateBoxLinkOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public UpdateBoxLinkOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<UpdateBoxLinkParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        using var connection = plikShareDb.OpenConnection();

        var currentName = connection
            .OneRowCmd(
                sql: "SELECT bl_name FROM bl_box_links WHERE bl_external_id = $externalId LIMIT 1",
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.BoxLinkExternalId)
            .Execute();

        return new UpdateBoxLinkOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxLinkExternalId = parameters.BoxLinkExternalId,
            CurrentName = currentName.IsEmpty ? null : currentName.Value,
            UpdateName = parameters.Name is not null,
            NewName = parameters.Name,
            UpdateIsEnabled = parameters.IsEnabled is not null,
            IsEnabled = parameters.IsEnabled,
            UpdatePermissions = parameters.HasPermissionChange,
            UpdateWidgetOrigins = parameters.WidgetOrigins is not null
        };
    }
}
