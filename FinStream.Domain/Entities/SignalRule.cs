// This file defines a "Rule" that can trigger signals.
// Think of it as defining an "IF-THEN" condition: "IF price changes by more than 5%, THEN fire a SPIKE signal".
// Traders create rules to automate their watch lists - they don't have to stare at charts all day!

namespace FinStream.Domain.Entities;

/// <summary>
/// Defines a trading rule that can trigger alerts (signals).
/// Think of it as programming a smoke detector: you set the sensitivity, and it alerts you when triggered.
/// </summary>
public class SignalRule
{
    // Unique identifier for this rule (auto-generated)
    public Guid Id { get; set; } = Guid.NewGuid();

    // Human-readable name for this rule (e.g., "SPIKE", "DIP", "HIGH_VOLATILITY")
    // This is how traders identify which rule triggered their alert
    public string Name { get; set; } = string.Empty;

    // The TYPE of condition to check (e.g., "PRICECHANGE_PCT_GT" = Price Change greater than threshold)
    // Available types:
    //   - PRICECHANGE_PCT_GT: Price changed by more than X%
    //   - PRICECHANGE_PCT_LT: Price changed by less than X%
    //   - VOLATILITY_GT: Volatility is above X%
    //   - SMA_GT: Price is above the Simple Moving Average
    //   - EMA_LT: Price is below the Exponential Moving Average
    public string ConditionType { get; set; } = string.Empty;

    // The threshold value that triggers this rule
    // For example: if ConditionType = "PRICECHANGE_PCT_GT" and Threshold = 5.0,
    // then this rule fires when the price changes by more than 5%
    public decimal Threshold { get; set; }

    // Whether this rule is currently active. Can be toggled without deleting the rule.
    public bool IsActive { get; set; } = true;

    // When this rule was created (for audit trail)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}