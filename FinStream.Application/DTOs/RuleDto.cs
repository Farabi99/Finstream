// DTO FILE: Data Transfer Objects for trading rules.

namespace FinStream.Application.DTOs;

/// <summary>
/// DTO for rule information sent to the client.
/// </summary>
public record RuleDto(
    // Unique ID of this rule
    Guid Id,

    // Human-readable name (e.g., "SPIKE", "DIP", "HIGH_VOLATILITY")
    string Name,

    // The type of condition (e.g., "PRICECHANGE_PCT_GT")
    // This tells the client what metric is being checked
    string ConditionType,

    // The threshold value (e.g., 5.0 means "5%")
    decimal Threshold,

    // Whether this rule is currently active
    bool IsActive,

    // When this rule was created
    DateTime CreatedAt
);

/// <summary>
/// DTO for creating a new rule (client -> API).
/// </summary>
public record CreateRuleDto(
    // Name for the new rule
    string Name,

    // Type of condition (what to check)
    string ConditionType,

    // Threshold value (when to trigger)
    string Threshold
);