using PlikShare.Agents.Operations;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Mcp.Workspaces.Create.Contracts;

namespace PlikShare.Mcp.Workspaces.Create;

/// <summary>
/// Resolves a create_workspace operation's stored parameters into the new workspace's name and the
/// storage it would be created on, so a human reviewing the approval sees what gets created and where.
/// </summary>
public class CreateWorkspaceOperationDetailsResolver(
    PlikShareDb plikShareDb)
{
    public CreateWorkspaceOperationDetails Resolve(AgentOperation operation)
    {
        var parameters = Json.Deserialize<CreateWorkspaceParams>(operation.ParamsJson)
            ?? throw new InvalidOperationException("The stored operation parameters were invalid.");

        return new CreateWorkspaceOperationDetails
        {
            Name = parameters.Name,
            StorageExternalId = parameters.StorageExternalId,
            StorageName = GetStorageName(parameters.StorageExternalId)
        };
    }

    private string? GetStorageName(string storageExternalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: """
                     SELECT s_name
                     FROM s_storages
                     WHERE s_external_id = $externalId
                     LIMIT 1
                     """,
                readRowFunc: reader => reader.GetString(0))
            .WithParameter("$externalId", storageExternalId)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }
}
