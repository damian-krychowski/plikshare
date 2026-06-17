using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Middleware;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Workspaces.Create.Contracts;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.Create;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Create;

[McpServerToolType]
public class CreateWorkspaceTool
{
    [McpServerTool(Name = AgentToolNames.CreateWorkspace)]
    [Description("Creates a new workspace owned by this agent, on the given storage. The agent must have " +
                 "the 'add workspace' permission and access to the storage. Use list_storages to discover " +
                 "the storages the agent can use. Storages with full client-side encryption are not " +
                 "supported. Returns the new workspace's external id.")]
    public static async Task<CreateWorkspaceResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        StorageClientStore storageClientStore,
        CreateWorkspaceQuery createWorkspaceQuery,
        AuditLogService auditLogService,
        [Description("Name for the new workspace.")]
        string name,
        [Description("External id of the storage to create the workspace on. Use list_storages to find it.")]
        string storageExternalId,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var agent = await httpContext.GetAgentContext();

        if (!agent.HasAdminRole && !agent.Permissions.CanAddWorkspace)
            throw new McpException(
                "This agent does not have permission to create workspaces.");

        var hasStorage = storageClientStore.TryGetClient(
            externalId: StorageExtId.Parse(storageExternalId),
            client: out var storage);

        if (!hasStorage)
            throw new McpException(
                $"Storage '{storageExternalId}' was not found.");

        if (!agent.CanAccessStorage(storage.StorageId))
            throw new McpException(
                $"This agent does not have access to storage '{storageExternalId}'.");

        if (storage.EncryptionType == StorageEncryptionType.Full)
            throw new McpException(
                $"Storage '{storageExternalId}' uses full client-side encryption, which agents cannot use. " +
                "Pick a storage without full encryption.");

        var result = await createWorkspaceQuery.Execute(
            storage: storage,
            agent: agent,
            name: name,
            correlationId: httpContext.GetCorrelationId(),
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateWorkspaceQuery.ResultCode.Ok:
                await auditLogService.LogWithStorageContext(
                    storageExternalId: storage.ExternalId,
                    buildEntry: storageRef => Audit.Workspace.CreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        storage: storageRef,
                        workspace: new Audit.WorkspaceRef
                        {
                            ExternalId = result.Workspace.ExternalId,
                            Name = name
                        },
                        maxSizeInBytes: result.Workspace.MaxSizeInBytes,
                        bucketName: result.Workspace.BucketName),
                    cancellationToken);

                return new CreateWorkspaceResponseDto
                {
                    WorkspaceExternalId = result.Workspace.ExternalId.Value
                };

            case CreateWorkspaceQuery.ResultCode.MaxNumberOfWorkspacesReached:
                throw new McpException(
                    "This agent has reached its maximum number of workspaces.");

            case CreateWorkspaceQuery.ResultCode.StorageNotFound:
                throw new McpException(
                    $"Storage '{storageExternalId}' was not found.");

            default:
                throw new McpException(
                    $"Unexpected result while creating workspace: {result.Code}.");
        }
    }
}
