using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using FinStream.Application.DTOs;
using FinStream.Application.Mappers;
using FinStream.Domain.Interfaces;

namespace FinStream.API.Controllers;

/// <summary>
/// This controller handles all requests related to financial Instruments (like stocks, e.g., AAPL, GOOG).
/// In an API, a controller's job is just to be the "receptionist". It takes the HTTP request, 
/// talks to the database (via Repositories), and returns a formatted JSON response.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class InstrumentsController : ControllerBase
{
    // The repository handles the actual database queries. We inject it here so the controller 
    // doesn't have to know whether we are using SQL Server, Postgres, or an In-Memory DB.
    private readonly IInstrumentRepository _instrumentRepository;

    public InstrumentsController(IInstrumentRepository instrumentRepository)
    {
        _instrumentRepository = instrumentRepository;
    }

    /// <summary>
    /// Gets a list of all active financial instruments in the system.
    /// </summary>
    /// <returns>A list of instruments (e.g. AAPL, GOOG, MSFT).</returns>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get All Instruments", 
        Description = "Retrieves a comprehensive list of all the financial instruments currently being tracked by the FinStream engine."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<InstrumentDto>>> GetAll()
    {
        var instruments = await _instrumentRepository.GetAllAsync();
        
        // We use a "Mapper" to convert our complex Database object (Instrument) 
        // into a simple Data Transfer Object (InstrumentDto) that is safe to send over the internet.
        return Ok(instruments.Select(InstrumentMapper.ToDto));
    }

    /// <summary>
    /// Gets the details of a specific instrument using its ticker symbol.
    /// </summary>
    /// <param name="symbol">The ticker symbol of the instrument (e.g., AAPL).</param>
    /// <returns>The instrument details.</returns>
    [HttpGet("{symbol}")]
    [SwaggerOperation(
        Summary = "Get Instrument by Symbol", 
        Description = "Fetches the details of a single financial instrument using its unique ticker symbol."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InstrumentDto>> GetBySymbol(string symbol)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol);
        
        if (instrument is null)
            return NotFound(new { message = $"Instrument with symbol {symbol} was not found." });

        return Ok(InstrumentMapper.ToDto(instrument));
    }

    /// <summary>
    /// Adds a new financial instrument to the tracking system.
    /// </summary>
    /// <param name="dto">The instrument data to create.</param>
    /// <returns>The created instrument.</returns>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a New Instrument", 
        Description = "Adds a new stock or financial asset to the system so the MarketFeed service can begin tracking its prices."
    )]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InstrumentDto>> Create([FromBody] CreateInstrumentDto dto)
    {
        // Check if it already exists to prevent duplicates
        var existing = await _instrumentRepository.GetBySymbolAsync(dto.Symbol);
        if (existing != null)
            return Conflict(new { message = $"Instrument {dto.Symbol} already exists in the system." });

        var instrument = InstrumentMapper.ToEntity(dto);
        await _instrumentRepository.AddAsync(instrument);

        return CreatedAtAction(
            nameof(GetBySymbol),
            new { symbol = instrument.Symbol },
            InstrumentMapper.ToDto(instrument));
    }

    /// <summary>
    /// Removes an instrument from the tracking system.
    /// </summary>
    /// <param name="symbol">The ticker symbol of the instrument to delete.</param>
    [HttpDelete("{symbol}")]
    [SwaggerOperation(
        Summary = "Delete an Instrument", 
        Description = "Stops tracking a specific financial instrument and removes it from the database."
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string symbol)
    {
        var instrument = await _instrumentRepository.GetBySymbolAsync(symbol);
        if (instrument is null)
            return NotFound();

        await _instrumentRepository.DeleteAsync(instrument.Id);
        return NoContent();
    }
}