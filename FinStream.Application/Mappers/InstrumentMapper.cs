// MAPPER FILE: Converts between Instrument entity and DTO (Data Transfer Object).
// Mappers are like translators - they convert database entities into API-friendly formats and vice versa.
// This keeps our Domain layer clean from API-specific concerns.

// We need the DTOs for API responses and the Domain entities for database operations
using FinStream.Application.DTOs;
using FinStream.Domain.Entities;

namespace FinStream.Application.Mappers;

/// <summary>
/// Mapper for converting between Instrument entity and DTO.
/// Think of it like a conversion function: Entity (database) <-> DTO (API)
/// </summary>
public static class InstrumentMapper
{
    /// <summary>
    /// Converts an Instrument entity (from database) into an InstrumentDto (for API response).
    /// Example: Database Instrument with Symbol="AAPL" -> JSON response { symbol: "AAPL" }
    /// </summary>
    /// <param name="instrument">The instrument entity from the database</param>
    /// <returns>A DTO ready to be sent to the client</returns>
    public static InstrumentDto ToDto(Instrument instrument) =>
        new(
            instrument.Id,
            instrument.Symbol,
            instrument.Name,
            instrument.IsActive,
            instrument.CreatedAt
        );

    /// <summary>
    /// Converts an InstrumentDto (from client/API) into an Instrument entity (for database).
    /// Example: JSON body { symbol: "aapl" } -> Database Instrument with Symbol="AAPL" (uppercase)
    /// We normalize the symbol to uppercase during this conversion.
    /// </summary>
    /// <param name="dto">The DTO data sent by the client</param>
    /// <returns>An entity ready to be saved to the database</returns>
    public static Instrument ToEntity(CreateInstrumentDto dto) =>
        new()
        {
            Symbol = dto.Symbol.ToUpperInvariant(),
            Name = dto.Name,
            IsActive = true  // New instruments are active by default
        };
}