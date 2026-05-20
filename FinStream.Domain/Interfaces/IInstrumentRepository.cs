// INTERFACE FILE: This defines a "contract" - a promise of what methods will exist.
// In Clean Architecture, interfaces live in the Domain layer so that the Domain doesn't depend on Infrastructure.
// This is called "Dependency Inversion" - our business logic doesn't care HOW data is stored, just THAT it can be stored.

// Think of this like a "remote control specification":
// The TV (Domain) says "I want a button that changes channels"
// The manufacturer (Infrastructure) says "I'll build a remote that does exactly that"
// The interface is the contract that ensures the remote will work with the TV.

using FinStream.Domain.Entities;

namespace FinStream.Domain.Interfaces;

/// <summary>
/// Defines the contract for Instrument data access operations.
/// Think of this as the "job description" for anything that wants to store/retrieve Instruments.
/// </summary>
public interface IInstrumentRepository
{
    // Get a single instrument by its unique ID (like finding someone by their ID card number)
    Task<Instrument?> GetByIdAsync(Guid id);

    // Get an instrument by its ticker symbol (like finding "Apple" by searching "AAPL")
    // Symbol lookups are case-insensitive internally
    Task<Instrument?> GetBySymbolAsync(string symbol);

    // Get ALL instruments in the system (use sparingly - can be slow with thousands of instruments)
    Task<IEnumerable<Instrument>> GetAllAsync();

    // Add a new instrument to the database
    // Returns the created instrument (with its new ID assigned)
    Task<Instrument> AddAsync(Instrument instrument);

    // Update an existing instrument's information
    Task UpdateAsync(Instrument instrument);

    // Remove an instrument from the database (warning: may cascade delete related data)
    Task DeleteAsync(Guid id);
}