using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PlikShare.Agents.Tools;
using PlikShare.AuditLog;
using PlikShare.Core.UserIdentity;
using PlikShare.Mcp.Files.Create.Contracts;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Create;

[McpServerToolType]
public class CreateFileTool
{
    [McpServerTool(Name = AgentToolNames.CreateFile)]
    [Description("Creates a new text file in a workspace the agent can access, from content the agent provides " +
                 "inline as UTF-8 text. Use it to save generated text artifacts (notes, reports, configs). " +
                 "The content must be UTF-8 text and at most 10 MB. The content type is derived from the file " +
                 "extension unless you pass contentType explicitly.")]
    public static async Task<CreateFileResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        CreateFileForAgentOperation createFileForAgentOperation,
        AuditLogService auditLogService,
        [Description("External id of the workspace.")]
        string workspaceExternalId,
        [Description("File name including its extension, e.g. \"report.md\".")]
        string name,
        [Description("The file content as UTF-8 text. At most 10 MB.")]
        string content,
        [Description("Optional folder id to create the file in; omit to create it at the workspace root.")]
        string? folderExternalId = null,
        [Description("Optional content type (e.g. text/markdown). If omitted it is derived from the extension.")]
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var membership = await httpContext.GetWorkspaceAgentMembershipDetails(
            workspaceAgentMembershipCache,
            WorkspaceExtId.Parse(workspaceExternalId),
            cancellationToken);

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        var contentBytes = Encoding.UTF8.GetBytes(content ?? string.Empty);

        var result = await createFileForAgentOperation.Execute(
            workspace: workspace,
            uploader: new AgentIdentity(membership.Agent.ExternalId),
            folderExternalId: folderExternalId,
            name: name,
            content: contentBytes,
            contentType: contentType,
            cancellationToken: cancellationToken);

        switch (result.Code)
        {
            case CreateFileForAgentOperation.ResultCode.Ok:
                await auditLogService.Log(
                    Audit.Agent.FileCreatedEntry(
                        actor: httpContext.GetAuditLogActorContext(),
                        workspaceExternalId: workspaceExternalId,
                        fileExternalId: result.FileExternalId!,
                        folderExternalId: folderExternalId,
                        sizeInBytes: contentBytes.Length),
                    cancellationToken);

                return new CreateFileResponseDto
                {
                    FileExternalId = result.FileExternalId!
                };

            case CreateFileForAgentOperation.ResultCode.InvalidName:
                throw new McpException("The file name is invalid. Provide a non-empty file name with an extension.");

            case CreateFileForAgentOperation.ResultCode.ContentTooLarge:
                throw new McpException(
                    $"The content is too large. create_file accepts at most " +
                    $"{CreateFileForAgentOperation.MaximumContentSizeInBytes} bytes.");

            case CreateFileForAgentOperation.ResultCode.FolderNotFound:
                throw new McpException(
                    $"Folder '{folderExternalId}' was not found in workspace '{workspaceExternalId}'.");

            case CreateFileForAgentOperation.ResultCode.NotEnoughSpace:
                throw new McpException(
                    $"The workspace does not have enough free space to store this file.");

            default:
                throw new McpException($"Could not create the file: {result.Code}.");
        }
    }
}
