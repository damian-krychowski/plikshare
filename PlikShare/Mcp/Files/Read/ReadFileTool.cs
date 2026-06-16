using System.ComponentModel;
using System.IO.Pipelines;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
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

[McpServerToolType]
public class ReadFileTool
{
    private const int DefaultMaxBytes = 64 * 1024;
    private const int MinMaxBytes = 1024;
    private const int HardMaxBytes = 256 * 1024;

    [McpServerTool(Name = "read_file")]
    [Description("Reads the text content of a file by its external id, decoded as UTF-8. The file is resolved " +
                 "across all workspaces the agent can access; if the agent has no access it is reported as not " +
                 "found. Only text files are returned — binary files (images, video, PDF, archives) are " +
                 "rejected with a clear error. Large files are read in pages: pass the returned nextOffset back " +
                 "as offset while hasMore is true to read the rest.")]
    public static async Task<ReadFileResponseDto> Execute(
        IHttpContextAccessor httpContextAccessor,
        WorkspaceAgentMembershipCache workspaceAgentMembershipCache,
        GetFileForAgentQuery getFileForAgentQuery,
        GetFileDetailsQuery getFileDetailsQuery,
        AuditLogService auditLogService,
        [Description("External id of the file to read.")]
        string fileExternalId,
        [Description("Byte offset to start reading from. Use 0 for the beginning, or the nextOffset from a previous call to continue.")]
        long offset = 0,
        [Description("Maximum number of bytes to read in this call. Default 65536, minimum 1024, maximum 262144.")]
        int? maxBytes = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext!;

        var agent = await httpContext.GetAgentContext();

        var file = getFileForAgentQuery.Execute(
            FileExtId.Parse(fileExternalId));

        if (file is null)
            throw new McpException($"File '{fileExternalId}' was not found.");

        var membership = await workspaceAgentMembershipCache.TryGetWorkspaceAgentMembership(
            workspaceExternalId: WorkspaceExtId.Parse(file.WorkspaceExternalId),
            agentExternalId: agent.ExternalId,
            cancellationToken: cancellationToken);

        if (membership is null || !membership.IsAvailableForAgent)
            throw new McpException($"File '{fileExternalId}' was not found.");

        var workspace = membership.Workspace;
        workspace.ThrowIfFullyEncrypted();

        if (!IsLikelyText(file.ContentType, file.Extension))
            throw new McpException(
                $"File '{file.Name}' (content type '{file.ContentType}', extension '{file.Extension}') is not a " +
                $"text file. read_file returns UTF-8 text only — use get_file for its metadata.");

        if (offset < 0)
            throw new McpException("offset must be zero or greater.");

        var effectiveMaxBytes = Math.Clamp(
            maxBytes ?? DefaultMaxBytes,
            MinMaxBytes,
            HardMaxBytes);

        var totalSize = file.SizeInBytes;

        if (offset >= totalSize)
        {
            await LogContentRead(
                auditLogService, 
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
            throw new McpException($"File '{fileExternalId}' was not found.");

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

        var (start, end) = ComputeUtf8Boundaries(
            raw,
            atFileStart: offset == 0,
            isEndOfFile: isEndOfFile);

        string content;

        try
        {
            content = StrictUtf8.GetString(raw, start, end - start);
        }
        catch (DecoderFallbackException)
        {
            throw new McpException(
                $"File '{file.Name}' (extension '{file.Extension}') could not be decoded as UTF-8 text — " +
                $"it appears to be binary.");
        }

        var nextOffset = offset + end;
        var hasMore = nextOffset < totalSize;

        await LogContentRead(
            auditLogService, 
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

    private static ValueTask LogContentRead(
        AuditLogService auditLogService,
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

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static (int Start, int End) ComputeUtf8Boundaries(
        byte[] bytes,
        bool atFileStart,
        bool isEndOfFile)
    {
        var start = 0;

        if (!atFileStart)
        {
            while (start < bytes.Length && (bytes[start] & 0xC0) == 0x80)
                start++;
        }

        var end = bytes.Length;

        if (!isEndOfFile)
            end = TrimIncompleteTrailingSequence(bytes, start, bytes.Length);

        return (start, end);
    }

    private static int TrimIncompleteTrailingSequence(byte[] bytes, int start, int end)
    {
        var i = end - 1;

        while (i >= start && (bytes[i] & 0xC0) == 0x80)
            i--;

        if (i < start)
            return end;

        var lead = bytes[i];

        var sequenceLength =
            lead < 0x80 ? 1 :
            (lead & 0xE0) == 0xC0 ? 2 :
            (lead & 0xF0) == 0xE0 ? 3 :
            (lead & 0xF8) == 0xF0 ? 4 :
            1;

        var available = end - i;

        return available >= sequenceLength
            ? end
            : i;
    }

    private static readonly HashSet<string> TextApplicationTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json", "application/ld+json", "application/xml", "application/xhtml+xml",
        "application/javascript", "application/ecmascript", "application/x-javascript",
        "application/yaml", "application/x-yaml", "application/x-ndjson",
        "application/csv", "application/sql", "application/graphql", "application/toml",
        "image/svg+xml"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "txt", "text", "md", "markdown", "rst", "adoc", "tex",
        "json", "jsonl", "ndjson", "xml", "html", "htm", "csv", "tsv",
        "yaml", "yml", "toml", "ini", "cfg", "conf", "env", "properties", "log",
        "js", "mjs", "cjs", "ts", "tsx", "jsx", "css", "scss", "less", "vue", "svelte",
        "py", "rb", "go", "rs", "java", "kt", "kts", "c", "h", "cpp", "hpp", "cc", "cs",
        "php", "sh", "bash", "zsh", "ps1", "sql", "graphql", "gql",
        "r", "lua", "pl", "swift", "dart", "scala", "clj", "ex", "exs"
    };

    private static bool IsLikelyText(string contentType, string extension)
    {
        var contentTypeValue = (contentType ?? string.Empty).Trim();

        if (contentTypeValue.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            return true;

        var semicolon = contentTypeValue.IndexOf(';');

        var bareContentType = semicolon >= 0
            ? contentTypeValue[..semicolon].Trim()
            : contentTypeValue;

        if (TextApplicationTypes.Contains(bareContentType))
            return true;

        var bareExtension = (extension ?? string.Empty).TrimStart('.').Trim();

        return TextExtensions.Contains(bareExtension);
    }
}
