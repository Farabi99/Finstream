// SERVICE FILE: Calculates technical indicators from price data.
// This is where the "math magic" happens - we transform raw prices into meaningful metrics.
// Think of this like a medical device: raw heartbeat data -> heart rate, variability, etc.

using FinStream.Domain.Entities;
using FinStream.Domain.ValueObjects;

namespace FinStream.Application.Services;

/// <summary>
/// Calculates technical analysis metrics from price ticks.
/// This is the "math engine" that turns raw prices into trading indicators.
/// </summary>
public class MetricsCalculator
{
    // Rolling window of prices: we keep the last N prices (default 20) for calculations
    // Using a Queue is efficient - we add new prices at the end, remove old ones from the front
    private readonly Queue<decimal> _priceWindow = new();

    // How many prices to keep in the window (20 = "20-period moving average", standard in trading)
    // Can be changed if traders prefer shorter/longer-term analysis
    private readonly int _windowSize;

    // Stores the previous price (for calculating percentage change)
    // We need this because each tick is processed independently
    private decimal _previousPrice;

    /// <summary>
    /// Creates a new MetricsCalculator with a specified window size.
    /// </summary>
    /// <param name="windowSize">How many prices to use for moving averages (default: 20)</param>
    public MetricsCalculator(int windowSize = 20)
    {
        _windowSize = windowSize;
    }

    /// <summary>
    /// Main entry point: calculates all metrics from a single price tick.
    /// This is called for EVERY price update from the market feed.
    /// </summary>
    /// <param name="tick">The incoming price tick</param>
    /// <param name="previousMetric">The previous calculation (for continuity)</param>
    /// <returns>A complete MetricSnapshot with all calculated values</returns>
    public MetricSnapshot Calculate(Tick tick, MetricSnapshot? previousMetric = null)
    {
        // Calculate percentage change: (new - old) / old * 100
        // Example: ($105 - $100) / $100 * 100 = 5% increase
        var priceChangePct = previousMetric != null && previousMetric.Price != 0
            ? (tick.Price - previousMetric.Price) / previousMetric.Price * 100
            : 0m;

        _previousPrice = previousMetric?.Price ?? tick.Price;

        // Add the new price to our rolling window
        _priceWindow.Enqueue(tick.Price);

        // Keep the window at the right size - remove oldest if we exceed windowSize
        while (_priceWindow.Count > _windowSize)
            _priceWindow.Dequeue();

        // Convert to list for calculations
        var window = _priceWindow.ToList();

        // Calculate Simple Moving Average (average of last N prices)
        var sma = CalculateSma(window);

        // Calculate Exponential Moving Average (more weight on recent prices)
        // If we have a previous EMA, use it; otherwise, just use current price as starting point
        var ema = previousMetric?.Ema is not null
            ? CalculateEma(tick.Price, previousMetric.Ema.Value)
            : tick.Price;

        // Calculate price volatility (standard deviation as percentage)
        var volatility = CalculateVolatility(window);

        // Build and return the complete snapshot
        return new MetricSnapshot
        {
            Price = tick.Price,
            Sma = sma,
            Ema = ema,
            Volatility = volatility,
            PriceChangePct = priceChangePct,
            Timestamp = tick.Timestamp
        };
    }

    /// <summary>
    /// Simple Moving Average (SMA): Average of prices in the window.
    /// Think of it like your average test score: sum all scores, divide by count.
    /// SMA is "simple" because all prices in the window are weighted equally.
    /// </summary>
    /// <param name="prices">List of prices to average</param>
    /// <returns>The average price, rounded to 4 decimal places</returns>
    public decimal CalculateSma(IEnumerable<decimal> prices)
    {
        var priceList = prices.ToList();
        if (priceList.Count == 0) return 0;
        return Math.Round(priceList.Average(), 4);
    }

    /// <summary>
    /// Exponential Moving Average (EMA): Recursively weighted moving average.
    /// EMA is "smarter" than SMA because it gives more weight to RECENT prices.
    /// Formula: EMA = (CurrentPrice - PreviousEMA) * Multiplier + PreviousEMA
    /// The multiplier (2 / (period + 1)) determines how much weight recent prices get.
    /// </summary>
    /// <param name="currentPrice">The newest price</param>
    /// <param name="previousEma">The EMA from the previous tick</param>
    /// <param name="period">The smoothing period (default 20)</param>
    /// <returns>The new EMA value</returns>
    public decimal CalculateEma(decimal currentPrice, decimal previousEma, int period = 20)
    {
        // Multiplier determines how responsive the EMA is:
        // - Smaller period = higher multiplier = more responsive (but more noise)
        // - Larger period = lower multiplier = smoother (but slower to react)
        var multiplier = 2m / (period + 1);
        return Math.Round((currentPrice - previousEma) * multiplier + previousEma, 4);
    }

    /// <summary>
    /// Volatility: Measures how much prices fluctuate.
    /// Calculated as the Coefficient of Variation (standard deviation / mean * 100).
    /// High volatility = risky but opportunity; Low volatility = stable but boring.
    /// Uses sample standard deviation (divide by n-1) which is more accurate for small samples.
    /// </summary>
    /// <param name="prices">List of prices to analyze</param>
    /// <returns>Volatility as a percentage (e.g., 2.5 means 2.5% fluctuation)</returns>
    public decimal CalculateVolatility(IEnumerable<decimal> prices)
    {
        var priceList = prices.ToList();

        // Need at least 2 prices to calculate variance
        if (priceList.Count < 2) return 0;

        // Step 1: Calculate mean (average)
        var mean = priceList.Average();

        // Step 2: Calculate sum of squared differences from mean
        // This measures how "spread out" the prices are
        var sumOfSquares = priceList.Sum(p => (p - mean) * (p - mean));

        // Step 3: Calculate sample variance (sum of squares / n-1)
        // We use n-1 (Bessel's correction) for better accuracy with small samples
        var stdDev = Math.Sqrt((double)sumOfSquares / (priceList.Count - 1));

        // Step 4: Convert to percentage (coefficient of variation)
        // This normalizes the value so we can compare volatility across different price levels
        return stdDev > 0 ? Math.Round((decimal)stdDev / mean * 100, 4) : 0;
    }
}

