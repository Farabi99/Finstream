// MAPPER FILE: Converts between MetricSnapshot entity and DTO (Data Transfer Object).
// MetricSnapshots are calculated indicators (SMA, EMA, Volatility) at a point in time.
// This mapper handles the conversion between database entities and API DTOs.

// We need the DTOs for API responses and the Domain entities for database operations
using FinStream.Application.DTOs;
using FinStream.Domain.Entities;

namespace FinStream.Application.Mappers;

/// <summary>
/// Mapper for converting between MetricSnapshot entity and DTO.
/// MetricSnapshots contain calculated trading indicators like Moving Averages and Volatility.
/// Example: Raw prices -> SMA=150.00, EMA=151.00, Volatility=2.5%
/// </summary>
public static class MetricMapper
{
    /// <summary>
    /// Converts a MetricSnapshot entity into a MetricDto with signals included.
    /// This is the full version used when we have signals that triggered.
    /// </summary>
    /// <param name="metric">The metric entity from the database</param>
    /// <param name="signals">List of signal names that triggered (e.g., ["SPIKE", "VOLATILE"])</param>
    /// <returns>A DTO with all metric data and any triggered signals</returns>
    public static MetricDto ToDto(MetricSnapshot metric, List<string> signals) =>
        new(
            metric.Id,
            metric.InstrumentId,
            metric.Price,
            metric.Sma,
            metric.Ema,
            metric.Volatility,
            metric.PriceChangePct,
            metric.Timestamp,
            signals  // Include which rules triggered for this metric
        );

    /// <summary>
    /// Converts a MetricSnapshot entity into a MetricDto with no signals.
    /// This overload is used when we don't need signal information.
    /// </summary>
    /// <param name="metric">The metric entity from the database</param>
    /// <returns>A DTO with metric data but no signals</returns>
    public static MetricDto ToDto(MetricSnapshot metric) =>
        ToDto(metric, new List<string>());  // Default to empty signal list
}