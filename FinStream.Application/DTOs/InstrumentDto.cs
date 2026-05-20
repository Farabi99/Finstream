// DTO FILE: Data Transfer Objects - lightweight data carriers for API communication.
// DTOs are different from Entities: they're designed for network transmission, not database storage.
// Think of DTOs as "public-facing" data - only show what the API consumer needs.

namespace FinStream.Application.DTOs;

/// <summary>
/// DTO for sending instrument data TO the client (read-only view).
/// This is a "flat" representation - no navigation properties to avoid circular references.
/// </summary>
public record InstrumentDto(
    // The unique ID (for API links like /api/instruments/5)
    Guid Id,

    // The ticker symbol (e.g., "AAPL") - what traders use most
    string Symbol,

    // Full company name (e.g., "Apple Inc.") - more human-readable
    string Name,

    // Whether this instrument is being tracked
    bool IsActive,

    // When it was added to the system
    DateTime CreatedAt
);

/// <summary>
/// DTO for CREATING a new instrument (client -> API).
/// This is intentionally minimal - clients only need to provide essential info.
/// </summary>
public record CreateInstrumentDto(
    // Only the symbol is required to create an instrument
    // The system will generate the ID and set defaults
    string Symbol,

    // Optional: if not provided, will default to the symbol
    string Name
);