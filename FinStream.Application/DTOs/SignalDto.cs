// DTO FILE: Data Transfer Objects for signal events.

namespace FinStream.Application.DTOs;

/// <summary>
/// DTO for signal events sent to the client.
/// A signal is an alert that fires when a trading rule is triggered.
/// </summary>
public record SignalDto(
    // Unique ID of this signal event
    Guid Id,

    // The ticker symbol (e.g., "AAPL") - human-readable identifier
    // We include this instead of InstrumentId because clients care about the stock name
    string Symbol,

    // The name of the rule that triggered (e.g., "SPIKE", "DIP")
    string RuleName,

    // The price when the signal fired (context for the alert)
    decimal Price,

    // When the signal was triggered
    DateTime TriggeredAt
);

/// <summary>
/// Paginated response for signal queries.
/// </summary>
public record SignalListDto(
    // The actual signal records for this page
    IEnumerable<SignalDto> Signals,

    // Current page number
    int Page,

    // Items per page
    int PageSize,

    // Total signals across all pages
    int TotalCount
);