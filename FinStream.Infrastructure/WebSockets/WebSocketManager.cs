// WEBSOCKET FILE: Manages WebSocket connections from browsers/clients.
// WebSockets allow real-time, bidirectional communication between the server and clients.
// Think of it like a "phone call" where the server can push messages to clients anytime,
// unlike HTTP where clients have to "ask" the server for data.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using WebSocket = System.Net.WebSockets.WebSocket;
using WebSocketState = System.Net.WebSockets.WebSocketState;

namespace FinStream.Infrastructure.WebSockets;

/// <summary>
/// A simple data structure for messages sent over WebSocket.
/// This is the JSON payload that clients receive for each market update.
/// </summary>
public class WebSocketMessage
{
    /// <summary>The ticker symbol (e.g., "AAPL")</summary>
    public string Symbol { get; set; } = string.Empty;
    /// <summary>When this update occurred</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>The current price</summary>
    public decimal Price { get; set; }
    /// <summary>Simple Moving Average</summary>
    public decimal? Sma { get; set; }
    /// <summary>Exponential Moving Average</summary>
    public decimal? Ema { get; set; }
    /// <summary>Volatility percentage</summary>
    public decimal? Volatility { get; set; }
    /// <summary>List of triggered signal names (e.g., ["SPIKE", "VOLATILE"])</summary>
    public List<string> Signals { get; set; } = new();
}

/// <summary>
/// Interface for the WebSocket manager (allows for easy testing/mocking).
/// Defines the contract for managing WebSocket connections.
/// </summary>
public interface IWebSocketManager
{
    /// <summary>Adds a new client connection to the manager</summary>
    Task AddConnectionAsync(WebSocket socket, string? symbol = null);
    /// <summary>Removes a client connection when they disconnect</summary>
    Task RemoveConnectionAsync(WebSocket socket);
    /// <summary>Sends a message to all connected clients (optionally filtered by symbol)</summary>
    Task BroadcastAsync(WebSocketMessage message);
}

/// <summary>
/// The main WebSocket connection manager. This class is responsible for:
/// 1. Tracking all connected browser clients
/// 2. Handling client disconnections
/// 3. Broadcasting messages to connected clients
///
/// It uses ConcurrentDictionary for thread-safe storage since multiple threads
/// may be adding/removing connections at the same time.
/// </summary>
public class WebSocketManager : IWebSocketManager
{
    // ConcurrentDictionary: Thread-safe dictionary for storing WebSocket connections.
    // Key = unique connection ID (Guid)
    // Value = tuple of (WebSocket socket, optional symbol filter)
    // We use a tuple because each client might want to subscribe to specific symbols only.
    private readonly ConcurrentDictionary<Guid, (WebSocket Socket, string? Symbol)> _connections = new();

    // Tracks cancellation tokens for each connection so we can stop reading when clients disconnect
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _connectionCts = new();

    /// <summary>
    /// Adds a new client WebSocket connection to the manager.
    /// Called when a client successfully upgrades their HTTP connection to WebSocket.
    /// </summary>
    /// <param name="socket">The WebSocket connection from the client</param>
    /// <param name="symbol">Optional: if provided, client only receives updates for this symbol</param>
    public async Task AddConnectionAsync(WebSocket socket, string? symbol = null)
    {
        // Generate a unique ID for this connection
        var id = Guid.NewGuid();

        // Normalize symbol to uppercase (consistent storage)
        _connections[id] = (socket, symbol?.ToUpperInvariant());

        // Create a cancellation token for this connection
        // We use this to cleanly stop reading when the client disconnects
        var cts = new CancellationTokenSource();
        _connectionCts[id] = cts;

        // Start a background task to monitor this connection
        // This task listens for incoming messages from the client (e.g., ping/pong for keep-alive)
        _ = Task.Run(async () =>
        {
            try
            {
                // Buffer for receiving data from the client
                var buffer = new byte[1024 * 4];

                // Keep reading until the socket closes or we explicitly cancel
                while (socket.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Wait for data from the client
                        await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal - connection is being closed
                        break;
                    }
                    catch (WebSocketException)
                    {
                        // Connection error - exit the loop
                        break;
                    }
                }
            }
            finally
            {
                // Clean up when the client disconnects
                await RemoveConnectionAsync(socket);
            }
        });
    }

    /// <summary>
    /// Removes a client connection and cleans up resources.
    /// Called when a client disconnects (either intentionally or due to network issues).
    /// </summary>
    /// <param name="socket">The WebSocket connection to remove</param>
    public async Task RemoveConnectionAsync(WebSocket socket)
    {
        // Find the connection by socket reference
        var connection = _connections.FirstOrDefault(c => c.Value.Socket == socket);

        if (connection.Value.Socket != null)
        {
            // Cancel any pending read operations for this connection
            _connectionCts.TryRemove(connection.Key, out var cts);
            cts?.Cancel();

            // Remove from our tracking dictionary
            _connections.TryRemove(connection.Key, out _);

            // Gracefully close the WebSocket connection
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                }
                catch { }  // Ignore errors during close (client may have already closed)
            }
        }
    }

    /// <summary>
    /// Sends a message to all connected clients.
    /// Optionally filters by symbol so clients only receive updates for stocks they care about.
    /// </summary>
    /// <param name="message">The WebSocket message to broadcast</param>
    public async Task BroadcastAsync(WebSocketMessage message)
    {
        // Serialize the message to JSON
        // JsonNamingPolicy.CamelCase converts property names like "PriceChangePct" to "priceChangePct"
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        // Get all connections that should receive this message:
        // - Clients with no symbol filter receive ALL messages
        // - Clients with a matching symbol filter receive messages for that symbol
        var tasks = _connections
            .Where(c => c.Value.Symbol == null || c.Value.Symbol == message.Symbol)
            .Select(async c =>
            {
                try
                {
                    if (c.Value.Socket.State == WebSocketState.Open)
                    {
                        // Send the JSON bytes to the client
                        // The last parameter (true) means this is the final message in a sequence
                        await c.Value.Socket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                    }
                }
                catch
                {
                    // Client disconnected - clean up
                    await RemoveConnectionAsync(c.Value.Socket);
                }
            });

        // Send to all clients in parallel (awaiting all to complete)
        await Task.WhenAll(tasks);
    }
}