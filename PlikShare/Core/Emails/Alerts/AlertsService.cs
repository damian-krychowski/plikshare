using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Queue;
using Serilog;

namespace PlikShare.Core.Emails.Alerts;

public class AlertsService
{
    private readonly IClock _clock;
    private readonly IQueue _queue;
    private readonly IConfig _config;

    public AlertsService(
        IClock clock,
        IQueue queue,
        IConfig config)
    {
        _clock = clock;
        _queue = queue;
        _config = config;
    }

    public void SendEmailAlert(
        string title,
        string content,
        Guid correlationId)
    {
        try
        {
            // var result = _queue.Enqueue(
            //     correlationId: correlationId,
            //     jobType: EmailQueueJobType.Value,
            //     definition: new EmailQueueJobDefinition<AlertEmailDefinition>
            //     {
            //         Email = _config.AlertsDestinationEmail,
            //         Definition = new AlertEmailDefinition
            //         {
            //             Title = title,
            //             Content = content,
            //             EventDateTime = _clock.UtcNow
            //         },
            //         Template = EmailTemplate.Alert
            //     },
            //     debounceId: null);
            //
            // Log.Information("Alert Email '{AlertTitle}' scheduled {@EnqueuedJob}", title, result);

        }
        catch (Exception e)
        {
            Log.Error(e, "Something went wrong while scheduling Alert Email '{AlertTitle}'", title);
            
            //we are not rethrowing not to break functionality of the app
            //only because we were not able to send alert 
        }
    }
}