using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinStream.Domain.Entities;
using FinStream.Infrastructure.Data;

namespace FinStream.Infrastructure.Pipeline;

/// <summary>
/// A simple container holding the database records that need to be saved together.
/// </summary>
public class BatchItem
{
    public MetricSnapshot Metric { get; set; } = null!;
    public List<SignalEvent> Signals { get; set; } = new();
}

/// <summary>
/// The interface for our internal queue (bucket). 
/// It acts as a buffer between the fast math processor and the slow database.
/// </summary>
public interface IBatchChannel
{
    ChannelReader<BatchItem> Reader { get; }
    ChannelWriter<BatchItem> Writer { get; }
}

/// <summary>
/// Implementation of the IBatchChannel using System.Threading.Channels.
/// This creates an in-memory queue that can safely pass data between different threads.
/// </summary>
public class BatchChannel : IBatchChannel
{
    public ChannelReader<BatchItem> Reader { get; }
    public ChannelWriter<BatchItem> Writer { get; }

    public BatchChannel()
    {
        // We create a bounded channel holding up to 10,000 items.
        // If it gets full, it will wait before accepting more, preventing memory overflow.
        var options = new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        var channel = Channel.CreateBounded<BatchItem>(options);
        Reader = channel.Reader;
        Writer = channel.Writer;
    }
}

/// <summary>
/// A background worker service dedicated strictly to saving data to the database in bulk.
/// This is crucial for performance because inserting 1,000 records all at once 
/// is massively faster than inserting 1 record 1,000 times.
/// </summary>
public class BatchDbWriterService : BackgroundService
{
    private readonly IBatchChannel _batchChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchDbWriterService> _logger;

    public BatchDbWriterService(
        IBatchChannel batchChannel,
        IServiceProvider serviceProvider,
        ILogger<BatchDbWriterService> logger)
    {
        _batchChannel = batchChannel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BatchDbWriterService started.");

        var batch = new List<BatchItem>();
        // We will flush to the database if the queue is idle for 1 second, or if we hit 1,000 items.
        var flushInterval = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(flushInterval);

            try
            {
                // Wait for the next item or until the 1-second timer expires
                var item = await _batchChannel.Reader.ReadAsync(cts.Token);
                batch.Add(item);

                // If we reach 1,000 items, let's flush immediately
                if (batch.Count >= 1000)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // The 1-second timer expired. If we have items waiting, save them!
                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in BatchDbWriterService.");
            }
        }
    }

    /// <summary>
    /// Takes all the collected items and inserts them into the database at once.
    /// </summary>
    private async Task FlushBatchAsync(List<BatchItem> batch, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var metrics = batch.Select(b => b.Metric).ToList();
            var signals = batch.SelectMany(b => b.Signals).ToList();

            // AddRangeAsync is highly optimized for bulk inserts.
            if (metrics.Any())
                await dbContext.Metrics.AddRangeAsync(metrics, stoppingToken);

            if (signals.Any())
                await dbContext.Signals.AddRangeAsync(signals, stoppingToken);

            await dbContext.SaveChangesAsync(stoppingToken);
            
            _logger.LogDebug("Flushed {MetricCount} metrics and {SignalCount} signals to the database.", metrics.Count, signals.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush batch to database.");
        }
        finally
        {
            // Clear the list so it's ready for the next batch
            batch.Clear();
        }
    }
}
