// REPOSITORY FILE: Data access layer for Instrument entities.
// Repository pattern: Provides a clean interface for accessing data.
// Think of this as a "data access service" that hides the details of talking to the database.
// The controller doesn't need to know if we're using SQL Server, Postgres, or InMemory DB.

using Microsoft.EntityFrameworkCore;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using FinStream.Infrastructure.Data;

namespace FinStream.Infrastructure.Repositories;

/// <summary>
/// Repository for managing Instrument (stock/ETF) data.
/// This class handles all database operations for instruments using Entity Framework Core.
/// Think of it as the "data access layer" that the Application layer uses to interact with instruments.
/// </summary>
public class InstrumentRepository : IInstrumentRepository
{
    // The database context - this is our connection to the database
    // We don't create new instances; EF Core injects this via dependency injection
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor receives the database context via dependency injection.
    /// This is how .NET's DI container wires up our repository to the database.
    /// </summary>
    /// <param name="context">The EF Core database context</param>
    public InstrumentRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets an instrument by its unique ID (primary key lookup).
    /// This is the fastest way to look up a single instrument.
    /// </summary>
    /// <param name="id">The unique identifier (GUID)</param>
    /// <returns>The instrument if found, null if not</returns>
    public async Task<Instrument?> GetByIdAsync(Guid id)
    {
        // FindAsync uses the primary key - very efficient
        return await _context.Instruments.FindAsync(id);
    }

    /// <summary>
    /// Gets an instrument by its ticker symbol (e.g., "AAPL").
    /// We normalize to uppercase because ticker symbols are always uppercase.
    /// </summary>
    /// <param name="symbol">The ticker symbol (case-insensitive)</param>
    /// <returns>The instrument if found, null if not</returns>
    public async Task<Instrument?> GetBySymbolAsync(string symbol)
    {
        // FirstOrDefaultAsync finds the first match or returns null
        // ToUpperInvariant() normalizes input so "aapl" matches "AAPL"
        return await _context.Instruments
            .FirstOrDefaultAsync(i => i.Symbol == symbol.ToUpperInvariant());
    }

    /// <summary>
    /// Gets ALL instruments from the database.
    /// Use this for listing all tracked stocks/ETFs.
    /// </summary>
    /// <returns>Collection of all instruments</returns>
    public async Task<IEnumerable<Instrument>> GetAllAsync()
    {
        // ToListAsync() executes the query and returns results as a list
        return await _context.Instruments.ToListAsync();
    }

    /// <summary>
    /// Adds a new instrument to the database.
    /// EF Core tracks the new entity and saves it on the next SaveChanges call.
    /// </summary>
    /// <param name="instrument">The instrument to add</param>
    /// <returns>The saved instrument (with generated ID if new)</returns>
    public async Task<Instrument> AddAsync(Instrument instrument)
    {
        // Add the entity to the DbSet - EF Core marks it for insertion
        _context.Instruments.Add(instrument);

        // SaveChanges() actually executes the SQL INSERT statement
        await _context.SaveChangesAsync();

        return instrument;  // Return the same object (now with ID populated if it was new)
    }

    /// <summary>
    /// Updates an existing instrument in the database.
    /// EF Core marks the entity as Modified so SaveChanges will generate an UPDATE statement.
    /// </summary>
    /// <param name="instrument">The instrument with updated values</param>
    public async Task UpdateAsync(Instrument instrument)
    {
        // Tell EF Core this entity has been modified
        _context.Entry(instrument).State = EntityState.Modified;

        // SaveChanges() generates and executes the SQL UPDATE statement
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes an instrument from the database by its ID.
    /// Because we configured cascade delete, this also deletes all related metrics and signals.
    /// </summary>
    /// <param name="id">The ID of the instrument to delete</param>
    public async Task DeleteAsync(Guid id)
    {
        // First, find the instrument to delete (FindAsync is efficient for primary keys)
        var instrument = await _context.Instruments.FindAsync(id);

        if (instrument != null)
        {
            // Remove() marks it for deletion, SaveChanges() executes the SQL DELETE
            _context.Instruments.Remove(instrument);
            await _context.SaveChangesAsync();
        }
        // If not found, we do nothing (idempotent - safe to call multiple times)
    }
}