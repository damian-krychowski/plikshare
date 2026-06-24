using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using PlikShare.Agents.BoxAccess;
using PlikShare.Agents.Middleware;
using PlikShare.AuditLog;
using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Id;
using PlikShare.Files.Download;
using PlikShare.Files.Id;
using PlikShare.Files.PreSignedLinks.RangeRequests;
using PlikShare.Mcp.BoxAccess.ReadFile.Contracts;
using PlikShare.Mcp.Files.Read;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;
using Audit = PlikShare.AuditLog.Details.Audit;

namespace PlikShare.Mcp.BoxAccess.ReadFile;

/// <summary>
/// The reusable core of read_box_file: re-validates the agent's box access, resolves the file inside the
/// box (scoped to its subtree), reads the requested byte range and decodes it as UTF-8 text (trimming
/// partial multibyte sequences at page boundaries), writing the audit entry. Called directly by the tool
/// when no approval is required, and by the execute flow once a human has approved the operation. The read
/// is idempotent, so the execute flow simply re-reads.
/// </summary>
public class ReadBoxFileAgentOperation(
    BoxCache boxCache,
    AgentBoxAccessCache boxAccessCache,
    GetFileDetailsQuery getFileDetailsQuery,
    AuditLogService auditLogService)
{
    private const int DefaultMaxBytes = 64 * 1024;
    private const int MinMaxBytes = 1024;
    private const int HardMaxBytes = 256 * 1024;

    public async Task<ReadBoxFileResponseDto> Execute(
        HttpContext httpContext,
        ReadBoxFileParams parameters,
        CancellationToken cancellationToken)
    {
        var agent = await httpContext.GetAgentContext();

        var boxAccess = await httpContext.GetAgentBoxAccess(
            agent,
            boxCache,
            boxAccessCache,
            BoxExtId.Parse(parameters.BoxExternalId),
            cancellationToken);

        if (boxAccess.IsOff)
            throw new McpException(
                $"Box '{parameters.BoxExternalId}' is disabled or exposes no folder, so its files cannot be read.");

        var workspace = boxAccess.Box.Workspace;
        var fileExternalId = FileExtId.Parse(parameters.FileExternalId);

        var details = getFileDetailsQuery.Execute(
            workspaceId: workspace.Id,
            fileExternalId: fileExternalId,
            boxFolderId: boxAccess.Box.Folder!.Id,
            workspaceEncryptionSession: null);

        if (details.IsEmpty)
            throw new McpException(
                $"File '{parameters.FileExternalId}' was not found inside box '{parameters.BoxExternalId}'.");

        var file = details.Value;

        if (!AgentTextFileReader.IsLikelyText(file.ContentType, file.Extension))
            throw new McpException(
                $"File '{file.Name}' (content type '{file.ContentType}', extension '{file.Extension}') is not a " +
                $"text file. read_box_file returns UTF-8 text only.");

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
            await LogContentRead(httpContext, workspace.ExternalId.Value, parameters.FileExternalId, offset, 0, cancellationToken);

            return new ReadBoxFileResponseDto
            {
                Content = string.Empty,
                TotalSizeInBytes = totalSize,
                NextOffset = totalSize,
                HasMore = false
            };
        }

        var range = FileBytesRange.Create(
            start: offset,
            end: offset + effectiveMaxBytes - 1,
            fileSizeInBytes: totalSize);

        var encryptionMode = workspace.GetFileEncryptionMode(
            fileEncryptionMetadata: file.EncryptionMetadata,
            workspaceEncryptionSession: null);

        var fileKey = new FileKey
        {
            FileExternalId = file.ExternalId,
            KeySecretPart = file.KeySecretPart
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

        await LogContentRead(httpContext, workspace.ExternalId.Value, parameters.FileExternalId, offset, end, cancellationToken);

        return new ReadBoxFileResponseDto
        {
            Content = content,
            TotalSizeInBytes = totalSize,
            NextOffset = nextOffset,
            HasMore = hasMore
        };
    }

    private ValueTask LogContentRead(
        HttpContext httpContext,
        string workspaceExternalId,
        string fileExternalId,
        long offset,
        long bytesReturned,
        CancellationToken cancellationToken)
    {
        return auditLogService.Log(
            Audit.Agent.FileContentReadEntry(
                actor: httpContext.GetAuditLogActorContext(),
                workspaceExternalId: workspaceExternalId,
                fileExternalId: fileExternalId,
                offset: offset,
                bytesReturned: bytesReturned),
            cancellationToken);
    }
}
