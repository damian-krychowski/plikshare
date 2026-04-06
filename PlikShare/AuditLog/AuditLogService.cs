using System.Threading.Channels;

namespace PlikShare.AuditLog;

public class AuditLogService(AuditLogChannel channel)
{
    private static readonly Serilog.ILogger Logger = Serilog.Log.ForContext<AuditLogService>();

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
