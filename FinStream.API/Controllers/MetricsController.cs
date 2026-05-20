using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using FinStream.Application.DTOs;
using FinStream.Application.Mappers;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using StackExchange.Redis;

namespace FinStream.API.Controllers;

/// <summary>
/// This controller fetches the mathematical calculations (Metrics like Moving Averages) 
/// for our financial instruments. It implements the "CQRS" pattern by reading 
/// the latest state directly from a high-speed Redis Cache instead of the slow SQL database.
/// </summary>
[ApiController]
[Route("api/instruments/{symbol}/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly IMetricRepository _metricRepository;
    private readonly IConnectionMultiplexer _redis;

    public MetricsController(
        IInstrumentRepository instrumentRepository, 
        IMetricRepository metricRepository,
        IConnectionMultiplexer redis)
    {
        _instrumentRepository = instrumentRepository;
        _metricRepository = metricRepository;
        _redis = redis;
    }

    /// <summary>
    /// Gets the absolute latest calculated metrics for a given instrument.
    /// </summary>
    /// <param name="symbol">The ticker symbol (e.g., AAPL)</param>
    /// <returns>The most recently calculated metrics from the fast cache.</returns>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get Latest Metrics", 
        Description = "Instantly fetches the most recently calculated metrics (SMA, EMA, Volatility) for an instrument by querying the high-speed Redis Cache."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MetricDto>> GetLatest(string symbol)
    {
        // 1. We check if the instrument actually exists in our system.
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol);
        if (instrument is null)
            return NotFound(new { message = $"Instrument {symbol} not found." });

        // 2. CQRS IN ACTION: Instead of hitting the SQL database, we ask Redis!
        // The background FinStream.Processor app constantly updates this key in Redis.
        var db = _redis.GetDatabase();
        var cacheKey = $"metrics:latest:{symbol.ToUpperInvariant()}";
        var cachedData = await db.StringGetAsync(cacheKey);

        if (cachedData.HasValue)
        {
            // If Redis has the data, we deserialize it back into a C# object and return it.
            var metricSnapshot = JsonSerializer.Deserialize<MetricSnapshot>(cachedData!);
            return Ok(MetricMapper.ToDto(metricSnapshot!));
        }

        // 3. Fallback: If Redis is empty (maybe the server just restarted), 
        // we fall back to checking the slow SQL database just in case.
        var metricDb = await _metricRepository.GetLatestAsync(instrument.Id);
        if (metricDb is null)
            return NotFound(new { message = $"No metrics calculated yet for {symbol}." });

        return Ok(MetricMapper.ToDto(metricDb));
    }

    /// <summary>
    /// Gets the historical metrics for a given instrument.
    /// </summary>
    /// <param name="symbol">The ticker symbol (e.g., AAPL)</param>
    /// <param name="page">The page number for pagination</param>
    /// <param name="pageSize">How many records to return per page</param>
    /// <returns>A paginated history of metrics.</returns>
    [HttpGet("history")]
    [SwaggerOperation(
        Summary = "Get Historical Metrics", 
        Description = "Retrieves a paginated list of historical metrics from the SQL Database. Use this to plot historical charts."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MetricHistoryDto>> GetHistory(
        string symbol,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol);
        if (instrument is null)
            return NotFound();

        // Historical data is way too big to store in Redis, 
        // so we ask the SQL database for this using the Repository.
        var metrics = await _metricRepository.GetHistoryAsync(instrument.Id, page, pageSize);
        
        // Convert the database entities to DTOs for the web response.
        var dtos = metrics.Select(m => MetricMapper.ToDto(m));

        return Ok(new MetricHistoryDto(dtos, page, pageSize, dtos.Count()));
    }
}