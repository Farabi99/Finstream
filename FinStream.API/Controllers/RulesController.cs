using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using FinStream.Application.DTOs;
using FinStream.Application.Mappers;
using FinStream.Domain.Interfaces;
using StackExchange.Redis;

namespace FinStream.API.Controllers;

/// <summary>
/// This controller manages the trading rules (e.g., "Alert me if Volatility > 5%").
/// Since this is the Read API, when a user creates a rule, we save it to the database 
/// and then use Redis Pub/Sub to instantly notify the background Processor app 
/// so it can reload its rule engine.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RulesController : ControllerBase
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IConnectionMultiplexer _redis;

    public RulesController(IRuleRepository ruleRepository, IConnectionMultiplexer redis)
    {
        _ruleRepository = ruleRepository;
        _redis = redis;
    }

    /// <summary>
    /// Gets all active rules.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(
        Summary = "Get All Rules", 
        Description = "Retrieves a list of all custom rules configured in the system."
    )]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<RuleDto>>> GetAll()
    {
        var rules = await _ruleRepository.GetAllAsync();
        return Ok(rules.Select(RuleMapper.ToDto));
    }

    /// <summary>
    /// Creates a new custom rule.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(
        Summary = "Create Rule", 
        Description = "Creates a new rule (e.g., VOLATILITY_GT with threshold 5) and notifies the background processor to start enforcing it."
    )]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RuleDto>> Create([FromBody] CreateRuleDto dto)
    {
        // 1. Check for duplicates
        var existingRules = await _ruleRepository.GetAllAsync();
        if (existingRules.Any(r => r.Name == dto.Name))
            return Conflict(new { message = "Rule with this name already exists" });

        // 2. Save to database
        var rule = RuleMapper.ToEntity(dto);
        await _ruleRepository.AddAsync(rule);

        // 3. Notify the background Processor app to reload its rules!
        var pubSub = _redis.GetSubscriber();
        var channel = RedisChannel.Literal("finstream:rules:changed");
        await pubSub.PublishAsync(channel, "reload");

        return CreatedAtAction(nameof(GetAll), RuleMapper.ToDto(rule));
    }

    /// <summary>
    /// Deletes a rule by ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(
        Summary = "Delete Rule", 
        Description = "Removes a rule from the database and notifies the background processor to stop enforcing it."
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var rule = await _ruleRepository.GetByIdAsync(id);
        if (rule is null)
            return NotFound();

        await _ruleRepository.DeleteAsync(id);

        // Notify the background Processor app to reload its rules
        var pubSub = _redis.GetSubscriber();
        var channel = RedisChannel.Literal("finstream:rules:changed");
        await pubSub.PublishAsync(channel, "reload");

        return NoContent();
    }
}