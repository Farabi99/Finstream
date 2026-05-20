// This file represents a financial instrument (like a stock, e.g., AAPL, GOOG, MSFT).
// In the world of finance, an "instrument" is any asset that can be traded - stocks, bonds, crypto, etc.
// This is our Domain layer, meaning it contains the CORE business rules and has ZERO dependencies on other layers.

namespace FinStream.Domain.Entities;

/// <summary>
/// Represents a financial instrument (like a stock) that we want to track in our system.
/// Think of it as a "container" that holds information about a specific stock.
/// </summary>
public class Instrument
{
    // Unique identifier for this instrument in our database (automatically generated)
    public Guid Id { get; set; } = Guid.NewGuid();

    // The ticker symbol (e.g., "AAPL" for Apple). This is the "short name" traders use.
    public string Symbol { get; set; } = string.Empty;

    // The full company name (e.g., "Apple Inc."). More readable for humans.
    public string Name { get; set; } = string.Empty;

    // Whether this instrument is currently being tracked. We can "pause" tracking without deleting.
    public bool IsActive { get; set; } = true;

    // When this instrument was added to our system. Useful for auditing.
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property: An instrument can have MANY historical metric records (prices, moving averages, etc.)
    // This is called a "one-to-many" relationship in database terms.
    public ICollection<MetricSnapshot> Metrics { get; set; } = new List<MetricSnapshot>();

    // Navigation property: An instrument can have MANY signal events (alerts when rules are triggered)
    public ICollection<SignalEvent> Signals { get; set; } = new List<SignalEvent>();
}
