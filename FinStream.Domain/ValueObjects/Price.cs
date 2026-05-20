// VALUE OBJECT: Represents a Price - a domain concept that has its own validation rules.
// Unlike entities, value objects are defined by their VALUES, not their identity.
// Example: $150.25 is the same as another $150.25 - we don't care WHICH $150.25 it is.

namespace FinStream.Domain.ValueObjects;

/// <summary>
/// Represents a monetary price with built-in validation.
/// Value objects are immutable and their equality is based on their values.
/// Think of this as "wrapping" a decimal with price-specific business rules.
/// </summary>
public record Price
{
    // The actual numeric value of the price
    // 'init' means it can only be set during object creation (immutable)
    public decimal Value { get; init; }

    // Private constructor ensures objects can ONLY be created through the factory method
    // This prevents creating invalid prices like new Price(-100)
    private Price(decimal value) => Value = value;

    // Factory method: The ONLY way to create a Price object
    // This is where we enforce business rules (validation)
    public static Price Create(decimal value)
    {
        // Business rule: Prices can never be negative
        // Real stocks don't have negative prices (though some derivatives can!)
        if (value < 0)
            throw new ArgumentException("Price cannot be negative", nameof(value));

        // Round to 2 decimal places (standard for most currencies)
        // $150.257 becomes $150.26
        return new Price(Math.Round(value, 2));
    }

    // Implicit conversion: Price can automatically become decimal
    // This lets us do: decimal x = price; (automatic)
    public static implicit operator decimal(Price price) => price.Value;

    // Explicit conversion: decimal must be explicitly cast to Price
    // This makes it clear when we're creating a Price: Price p = (Price)150.25;
    public static explicit operator Price(decimal value) => Create(value);
}