// MAPPER FILE: Converts between SignalEvent entity and DTO (Data Transfer Object).
// SignalEvents are alerts that fire when trading rules trigger.
// This mapper handles the special case of including the symbol (ticker) with the signal.

// We need the DTOs for API responses and the Domain entities for database operations
using FinStream.Application.DTOs;
using FinStream.Domain.Entities;

namespace FinStream.Application.Mappers;

/// <summary>
/// Mapper for converting between SignalEvent entity and DTO.
/// SignalEvents are notifications that fire when trading rules trigger.
/// Example: "AAPL price dropped 5%!" -> signal with RuleName="DIP", Price=150.00
/// </summary>
public static class SignalMapper
{
    /// <summary>
    /// Converts a SignalEvent entity into a SignalDto with the symbol included.
    /// This overload is used when we already know the symbol (e.g., from the instrument).
    /// </summary>
    /// <param name="signal">The signal event from the database</param>
    /// <param name="symbol">The ticker symbol (e.g., "AAPL") for human readability</param>
    /// <returns>A DTO with the symbol included</returns>
    public static SignalDto ToDto(SignalEvent signal, string symbol) =>
        new(
            signal.Id,
            symbol,
            signal.RuleName,
            signal.Price,
            signal.TriggeredAt
        );

    /// <summary>
    /// Converts a SignalEvent entity into a SignalDto.
    /// This overload is used when we need to fetch the symbol from the related Instrument.
    /// Falls back to "UNKNOWN" if the instrument is not loaded.
    /// </summary>
    /// <param name="signal">The signal event from the database</param>
    /// <returns>A DTO with the symbol from the related instrument</returns>
    public static SignalDto ToDto(SignalEvent signal) =>
        new(
            signal.Id,
            signal.Instrument?.Symbol ?? "UNKNOWN",  // Get symbol from related instrument, or "UNKNOWN" if missing
            signal.RuleName,
            signal.Price,
            signal.TriggeredAt
        );
}