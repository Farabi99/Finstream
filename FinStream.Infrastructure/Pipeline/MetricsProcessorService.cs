using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FinStream.Application.Services;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using FinStream.Domain.ValueObjects;
using FinStream.Infrastructure.WebSockets;
using StackExchange.Redis;

namespace FinStream.Infrastructure.Pipeline;

/// <summary>
/// This is the core engine of the application. It acts as a background worker that constantly
/// listens for new market data, calculates metrics (like moving averages), checks trading rules,
/// updates the fast Redis cache, pushes the data to a bulk database writer, and finally
/// broadcasts the updates to all web clients via Redis Pub/Sub.
/// </summary>
public class MetricsProcessorService : BackgroundService
{
    private readonly ITickChannel _tickChannel;
    private readonly IBatchChannel _batchChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly MetricsCalculator _calculator;
    private readonly RuleEngine _ruleEngine;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<MetricsProcessorService> _logger;
    
    private SignalEvaluator _signalEvaluator = null!;
    private readonly Dictionary<string, Guid> _instrumentIds = new();
    private bool _initialized;

    public MetricsProcessorService(
        ITickChannel tickChannel,
        IBatchChannel batchChannel,
        IServiceProvider serviceProvider,
        MetricsCalculator calculator,
        RuleEngine ruleEngine,
        IConnectionMultiplexer redis,
        ILogger<MetricsProcessorService> logger)
    {
        _tickChannel = tickChannel;
        _batchChannel = batchChannel;
        _serviceProvider = serviceProvider;
        _calculator = calculator;
        _ruleEngine = ruleEngine;
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetricsProcessorService started");

        // We only initialize the rules and IDs once when the service boots up.
        if (!_initialized)
        {
            await InitializeInstrumentsAsync();
            await InitializeRulesAsync();
            _initialized = true;
        }

        // Subscribe to rule changes from the API
        var pubSub = _redis.GetSubscriber();
        var rulesChannel = RedisChannel.Literal("finstream:rules:changed");
        await pubSub.SubscribeAsync(rulesChannel, async (channel, message) =>
        {
            _logger.LogInformation("Rule change detected. Reloading rules from database...");
            await InitializeRulesAsync();
        });

        // This is a continuous loop. It waits here until new data is dropped into the TickChannel.
        await foreach (var tick in _tickChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessTickAsync(tick, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tick for {Symbol}", tick.Symbol.Value);
            }
        }
    }

    private async Task InitializeInstrumentsAsync()
    {
        // We use a scope here because Repositories are usually 'Scoped' to a single HTTP request,
        // but this is a background service (which is a Singleton). We have to manually create a scope
        // to safely grab the data from the database.
        using var scope = _serviceProvider.CreateScope();
        var instrumentRepo = scope.ServiceProvider.GetRequiredService<IInstrumentRepository>();
        var instruments = await instrumentRepo.GetAllAsync();

        foreach (var instrument in instruments)
        {
            _instrumentIds[instrument.Symbol] = instrument.Id;
        }
    }

    private async Task InitializeRulesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var ruleRepo = scope.ServiceProvider.GetRequiredService<IRuleRepository>();
        var rules = await ruleRepo.GetAllAsync();
        
        // SignalEvaluator combines our metric data with our active trading rules to generate alerts.
        _signalEvaluator = new SignalEvaluator(_ruleEngine, rules);
        _signalEvaluator.InitializeRules();
    }

    private async Task ProcessTickAsync(Tick tick, CancellationToken stoppingToken)
    {
        if (!_instrumentIds.TryGetValue(tick.Symbol.Value, out var instrumentId))
        {
            return; // We don't care about symbols that aren't in our database.
        }

        var db = _redis.GetDatabase();
        var cacheKey = $"metrics:latest:{tick.Symbol.Value}";
        
        // 1. Fetch the previous state from Redis. This allows us to scale out to multiple servers 
        // since they all share the same centralized Redis cache instead of local memory.
        MetricSnapshot? previousMetric = null;
        var cachedData = await db.StringGetAsync(cacheKey);
        if (cachedData.HasValue)
        {
            previousMetric = JsonSerializer.Deserialize<MetricSnapshot>(cachedData!);
        }

        // 2. Do the heavy math
        var metric = _calculator.Calculate(tick, previousMetric);
        metric.InstrumentId = instrumentId;

        // 3. Check if any rules were broken (e.g. "Price dropped 5%!")
        var signals = _signalEvaluator.Evaluate(metric, instrumentId).ToList();

        // 4. Update the centralized Redis cache with the new state instantly.
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(metric));

        // 5. Send the data to the BatchDbWriterService queue. 
        // We do NOT save to the database here, which keeps this processor running at lightning speed.
        var batchItem = new BatchItem { Metric = metric, Signals = signals };
        await _batchChannel.Writer.WriteAsync(batchItem, stoppingToken);

        // 6. Broadcast the updates to all connected web clients via Redis Pub/Sub.
        // Instead of talking to a local WebSocket manager, we shout the message to Redis.
        // That way, if we have 5 API servers running, they ALL hear the message and send it to their users.
        var signalNames = signals.Select(s => s.RuleName).ToList();
        var wsMessage = new WebSocketMessage
        {
            Symbol = tick.Symbol.Value,
            Timestamp = tick.Timestamp,
            Price = tick.Price,
            Sma = metric.Sma,
            Ema = metric.Ema,
            Volatility = metric.Volatility,
            Signals = signalNames
        };

        var pubSub = _redis.GetSubscriber();
        var broadcastChannel = RedisChannel.Literal("finstream:ws:broadcast");
        await pubSub.PublishAsync(broadcastChannel, JsonSerializer.Serialize(wsMessage));
    }
}