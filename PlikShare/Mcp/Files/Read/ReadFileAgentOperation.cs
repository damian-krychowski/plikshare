using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Mcp.Files.Get;
using PlikShare.Mcp.Files.Read.Contracts;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using PlikShare.Workspaces.Id;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.Files.Read;

/// <summary>
/// The reusable core of read_file: resolves the file across the agent's workspaces, re-validates
/// access, reads the requested byte range and decodes it as UTF-8 text (trimming partial multibyte
/// sequences at page boundaries), writing the audit entry. Called directly by the tool when no
/// approval is required, and by the execute flow once a human has approved the operation. The read is
/// idempotent, so the execute flow simply re-reads.
/// </summary>
public class ReadFileAgentOperation(
    WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
    GetFileForAgentQuery getFileForAgentQuery,
    GetFileDetailsQuery getFileDetailsQuery,
    AuditLogService auditLogService)
{
    private const int DefaultMaxBytes = 64 * 1024;
    private const int MinMaxBytes = 1024;
    private const int HardMaxBytes = 256 * 1024;

    public async Task<ReadFileResponseDto> Execute(
        HttpContext httpContext,
        ReadFileParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var file = getFileForAgentQuery.Execute(
            FileExtId.Parse(parameters.FileExternalId));

        if (file is null)
            throw new McpException($"File '{parameters.FileExternalId}' was not found.");

        var membership = await workspaceAgentMembershipCache.TryGetWorkspaceAgentMembership(
            workspaceExternalId: WorkspaceExtId.Parse(file.WorkspaceExternalId),
            agentExternalId: agent.ExternalId,
            cancellationToken: cancellationToken);

        if (membership is null || !membership.IsAvailableForAgent)
            throw new McpException($"File '{parameters.FileExternalId}' was not found.");

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        if (!AgentTextFileReader.IsLikelyText(file.ContentType, file.Extension))
            throw new McpException(
                $"File '{file.Name}' (content type '{file.ContentType}', extension '{file.Extension}') is not a " +
                $"text file. read_file returns UTF-8 text only - use get_file for its metadata.");

        var offset = parameters.Offset;

        if (offset < 0)
            throw new McpException("offset must be zero or greater.");

        var effectiveMaxBytes = Math.Clamp(
            parameters.MaxBytes ?? DefaultMaxBytes,
            MinMaxBytes,
            HardMaxBytes);

        var totalSize = file.SizeInBytes;

        if (offset >= totalSize)
        {
            await LogContentRead(
                httpContext,
                file,
                offset: offset,
                bytesReturned: 0,
                cancellationToken);

            return new ReadFileResponseDto
            {
                Content = string.Empty,
                TotalSizeInBytes = totalSize,
                NextOffset = totalSize,
                HasMore = false
            };
        }

        var details = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: FileExtId.Parse(file.ExternalId),
            boxFolderId: null,
            workspaceEncryptionSession: null);

        if (details.IsEmpty)
            throw new McpException($"File '{parameters.FileExternalId}' was not found.");

        var fileRecord = details.Value;

        var range = FileBytesRange.Create(
            start: offset,
            end: offset + effectiveMaxBytes - 1,
            fileSizeInBytes: totalSize);

        var encryptionMode = workspace.GetFileEncryptionMode(
            fileEncryptionMetadata: fileRecord.EncryptionMetadata,
            workspaceEncryptionSession: null);

        var fileKey = new FileKey
        {
            FileExternalId = fileRecord.ExternalId,
            KeySecretPart = fileRecord.KeySecretPart
        };

        byte[] raw;

        await using (var storageFile = await workspace.DownloadFileRange(
            fileDetails: new DownloadFileRangeDetails(
                Range: range,
                FileKey: fileKey,
                FileSizeInBytes: totalSize,
                EncryptionMode: encryptionMode),
            cancellationToken: cancellationToken))
        {
            using var memory = new MemoryStream();
            var writer = PipeWriter.Create(memory);

            await storageFile.ReadTo(writer, cancellationToken);
            await writer.CompleteAsync();

            raw = memory.ToArray();
        }

        var isEndOfFile = range.End >= totalSize - 1;

        var (start, end) = AgentTextFileReader.ComputeUtf8Boundaries(
            raw,
            atFileStart: offset == 0,
            isEndOfFile: isEndOfFile);

        string content;

        try
        {
            content = AgentTextFileReader.StrictUtf8.GetString(raw, start, end - start);
        }
        catch (DecoderFallbackException)
        {
            throw new McpException(
                $"File '{file.Name}' (extension '{file.Extension}') could not be decoded as UTF-8 text - " +
                $"it appears to be binary.");
        }

        var nextOffset = offset + end;
        var hasMore = nextOffset < totalSize;

        await LogContentRead(
            httpContext,
            file,
            offset: offset,
            bytesReturned: end,
            cancellationToken);

        return new ReadFileResponseDto
        {
            Content = content,
            TotalSizeInBytes = totalSize,
            NextOffset = nextOffset,
            HasMore = hasMore
        };
    }

    private ValueTask LogContentRead(
        HttpContext httpContext,
        GetFileForAgentQuery.Result file,
        long offset,
        long bytesReturned,
        CancellationToken cancellationToken)
    {
        return auditLogService.Log(
            Audit.Agent.FileContentReadEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: file.WorkspaceExternalId,
                fileExternalId: file.ExternalId,
                offset: offset,
                bytesReturned: bytesReturned),
            cancellationToken);
    }

}
