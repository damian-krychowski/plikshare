using Microsoft.Data.Sqlite;
using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Emails.Definitions;
using PlikShare.Core.Queue;
using Serilog;

namespace PlikShare.Core.Emails.Alerts;

public class AlertsService(
    AppOwners appOwners,
    IClock clock,
    IQueue queue)
{

    public void SendEmailAlert(
        DbWriteQueue.Context dbWriteContext,
        string title,
        string content,
        SqliteTransaction transaction,
        Guid? correlationId = null)
    {
        try
        {
            foreach (var appOwner in appOwners.Owners())
            {
                var result = queue.Enqueue(
                    correlationId: correlationId ?? Guid.NewGuid(),
                    jobType: EmailQueueJobType.Value,
                    definition: new EmailQueueJobDefinition<AlertEmailDefinition>
                    {
                        Email = appOwner.Value,
                        Definition = new AlertEmailDefinition
                        {
                            Title = title,
                            Content = content,
                            EventDateTime = clock.UtcNow
                        },
                        Template = EmailTemplate.Alert
                    },
                    executeAfterDate: clock.UtcNow,
                    debounceId: null,
                    sagaId: null,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction);

                Log.Information("Alert Email '{AlertTitle}' scheduled QueueJob#{QueueJobId}", title, result.Value.Value);
            }           

        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while scheduling Alert Email '{AlertTitle}'", title);
            
            //we are not rethrowing not to break functionality of the app
            //only because we were not able to send alert 
        }
    }
}