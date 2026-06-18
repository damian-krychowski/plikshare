using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Core.CorrelationId;
using PlikShare.Mcp.Workspaces.Create.Contracts;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Workspaces.Create;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Workspaces.Create;

/// <summary>
/// The reusable core of create_workspace: re-validates the agent's permission and storage access and
/// creates the workspace, writing the audit entry. Called directly by the tool when no approval is
/// required, and by the execute flow once a human has approved the operation.
/// </summary>
public class CreateWorkspaceAgentOperation(
    StorageClientStore storageClientStore,
    CreateWorkspaceQuery createWorkspaceQuery,
    AuditLogService auditLogService)
{
    public async Task<CreateWorkspaceResponseDto> Execute(
        HttpContext httpContext,
        CreateWorkspaceParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        if (!agent.HasAdminRole && !agent.Permissions.CanAddWorkspace)
            throw new McpException(
                "This agent does not have permission to create workspaces.");

        var hasStorage = storageClientStore.TryGetClient(
            externalId: StorageExtId.Parse(parameters.StorageExternalId),
            client: out var storage);

        if (!hasStorage)
            throw new McpException(
                $"Storage '{parameters.StorageExternalId}' was not found.");

        if (!agent.CanAccessStorage(storage.StorageId))
            throw new McpException(
                $"This agent does not have access to storage '{parameters.StorageExternalId}'.");

        if (storage.EncryptionType == StorageEncryptionType.Full)
            throw new McpException(
                $"Storage '{parameters.StorageExternalId}' uses full client-side encryption, which agents cannot use. " +
                "Pick a storage without full encryption.");

        var result = await createWorkspaceQuery.Execute(
            storage: storage,
            agent: agent,
            name: parameters.Name,
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
                            Name = parameters.Name
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
                    $"Storage '{parameters.StorageExternalId}' was not found.");

            default:
                throw new McpException(
                    $"Unexpected result while creating workspace: {result.Code}.");
        }
    }
}
