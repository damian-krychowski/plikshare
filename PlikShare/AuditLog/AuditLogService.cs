using System.Threading.Channels;
using PlikShare.AuditLog.Queries;
using PlikShare.Files.Id;

namespace PlikShare.AuditLog;

public class AuditLogService(
    AuditLogChannel channel,
    GetFileAuditContextQuery getFileAuditContextQuery)
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<AuditLogService>();

    public async ValueTask LogWithFileContext(
        FileExtId fileExternalId,
        Func<FileAuditContext, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var fileContext = getFileAuditContextQuery.Execute(
            fileExternalId: fileExternalId);

        if (fileContext is null)
        {
            Logger.Warning(
                "Could not resolve file audit context for File '{FileExternalId}', skipping audit log entry",
                fileExternalId);

            return;
        }

        await Log(buildEntry(fileContext), cancellationToken);
    }

    public async ValueTask LogWithFileContexts(
        List<FileExtId> fileExternalIds,
        Func<Dictionary<FileExtId, FileAuditContext>, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var fileContexts = getFileAuditContextQuery.ExecuteMany(
            fileExternalIds: fileExternalIds);

        await Log(buildEntry(fileContexts), cancellationToken);
    }

    public async ValueTask Log(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await channel.WriteAsync(
                entry,
                cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            Logger.Warning(
                "Audit log channel is disposed, dropping entry: {EventType}",
                entry.EventType);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug(
                "Audit log write cancelled for entry: {EventType}",
                entry.EventType);
        }
        catch (ChannelClosedException)
        {
            Logger.Warning(
                "Audit log channel is closed, dropping entry: {EventType}",
                entry.EventType);
        }
    }
}
