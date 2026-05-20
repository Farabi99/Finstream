// INTERFACE FILE: Defines the contract for SignalEvent data access.
// Signals are events, so queries focus on retrieving historical alerts.

using FinStream.Domain.Entities;

namespace FinStream.Domain.Interfaces;

/// <summary>
/// Defines the contract for SignalEvent data access operations.
/// Signals are immutable events (like a log), so we only Add and Get - no Update needed.
/// </summary>
public interface ISignalRepository
{
    // Get a single signal event by its ID (for detailed inspection)
    Task<SignalEvent?> GetByIdAsync(Guid id);

    // Get all signals for a SPECIFIC instrument (e.g., "show me all alerts for AAPL")
    // Supports pagination for performance
    Task<IEnumerable<SignalEvent>> GetByInstrumentAsync(Guid instrumentId, int page, int pageSize);

    // Get ALL signals across ALL instruments (for global dashboards)
    // Use pagination to avoid loading too much data at once
    Task<IEnumerable<SignalEvent>> GetAllAsync(int page, int pageSize);

    // Record a new signal event (called when a rule is triggered)
    // Note: We don't have Update/Delete because signals are historical records - they're never changed
    Task<SignalEvent> AddAsync(SignalEvent signal);
}