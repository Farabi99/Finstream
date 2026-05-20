// BACKPLANE FILE: Bridges Redis Pub/Sub messages to local WebSocket connections.
// This service enables horizontal scaling (multiple API servers) by using Redis as a message bus.
// Without this, only clients connected to the SAME API server would receive updates.
// With this, ALL clients across ALL servers receive updates through the shared Redis channel.

// This is a BackgroundService, which means it runs continuously in the background
// from the moment the application starts until it shuts down.
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FinStream.Infrastructure.WebSockets;

/// <summary>
/// This service acts as the "bridge" between the centralized Redis Pub/Sub system and
/// the local WebSocket connections. It listens for messages broadcast by other services
/// through the Redis channel, then forwards them to browsers connected to THIS server.
///
/// Think of it like a "radio relay station":
/// - The MetricsProcessorService broadcasts market updates to a Redis channel
/// - ALL API servers hear this broadcast (because they're all subscribed to the same Redis channel)
/// - Each server's RedisWebSocketBackplane then forwards the message to its LOCAL connected browsers
///
/// This is what enables horizontal scaling - multiple API servers can handle different browser connections,
/// but all browsers receive the same real-time data through the shared Redis message bus.
/// </summary>
public class RedisWebSocketBackplane : BackgroundService
{
    // Redis connection multiplexer - provides access to Redis pub/sub
    private readonly IConnectionMultiplexer _redis;

    // The local WebSocket manager - handles the actual browser connections
    private readonly IWebSocketManager _webSocketManager;

    // Logger for debugging and monitoring
    private readonly ILogger<RedisWebSocketBackplane> _logger;

    /// <summary>
    /// Constructor receives dependencies via dependency injection.
    /// .NET will automatically inject the Redis connection and WebSocket manager.
    /// </summary>
    public RedisWebSocketBackplane(
        IConnectionMultiplexer redis,
        IWebSocketManager webSocketManager,
        ILogger<RedisWebSocketBackplane> logger)
    {
        _redis = redis;
        _webSocketManager = webSocketManager;
        _logger = logger;
    }

    /// <summary>
    /// The main execution loop. This runs continuously from startup to shutdown.
    /// It subscribes to the Redis "finstream:ws:broadcast" channel and forwards all messages
    /// to locally connected WebSocket clients.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RedisWebSocketBackplane started. Listening to Redis for market updates...");

        // Get the Redis pub/sub subscriber
        var pubSub = _redis.GetSubscriber();

        // The channel name - must match what MetricsProcessorService publishes to
        var channel = RedisChannel.Literal("finstream:ws:broadcast");

        // JSON deserialization options (case-insensitive property matching)
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // Subscribe to the Redis channel
        // This callback runs whenever a message is published to the channel
        await pubSub.SubscribeAsync(channel, async (ch, message) =>
        {
            try
            {
                if (message.HasValue)
                {
                    // Deserialize the JSON message back into a WebSocketMessage object
                    var wsMessage = JsonSerializer.Deserialize<WebSocketMessage>(message.ToString(), options);

                    if (wsMessage != null)
                    {
                        // Forward to all locally connected WebSocket clients
                        await _webSocketManager.BroadcastAsync(wsMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast message from Redis backplane.");
            }
        });

        // Keep the service running until cancellation is requested
        // (SubscribeAsync is persistent - it keeps the subscription active)
        while (!stoppingToken.IsCancellationRequested)
        {
            // Wait 1 second at a time, checking if we should stop
            // This prevents busy-waiting and allows graceful shutdown
            await Task.Delay(1000, stoppingToken);
        }
    }
}
