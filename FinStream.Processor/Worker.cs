// WORKER FILE: A placeholder background service (not currently used).
// The actual background work is done by MarketFeedService, MetricsProcessorService,
// and BatchDbWriterService which are registered directly in Program.cs.
//
// This class demonstrates how to create a basic BackgroundService.
// It's kept here as a reference but is not registered in the DI container.

namespace FinStream.Processor;

/// <summary>
/// A basic background service template.
/// Shows the pattern for creating a long-running worker in .NET.
///
/// HOW TO USE:
/// 1. Create a class that inherits from BackgroundService
/// 2. Override ExecuteAsync() with your main logic
/// 3. Register in DI with: builder.Services.AddHostedService<YourWorker>();
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// This is the main loop that runs until the service stops.
    /// It logs every second to show the worker is running.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Loop until cancellation is requested (service stopping)
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            // Wait 1 second before the next iteration
            // This prevents busy-waiting (using 100% CPU)
            await Task.Delay(1000, stoppingToken);
        }
    }
}
