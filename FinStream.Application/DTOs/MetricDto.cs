// DTO FILE: Data Transfer Objects for metric data.
// Includes the current metric values PLUS any signals triggered at that moment.

namespace FinStream.Application.DTOs;

/// <summary>
/// DTO for metric snapshots sent to the client.
/// Includes calculated values (SMA, EMA, Volatility) plus any signals that fired.
/// </summary>
public record MetricDto(
    // Unique ID of this metric snapshot
    Guid Id,

    // Which instrument this belongs to (e.g., AAPL's ID)
    Guid InstrumentId,

    // The current price at this snapshot
    decimal Price,

    // Simple Moving Average (average price over last 20 periods)
    decimal? Sma,

    // Exponential Moving Average (recent-weighted average)
    decimal? Ema,

    // Volatility percentage (how much price swings)
    decimal? Volatility,

    // Percentage change from previous price
    decimal? PriceChangePct,

    // When this snapshot was taken
    DateTime Timestamp,

    // List of signal names that were triggered (e.g., ["SPIKE", "VOLATILE"])
    // This lets the client know what alerts fired at this moment
    List<string> Signals
);

/// <summary>
/// Paginated response for metric history queries.
/// Clients need to know: "Which page am I on? How many total items are there?"
/// </summary>
public record MetricHistoryDto(
    // The actual metric records for this page
    IEnumerable<MetricDto> Metrics,

    // Current page number (1-based)
    int Page,

    // How many items per page
    int PageSize,

    // Total number of items across ALL pages (for calculating "page X of Y")
    int TotalCount
);