using PlikShare.Core.Database.MainDatabase;
using Serilog;

namespace PlikShare.Core.Queue;

public static class QueueBuilderExtensions
{
    public static void StartSqLiteQueueProcessing(
        this WebApplicationBuilder app,
        int parallelConsumersCount)
    {
        app.Services.AddSingleton<IQueue, Queue>();
        
        app.Services.AddSingleton(new QueueChannels(
            capacity: parallelConsumersCount * 3));

        app.Services.AddSingleton<QueueJobInfoProvider>();
        app.Services.AddHostedService<QueueProducer>();
        
        app.Services.AddSingleton<IHostedService>((serviceProvider) => new DbOnlyQueueConsumer(
            queue: serviceProvider.GetRequiredService<IQueue>(),
            dbWriteQueue: serviceProvider.GetRequiredService<DbWriteQueue>(),
            channels: serviceProvider.GetRequiredService<QueueChannels>(),
            dbOnlyExecutors: serviceProvider.GetServices<IQueueDbOnlyJobExecutor>()));

        for (var i = 0; i < parallelConsumersCount; i++)
        {
            var consumerIndex = i + 1;
            
            app.Services.AddSingleton<IHostedService>((serviceProvider) => new NormalQueueConsumer(
                queue: serviceProvider.GetRequiredService<IQueue>(),
                channels: serviceProvider.GetRequiredService<QueueChannels>(),
                executors: serviceProvider.GetServices<IQueueNormalJobExecutor>(),
                consumerId: consumerIndex));

            app.Services.AddSingleton<IHostedService>((serviceProvider) => new LongRunningQueueConsumer(
                queue: serviceProvider.GetRequiredService<IQueue>(),
                channels: serviceProvider.GetRequiredService<QueueChannels>(),
                executors: serviceProvider.GetServices<IQueueLongRunningJobExecutor>(),
                consumerId: consumerIndex));
        }
        
        Log.Information("[SETUP] SQLite queue processing setup finished.");
    }
}