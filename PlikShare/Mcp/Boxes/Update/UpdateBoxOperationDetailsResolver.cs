using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Boxes.Update.Contracts;

namespace PlikShare.Mcp.Boxes.Update;

/// <summary>
/// Resolves an update_box operation's stored parameters into the box's current name plus the requested
/// changes, so a human reviewing the approval sees exactly what would change.
/// </summary>
public class UpdateBoxOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public UpdateBoxOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<UpdateBoxParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        using var connection = plikShareDb.OpenConnection();

        var currentName = connection
            .OneRowCmd(
                sql: """
                     SELECT bo_name
                     FROM bo_boxes
                     WHERE bo_external_id = $externalId
                         AND bo_is_being_deleted = FALSE
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", parameters.BoxExternalId)
            .Execute();

        return new UpdateBoxOperationDetails
        {
            WorkspaceExternalId = parameters.WorkspaceExternalId,
            BoxExternalId = parameters.BoxExternalId,
            CurrentName = currentName.IsEmpty ? null : currentName.Value,
            UpdateName = parameters.Name is not null,
            NewName = parameters.Name,
            UpdateIsEnabled = parameters.IsEnabled is not null,
            IsEnabled = parameters.IsEnabled,
            UpdateFolder = parameters.FolderExternalId is not null,
            FolderExternalId = parameters.FolderExternalId
        };
    }
}
