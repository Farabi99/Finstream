// This file represents a "snapshot" of calculated metrics for an instrument at a specific point in time.
// Think of it like taking a photo of the stock's health - it captures all the important numbers at that moment.
// This is crucial for historical analysis and debugging - we can "play back" what the market looked like at any time.

namespace FinStream.Domain.Entities;

/// <summary>
/// Represents a calculated "snapshot" of metrics for a financial instrument at a specific moment.
/// This is like a "photo" of the stock's health - it captures the price, moving averages, and volatility at that instant.
/// </summary>
public class MetricSnapshot
{
    // Unique identifier for this snapshot (auto-generated)
    public Guid Id { get; set; } = Guid.NewGuid();

    // Which instrument this snapshot belongs to (foreign key reference)
    // This connects this snapshot to its parent Instrument (e.g., AAPL)
    public Guid InstrumentId { get; set; }

    // The current price of the instrument at this moment (e.g., $150.25)
    // We use 'decimal' for financial calculations because it's more accurate than 'double'
    public decimal Price { get; set; }

    // Simple Moving Average (SMA): The average price over a window of time (usually 20 periods)
    // Think of it as "the average stock price over the last 20 ticks"
    // Nullable because we need at least 20 data points before we can calculate this
    public decimal? Sma { get; set; }

    // Exponential Moving Average (EMA): Similar to SMA but gives more weight to RECENT prices
    // This makes it more responsive to new data - traders prefer this for short-term analysis
    public decimal? Ema { get; set; }

    // Volatility: A measure of how much the price fluctuates (shown as a percentage)
    // Higher volatility = more price swings = riskier but more opportunity
    // Calculated using standard deviation of price changes
    public decimal? Volatility { get; set; }

    // Price Change Percentage: How much the price changed from the PREVIOUS tick (e.g., +2.5% or -1.2%)
    // This is what we use to detect "spikes" - sudden price movements
    public decimal? PriceChangePct { get; set; }

    // When this snapshot was taken. Critical for time-series analysis.
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation property: Links back to the parent Instrument
    // This lets us easily access "which stock does this metric belong to?"
    public Instrument? Instrument { get; set; }
}