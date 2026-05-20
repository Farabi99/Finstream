// REPOSITORY FILE: Data access layer for SignalEvent entities.
// SignalEvents are alerts that fire when trading rules trigger (e.g., "price spiked 5%!").
// This repository handles storing and retrieving signal history.

using Microsoft.EntityFrameworkCore;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using FinStream.Infrastructure.Data;

namespace FinStream.Infrastructure.Repositories;

/// <summary>
/// Repository for managing SignalEvent data (alerts when rules trigger).
/// Handles storing signal history and retrieving signals for display/notifications.
/// </summary>
public class SignalRepository : ISignalRepository
{
    // The database context - our connection to the database
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor receives the database context via dependency injection.
    /// </summary>
    /// <param name="context">The EF Core database context</param>
    public SignalRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a signal event by its unique ID.
    /// </summary>
    /// <param name="id">The unique identifier</param>
    /// <returns>The signal if found, null if not</returns>
    public async Task<SignalEvent?> GetByIdAsync(Guid id)
    {
        return await _context.Signals.FindAsync(id);
    }

    /// <summary>
    /// Gets paginated signals for a specific instrument (stock).
    /// Used when viewing signal history for a particular ticker symbol.
    /// </summary>
    /// <param name="instrumentId">Which instrument to get signals for</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <returns>Collection of signals for the requested page</returns>
    public async Task<IEnumerable<SignalEvent>> GetByInstrumentAsync(Guid instrumentId, int page, int pageSize)
    {
        // .Include() eagerly loads the related Instrument entity
        // This is important so we can show the ticker symbol (e.g., "AAPL") with each signal
        return await _context.Signals
            .Include(s => s.Instrument)  // Load the related instrument to get its symbol
            .Where(s => s.InstrumentId == instrumentId)  // Filter to this instrument
            .OrderByDescending(s => s.TriggeredAt)  // Most recent first
            .Skip((page - 1) * pageSize)  // Skip previous pages
            .Take(pageSize)  // Take only this page
            .ToListAsync();
    }

    /// <summary>
    /// Gets all signals across all instruments with pagination.
    /// Used for the main signals dashboard showing all alerts.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Items per page</param>
    /// <returns>Collection of signals for the requested page</returns>
    public async Task<IEnumerable<SignalEvent>> GetAllAsync(int page, int pageSize)
    {
        // Include the instrument so we can show which ticker triggered the signal
        return await _context.Signals
            .Include(s => s.Instrument)  // Load related instrument for symbol display
            .OrderByDescending(s => s.TriggeredAt)  // Most recent first
            .Skip((page - 1) * pageSize)  // Skip previous pages
            .Take(pageSize)  // Take only this page
            .ToListAsync();
    }

    /// <summary>
    /// Saves a new signal event to the database.
    /// Called by the MetricsProcessorService when a rule triggers.
    /// </summary>
    /// <param name="signal">The signal event to store</param>
    /// <returns>The saved signal with populated ID</returns>
    public async Task<SignalEvent> AddAsync(SignalEvent signal)
    {
        _context.Signals.Add(signal);
        await _context.SaveChangesAsync();
        return signal;
    }
}