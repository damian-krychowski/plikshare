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
        Func<AuditLogDetails.FileRef, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var fileRef = getFileAuditContextQuery.Execute(
            fileExternalId: fileExternalId);

        if (fileRef is null)
        {
            Logger.Warning(
                "Could not resolve file audit context for File '{FileExternalId}', skipping audit log entry",
                fileExternalId);

            return;
        }

        await Log(buildEntry(fileRef), cancellationToken);
    }

    public async ValueTask LogWithFileContexts(
        List<FileExtId> fileExternalIds,
        Func<Dictionary<FileExtId, AuditLogDetails.FileRef>, AuditLogEntry> buildEntry,
        CancellationToken cancellationToken)
    {
        var fileRefs = getFileAuditContextQuery.ExecuteMany(
            fileExternalIds: fileExternalIds);

        await Log(buildEntry(fileRefs), cancellationToken);
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
