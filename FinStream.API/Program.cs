// PROGRAM FILE: The main entry point and configuration for the FinStream API.
// This is the "Read API" (CQRS Query side) - it handles HTTP requests and WebSocket connections.
// It reads data from the SQL Database and Redis cache, serving historical data and real-time updates.
// Unlike the Processor, this API does NOT generate market data or evaluate rules.
// Think of it as the "customer-facing" part of the application.

// ASP.NET Core packages for web server functionality
using Microsoft.EntityFrameworkCore;
using FinStream.Application.Services;
using FinStream.Domain.Interfaces;
using FinStream.Infrastructure.Data;
using FinStream.Infrastructure.Pipeline;
using FinStream.Infrastructure.Repositories;
using FinStream.Infrastructure.WebSockets;

// Alias to avoid conflict with our FinStream.WebSockets namespace
using FinStreamWsManager = FinStream.Infrastructure.WebSockets.WebSocketManager;
using StackExchange.Redis;
using System.Reflection;

namespace FinStream.API;

/// <summary>
/// Main program class for the FinStream REST API.
/// This API is the "Read side" of our CQRS (Command Query Responsibility Segregation) architecture:
///
/// - CQRS Pattern: We separate the "write" operations (creating/updating data)
///   from the "read" operations (querying data). This API ONLY reads data.
///
/// - What this API does:
///   1. Serves historical data from the SQL Database
///   2. Serves current state from the Redis cache (fast reads)
///   3. Handles WebSocket connections for real-time updates
///   4. Provides REST endpoints for managing instruments, rules, metrics, and signals
///
/// - What this API does NOT do:
///   1. It does NOT run the MarketFeedService (that generates fake market data)
///   2. It does NOT run the MetricsProcessorService (that calculates indicators)
///   3. It does NOT evaluate rules or save new metrics/signals
///
/// This separation allows us to scale the "read" side independently from the "write" side
/// and simplifies the code by having each component do one thing well.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point. This method is called when the application starts.
    /// It configures all services, middleware, and endpoints.
    /// </summary>
    public static void Main(string[] args)
    {
        // Create the web application builder (this sets up the basic web server infrastructure)
        var builder = WebApplication.CreateBuilder(args);

        // ============== STEP 1: Configure Core Services ==============

        // Add Controllers: Enables MVC pattern for handling HTTP requests
        // This registers the services needed to route requests to controller actions
        builder.Services.AddControllers();

        // Add API Explorer: Enables endpoints to be discovered by tools like Swagger
        builder.Services.AddEndpointsApiExplorer();

        // ============== STEP 2: Configure Swagger (API Documentation) ==============
        // Swagger generates interactive API documentation that's great for:
        // - Testing APIs during development
        // - Letting frontend developers see available endpoints
        // - Documenting the API for external users

        builder.Services.AddSwaggerGen(options =>
        {
            // Enable annotations allows Swagger to read our [SwaggerOperation] attributes
            options.EnableAnnotations();

            // Configure the API metadata shown at the top of the Swagger page
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "FinStream REST API",
                Version = "v1",
                Description = @"A real-time financial market data streaming API.
This API is the 'Read' side of a CQRS architecture. It pulls historical data from the SQL Database
and real-time state from a Redis cache."
            });

            // Load XML comments from our code to include in the Swagger documentation
            // This makes our [Summary] and [Description] attributes appear in Swagger!
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        // ============== STEP 3: Configure Database (Entity Framework Core) ==============
        // Entity Framework Core is our ORM (Object-Relational Mapper).
        // It lets us work with databases using C# objects instead of raw SQL.

        // The DatabaseProvider setting controls which database we connect to:
        // - "InMemory" (default): Uses RAM, great for testing and demos
        // - "SqlServer": Microsoft SQL Server
        // - "Postgres": PostgreSQL
        var dbProvider = builder.Configuration["DatabaseProvider"] ?? "InMemory";
        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (dbProvider == "SqlServer")
                options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
            else if (dbProvider == "Postgres")
                options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
            else
                // InMemory database - everything stays in RAM, resets on restart
                // Perfect for demos, testing, and development
                options.UseInMemoryDatabase("FinStreamDb");
        });

        // ============== STEP 4: Register Repositories ==============
        // Repositories provide a clean interface for data access.
        // We register them as "Scoped" - one instance per HTTP request.
        // This keeps database connections short-lived and prevents memory leaks.

        builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        builder.Services.AddScoped<IMetricRepository, MetricRepository>();
        builder.Services.AddScoped<ISignalRepository, SignalRepository>();
        builder.Services.AddScoped<IRuleRepository, RuleRepository>();

        // ============== STEP 5: Connect to Redis ==============
        // Redis is used for:
        // 1. Caching the latest metrics for fast reads
        // 2. Pub/Sub for WebSocket horizontal scaling
        // ConnectionMultiplexer is the StackExchange.Redis way to connect to Redis.
        // We register it as a Singleton because one connection can handle many operations.

        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            // Get connection string from config, default to localhost if not set
            var redisConnStr = config.GetConnectionString("Redis") ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(redisConnStr);
        });

        // ============== STEP 6: Register WebSocket Services ==============

        // The WebSocketManager tracks all browser connections and handles message routing
        builder.Services.AddSingleton<IWebSocketManager, FinStreamWsManager>();

        // RedisWebSocketBackplane: A background service that:
        // - Listens to Redis pub/sub channel for market updates
        // - Forwards those updates to locally connected browsers
        // This is what enables horizontal scaling - multiple API servers can
        // share the same data stream via Redis.
        builder.Services.AddHostedService<RedisWebSocketBackplane>();

        // ============== STEP 7: Configure CORS (Cross-Origin Resource Sharing) ==============
        // CORS allows browsers from other domains to access our API.
        // This is necessary for frontend applications running on different ports/domains.
        // The policy below allows ALL origins, methods, and headers (permissive for development).

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            });
        });

        // ============== BUILD THE APPLICATION ==============

        var app = builder.Build();

        // ============== CONFIGURE HTTP PIPELINE ==============
        // The HTTP pipeline processes each incoming request through a series of middleware components.

        // Enable Swagger UI at /swagger (accessible in browser)
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "FinStream API v1");
            c.RoutePrefix = "swagger";  // Make Swagger the default page
        });

        // Enable CORS
        app.UseCors();

        // Enable WebSocket support
        app.UseWebSockets();

        // Map all controller routes (e.g., /api/instruments, /api/rules)
        app.MapControllers();

        // Map WebSocket endpoint at /ws/stream
        app.MapWebSocketManager();

        // Health check endpoint - returns OK if the server is running
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

        // Run the application! This starts the web server and begins listening for requests.
        app.Run();
    }
}

/// <summary>
/// Extension methods for mapping WebSocket endpoints.
/// These methods add WebSocket routing to the application.
/// </summary>
public static class WebSocketExtensions
{
    /// <summary>
    /// Maps the WebSocket endpoint at /ws/stream.
    /// Clients connect here to receive real-time market data updates.
    /// </summary>
    public static void MapWebSocketManager(this WebApplication app)
    {
        // Map a GET request to /ws/stream that upgrades to WebSocket
        app.Map("/ws/stream", async (HttpContext context, IWebSocketManager manager) =>
        {
            // Reject non-WebSocket requests with a 400 Bad Request
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            // Extract optional "symbol" query parameter for filtering
            // Example: ws://localhost/ws/stream?symbol=AAPL
            var symbol = context.Request.Query["symbol"].FirstOrDefault();

            // Accept the WebSocket connection
            var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            // Register this connection with the WebSocketManager
            // After this, the RedisWebSocketBackplane will forward market updates to this client
            await manager.AddConnectionAsync(webSocket, symbol);
        });
    }
}