// This file represents a "Signal Event" - an alert that fires when a trading rule is triggered.
// Think of it like a smoke detector alarm: when certain conditions are met (smoke detected),
// the alarm triggers and logs the event. In our case, when a price crosses a threshold, we log a SignalEvent.

namespace FinStream.Domain.Entities;

/// <summary>
/// Represents an "alert" or "event" that fires when a trading rule is triggered.
/// For example: "The price dropped 5%!" or "Volatility is above 3%!"
/// These are stored so traders can review what happened historically.
/// </summary>
public class SignalEvent
{
    // Unique identifier for this signal event (auto-generated)
    public Guid Id { get; set; } = Guid.NewGuid();

    // Which instrument triggered this signal (e.g., AAPL)
    public Guid InstrumentId { get; set; }

    // The name of the rule that triggered (e.g., "SPIKE", "DIP", "VOLATILE")
    // This tells us WHAT happened (the rule name explains the condition)
    public string RuleName { get; set; } = string.Empty;

    // The price at the moment the signal was triggered
    // This is crucial for analysis - "AAPL dropped from $155 to $148" is more useful than just "it dropped"
    public decimal Price { get; set; }

    // When the signal was triggered (timestamp)
    // Used for historical analysis: "Show me all signals from the last hour"
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    // Navigation property: Links back to the parent Instrument
    // This lets us easily query "all signals for AAPL" by joining through this property
    public Instrument? Instrument { get; set; }
}