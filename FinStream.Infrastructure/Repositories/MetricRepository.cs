// REPOSITORY FILE: Data access layer for MetricSnapshot entities.
// MetricSnapshots are calculated trading indicators (SMA, EMA, Volatility) at specific points in time.
// This repository handles storing and retrieving metric history.

using Microsoft.EntityFrameworkCore;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using FinStream.Infrastructure.Data;

namespace FinStream.Infrastructure.Repositories;

/// <summary>
/// Repository for managing MetricSnapshot data (calculated trading indicators).
/// Handles storing metric history and retrieving latest metrics for charts/analysis.
/// </summary>
public class MetricRepository : IMetricRepository
{
    // The database context - our connection to the database
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor receives the database context via dependency injection.
    /// </summary>
    /// <param name="context">The EF Core database context</param>
    public MetricRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets the most recent (latest) metric snapshot for an instrument.
    /// This is useful for displaying the current indicators in real-time.
    /// </summary>
    /// <param name="instrumentId">Which instrument to get metrics for</param>
    /// <returns>The most recent metric snapshot, or null if no metrics exist yet</returns>
    public async Task<MetricSnapshot?> GetLatestAsync(Guid instrumentId)
    {
        // Filter by instrument, order by timestamp descending, take the first one
        // This gives us the "most recent" metric
        return await _context.Metrics
            .Where(m => m.InstrumentId == instrumentId)  // Only metrics for this instrument
            .OrderByDescending(m => m.Timestamp)  // Most recent first
            .FirstOrDefaultAsync();  // Take only the first one (null if none)
    }

    /// <summary>
    /// Gets paginated metric history for an instrument.
    /// This is used for charts that show historical data (e.g., "last 50 data points").
    /// </summary>
    /// <param name="instrumentId">Which instrument to get history for</param>
    /// <param name="page">Page number (1-based, like book pages)</param>
    /// <param name="pageSize">How many items per page (e.g., 50)</param>
    /// <returns>Collection of metric snapshots for the requested page</returns>
    public async Task<IEnumerable<MetricSnapshot>> GetHistoryAsync(Guid instrumentId, int page, int pageSize)
    {
        // Filter by instrument, order by timestamp, then:
        // - Skip the items from previous pages (page-1) * pageSize
        // - Take only the items for this page (pageSize)
        //
        // Example: pageSize=50, page=2:
        //   - Skip 50 items (first page)
        //   - Take next 50 items (second page)
        return await _context.Metrics
            .Where(m => m.InstrumentId == instrumentId)  // Filter by instrument
            .OrderByDescending(m => m.Timestamp)  // Most recent first (for time-series charts)
            .Skip((page - 1) * pageSize)  // Skip items from previous pages
            .Take(pageSize)  // Take only items for this page
            .ToListAsync();
    }

    /// <summary>
    /// Saves a new metric snapshot to the database.
    /// Called by the MetricsProcessorService after calculating indicators from price ticks.
    /// </summary>
    /// <param name="metric">The calculated metric to store</param>
    /// <returns>The saved metric with populated ID</returns>
    public async Task<MetricSnapshot> AddAsync(MetricSnapshot metric)
    {
        // Add to DbSet and save to database
        _context.Metrics.Add(metric);
        await _context.SaveChangesAsync();
        return metric;
    }
}