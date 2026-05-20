// PROGRAM FILE: The main entry point for the FinStream Processor background service.
// This is the "Write API" (CQRS Command side) - it generates and processes market data.
// Think of this as the "data factory" that runs continuously in the background.
//
// What the Processor does (it has no HTTP endpoints):
// 1. MarketFeedService: Generates fake market price data (simulates a real-time feed)
// 2. MetricsProcessorService: Calculates SMA, EMA, Volatility from prices, evaluates rules
// 3. BatchDbWriterService: Saves calculated metrics and signals to the database in batches
//
// This runs as a Windows Service or Linux Daemon, NOT as a web API.
// It's designed to run on a server that processes market data 24/7.

using Microsoft.EntityFrameworkCore;
using FinStream.Application.Services;
using FinStream.Domain.Interfaces;
using FinStream.Infrastructure.Data;
using FinStream.Infrastructure.Pipeline;
using FinStream.Infrastructure.Repositories;
using StackExchange.Redis;

namespace FinStream.Processor;

/// <summary>
/// The main program class for the FinStream Processor background service.
/// This is the "Write side" of CQRS - it generates data, not reads it.
///
/// ARCHITECTURE OVERVIEW:
/// The Processor uses a Channel<T>-based pipeline (inspired by Test B's requirements):
///
///     MarketFeedService (produces fake ticks every 100ms)
///            │
///            ▼
///     Channel<Tick> (thread-safe queue)
///            │
///            ▼
///     MetricsProcessorService (consumes ticks, calculates metrics, checks rules)
///            │
///            ├──► Redis (latest metrics cache + WebSocket broadcast)
///            │
///            ▼
///     BatchDbWriterService (batches writes, saves every 1 second or 1000 items)
///            │
///            ▼
///     SQL Database (persistent storage)
///
/// This pipeline is:
/// - Thread-safe (Channels handle synchronization)
/// - Fast (batch writes, no blocking)
/// - Scalable (Redis pub/sub for horizontal WebSocket scaling)
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point. Configures and starts the background worker services.
    /// </summary>
    public static void Main(string[] args)
    {
        // Create a Host - this is .NET's way of running background services
        // Unlike WebApplication (for APIs), Host is for long-running services
        var builder = Host.CreateApplicationBuilder(args);

        // ============== STEP 1: Configure Database (Entity Framework Core) ==============
        // Same as the API - we support InMemory (dev), SQL Server, or PostgreSQL
        // This is where metrics and signals are stored permanently
        var dbProvider = builder.Configuration["DatabaseProvider"] ?? "InMemory";
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (dbProvider == "SqlServer")
            {
                options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
            }
            else if (dbProvider == "Postgres")
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
            }
            else
            {
                // InMemory for development/testing
                options.UseInMemoryDatabase("FinStreamDb");
            }
        });

        // ============== STEP 2: Register Repositories ==============
        // Repositories allow us to talk to the database using clean interfaces
        // These are scoped services - one instance per logical operation
        builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        builder.Services.AddScoped<IMetricRepository, MetricRepository>();
        builder.Services.AddScoped<ISignalRepository, SignalRepository>();
        builder.Services.AddScoped<IRuleRepository, RuleRepository>();

        // ============== STEP 3: Register Channels (The Pipeline) ==============
        // ITickChannel: Queue for price ticks from the market feed
        // IBatchChannel: Queue for metrics/signals waiting to be saved to database
        //
        // Channels are thread-safe, async-friendly queues built into .NET.
        // They're better than ConcurrentQueue + Timer (Test B anti-pattern) because:
        // - Native async/await support
        // - Built-in backpressure (bounded channels wait when full)
        // - No manual lock management needed
        builder.Services.AddSingleton<ITickChannel, TickChannel>();
        builder.Services.AddSingleton<IBatchChannel, BatchChannel>();

        // ============== STEP 4: Register Business Logic Services ==============
        // MetricsCalculator: Calculates SMA, EMA, Volatility from raw prices
        // RuleEngine: Checks if any trading rules triggered based on metrics
        // Both are stateless singletons - no need for multiple instances
        builder.Services.AddSingleton<MetricsCalculator>();
        builder.Services.AddSingleton<RuleEngine>();

        // ============== STEP 5: Connect to Redis ==============
        // Redis is used for TWO purposes in the Processor:
        // 1. Cache: Store the LATEST metrics for fast reads by the API
        //    (API reads from Redis, Processor writes to Redis)
        // 2. Pub/Sub: Broadcast WebSocket messages to ALL API servers
        //    (This enables horizontal scaling of WebSocket connections)
        //
        // IMPORTANT: We use a FACTORY LAMBDA (sp => ...) instead of connecting eagerly.
        // Why? In Docker, the connection string comes from environment variables
        // (e.g., ConnectionStrings__Redis=redis:6379). These are only available
        // through IConfiguration AFTER the host is built. If we called
        // ConnectionMultiplexer.Connect() here directly, it would try to connect
        // to "localhost:6379" before Docker's environment overrides are applied.
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var redisConnStr = config.GetConnectionString("Redis") ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(redisConnStr);
        });

        // ============== STEP 6: Register Background Worker Services ==============
        // These run continuously from startup to shutdown:
        //
        // MarketFeedService: Generates fake price ticks every 100ms
        //   - Simulates a real market data feed (e.g., from Binance or Bloomberg)
        //   - In production, replace this with an actual WebSocket client
        //   - Writes to ITickChannel
        //
        // MetricsProcessorService: Processes ticks from the channel
        //   - Reads from ITickChannel
        //   - Calculates metrics using MetricsCalculator
        //   - Evaluates rules using RuleEngine
        //   - Writes to Redis (cache + pub/sub)
        //   - Writes batch items to IBatchChannel
        //
        // BatchDbWriterService: Saves data to database
        //   - Reads from IBatchChannel
        //   - Batches items for 1 second OR 1000 items
        //   - Bulk inserts to SQL database
        builder.Services.AddHostedService<MarketFeedService>();
        builder.Services.AddHostedService<MetricsProcessorService>();
        builder.Services.AddHostedService<BatchDbWriterService>();

        // Build the host (this starts all the background services)
        var host = builder.Build();

        // ============== STEP 7: Initialize the Rule Engine on Startup ==============
        // Load rules from the database and add them to the RuleEngine.
        // This happens ONCE at startup, not on every tick (too expensive).
        // The RulesController publishes to Redis when rules change,
        // and MetricsProcessorService subscribes to reload.
        using (var scope = host.Services.CreateScope())
        {
            // Ensure database exists (creates if it doesn't, like EnsureCreated)
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.EnsureCreated();

            // Load the rule engine
            var ruleEngine = scope.ServiceProvider.GetRequiredService<RuleEngine>();
            var ruleRepo = scope.ServiceProvider.GetRequiredService<IRuleRepository>();
            var rules = ruleRepo.GetAllAsync().GetAwaiter().GetResult();

            // For each active rule, convert its database definition into a lambda function
            // This is the same pattern as SignalEvaluator but at startup time
            foreach (var rule in rules.Where(r => r.IsActive))
            {
                // Convert condition type string to actual comparison function
                // Example: "PRICECHANGE_PCT_GT" with threshold 5.0 becomes:
                //   m => m.PriceChangePct.HasValue && m.PriceChangePct.Value > 5.0
                var condition = rule.ConditionType.ToUpperInvariant() switch
                {
                    "PRICECHANGE_PCT_GT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.PriceChangePct.HasValue && m.PriceChangePct.Value > rule.Threshold),
                    "PRICECHANGE_PCT_LT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.PriceChangePct.HasValue && m.PriceChangePct.Value < rule.Threshold),
                    "VOLATILITY_GT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.Volatility.HasValue && m.Volatility.Value > rule.Threshold),
                    "SMA_GT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.Sma.HasValue && m.Price > m.Sma.Value),
                    "SMA_LT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.Sma.HasValue && m.Price < m.Sma.Value),
                    "EMA_GT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.Ema.HasValue && m.Price > m.Ema.Value),
                    "EMA_LT" => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(m => m.Ema.HasValue && m.Price < m.Ema.Value),
                    _ => (Func<FinStream.Domain.Entities.MetricSnapshot, bool>)(_ => false)  // Unknown type: never fires
                };
                ruleEngine.AddRule(rule.Name, condition);
            }
        }

        // Start the host - this runs all background services
        host.Run();
    }
}
