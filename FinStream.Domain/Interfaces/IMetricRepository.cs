// INTERFACE FILE: Defines the contract for MetricSnapshot data access.
// The Domain layer defines WHAT needs to be done; Infrastructure decides HOW to do it.

using FinStream.Domain.Entities;

namespace FinStream.Domain.Interfaces;

/// <summary>
/// Defines the contract for MetricSnapshot data access operations.
/// Metrics are time-series data, so we focus on "latest" and "history" queries.
/// </summary>
public interface IMetricRepository
{
    // Get the most RECENT metric snapshot for an instrument (for real-time display)
    // This is like asking "what's the latest stock price for AAPL right now?"
    Task<MetricSnapshot?> GetLatestAsync(Guid instrumentId);

    // Get historical metric data with pagination (for charts and analysis)
    // "Give me the metrics for AAPL, page 1, 50 items per page"
    // Pagination is crucial for performance - we don't want to load millions of rows at once!
    Task<IEnumerable<MetricSnapshot>> GetHistoryAsync(Guid instrumentId, int page, int pageSize);

    // Save a new metric snapshot to the database
    // Called by the background processor after calculating new metrics
    Task<MetricSnapshot> AddAsync(MetricSnapshot metric);
}