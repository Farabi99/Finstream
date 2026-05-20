using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using FinStream.Domain.ValueObjects;

namespace FinStream.Infrastructure.Pipeline;

/// <summary>
/// This interface represents the first "bucket" in our pipeline. 
/// It acts as a memory queue between the feed that generates data and the engine that processes it.
/// </summary>
public interface ITickChannel
{
    ChannelReader<Tick> Reader { get; }
    ChannelWriter<Tick> Writer { get; }
}

/// <summary>
/// A C# Channel is basically a highly optimized, thread-safe queue.
/// We use it so that the MarketFeedService can pump data in as fast as it wants,
/// without having to wait for the MetricsProcessorService to finish doing the math.
/// </summary>
public class TickChannel : ITickChannel
{
    public ChannelReader<Tick> Reader { get; }
    public ChannelWriter<Tick> Writer { get; }

    public TickChannel()
    {
        // Unbounded means the queue can grow infinitely. 
        // In a real production system with millions of ticks, we might make this "Bounded" to prevent running out of RAM.
        var channel = Channel.CreateUnbounded<Tick>();
        Reader = channel.Reader;
        Writer = channel.Writer;
    }
}

/// <summary>
/// This is a fake market data generator. It runs constantly in the background.
/// In a real-world scenario, you would replace this class with a WebSocket client 
/// that connects to Binance, Coinbase, or Bloomberg.
/// </summary>
public class MarketFeedService : BackgroundService
{
    private readonly ITickChannel _tickChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MarketFeedService> _logger;
    private readonly Dictionary<string, decimal> _currentPrices = new();
    private readonly Random _random = new();

    public MarketFeedService(
        ITickChannel tickChannel,
        IServiceProvider serviceProvider,
        ILogger<MarketFeedService> logger)
    {
        _tickChannel = tickChannel;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Fake MarketFeedService is booting up...");

        await InitializePricesAsync();

        // This is an infinite loop that keeps generating fake price ticks until the app is shut down.
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var symbol in _currentPrices.Keys.ToList())
            {
                // Simulate market volatility by changing the price randomly by up to 0.2%
                var volatility = (decimal)(_random.NextDouble() * 0.004 - 0.002);
                _currentPrices[symbol] *= (1 + volatility);

                var tick = Tick.Create(
                    symbol,
                    Math.Round(_currentPrices[symbol], 2),
                    DateTime.UtcNow
                );

                // Drop the new tick into the Channel (queue). 
                // The MetricsProcessorService will pick it up from here.
                await _tickChannel.Writer.WriteAsync(tick, stoppingToken);
            }

            // Generate new prices every 100 milliseconds
            await Task.Delay(100, stoppingToken);
        }
    }

    /// <summary>
    /// Grabs the list of active instruments from the database so we know what to simulate.
    /// </summary>
    private async Task InitializePricesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IInstrumentRepository>();
        var instruments = await repo.GetAllAsync();

        foreach (var inst in instruments)
        {
            // Start everyone off at an arbitrary price, e.g., $150
            _currentPrices[inst.Symbol] = 150m;
        }
    }
}