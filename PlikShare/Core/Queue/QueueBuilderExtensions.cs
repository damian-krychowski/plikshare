using Serilog;

namespace PlikShare.Core.Queue;

public static class QueueBuilderExtensions
{
    public static void StartSqLiteQueueProcessing(
        this WebApplicationBuilder app,
        int parallelConsumersCount)
    {
        app.Services.AddSingleton<IQueue, Queue>();
        app.Services.AddSingleton<QueueBatchNotifier>();
        app.Services.AddSingleton<QueueProducerWakeSignal>();

        app.Services.AddSingleton(new QueueChannels(
            capacity: parallelConsumersCount * 3));

        // QueueJobInfoProvider is registered in Startup.RegisterServices with a pre-built job map
        // (see AddNormalQueueJob / AddLongRunningQueueJob) — not here, because the executor-driven
        // constructor would form an IQueue -> Queue -> provider dependency cycle.
        app.Services.AddHostedService<QueueProducer>();

        for (var i = 0; i < parallelConsumersCount; i++)
        {
            var consumerIndex = i + 1;
            
            app.Services.AddSingleton<IHostedService>(serviceProvider => new NormalQueueConsumer(
                queue: serviceProvider.GetRequiredService<IQueue>(),
                channels: serviceProvider.GetRequiredService<QueueChannels>(),
                executors: serviceProvider.GetServices<IQueueNormalJobExecutor>(),
                consumerId: consumerIndex));

            app.Services.AddSingleton<IHostedService>(serviceProvider => new LongRunningQueueConsumer(
                queue: serviceProvider.GetRequiredService<IQueue>(),
                channels: serviceProvider.GetRequiredService<QueueChannels>(),
                executors: serviceProvider.GetServices<IQueueLongRunningJobExecutor>(),
                consumerId: consumerIndex));
        }
        
        Log.Information("[SETUP] SQLite queue processing setup finished.");
    }
}