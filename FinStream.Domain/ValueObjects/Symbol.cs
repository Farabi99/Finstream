// VALUE OBJECTS: These are domain concepts that don't have their own identity.
// Symbol = a stock ticker (e.g., "AAPL", "GOOG")
// Tick = a single price update at a specific moment

namespace FinStream.Domain.ValueObjects;

/// <summary>
/// Represents a stock ticker symbol (e.g., "AAPL", "GOOG", "MSFT").
/// Value objects enforce validation rules at creation time.
/// </summary>
public record Symbol
{
    // The ticker value (e.g., "AAPL")
    // 'init' means it can only be set during construction (immutable)
    public string Value { get; init; }

    // Private constructor - forces use of the factory method
    // Automatically converts to uppercase: "aapl" -> "AAPL" (industry standard)
    private Symbol(string value) => Value = value;

    // Factory method - the ONLY way to create a Symbol
    public static Symbol Create(string symbol)
    {
        // Validation: Symbol cannot be empty or whitespace
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));

        // Validation: Symbols have max length (e.g., "BRK.B" is 5 chars)
        if (symbol.Length > 10)
            throw new ArgumentException("Symbol cannot exceed 10 characters", nameof(symbol));

        // Normalize: trim whitespace and convert to uppercase
        return new Symbol(symbol.Trim().ToUpperInvariant());
    }

    // Implicit conversion: Symbol -> string (for easy printing/comparison)
    public static implicit operator string(Symbol symbol) => symbol.Value;

    // Explicit conversion: string -> Symbol (explicit intent)
    public static explicit operator Symbol(string value) => Create(value);
}

/// <summary>
/// Represents a single price "tick" - the smallest unit of market data.
/// Think of it as one "heartbeat" of the market: timestamp + symbol + price.
/// </summary>
public record Tick
{
    // Which stock this tick is for (e.g., AAPL)
    public Symbol Symbol { get; init; }

    // The price at this moment (e.g., 150.25)
    public decimal Price { get; init; }

    // When this tick occurred
    // We default to "now" so you can create ticks without specifying time
    public DateTime Timestamp { get; init; }

    // Private constructor ensures use of factory method
    private Tick(Symbol symbol, decimal price, DateTime timestamp)
    {
        Symbol = symbol;
        Price = price;
        Timestamp = timestamp;
    }

    // Factory method - the only way to create a Tick
    public static Tick Create(string symbol, decimal price, DateTime? timestamp = null)
    {
        // If no timestamp provided, use current time (UTC is best practice for servers)
        return new Tick(Symbol.Create(symbol), price, timestamp ?? DateTime.UtcNow);
    }
}
