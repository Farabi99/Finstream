// REPOSITORY FILE: Data access layer for SignalRule entities.
// SignalRules define the conditions that trigger alerts (e.g., "alert when price > 5%").
// This repository handles storing and retrieving rule definitions.

using Microsoft.EntityFrameworkCore;
using FinStream.Domain.Entities;
using FinStream.Domain.Interfaces;
using FinStream.Infrastructure.Data;

namespace FinStream.Infrastructure.Repositories;

/// <summary>
/// Repository for managing SignalRule data (trader-defined alert conditions).
/// Handles storing rule definitions and loading rules for evaluation.
/// </summary>
public class RuleRepository : IRuleRepository
{
    // The database context - our connection to the database
    private readonly AppDbContext _context;

    /// <summary>
    /// Constructor receives the database context via dependency injection.
    /// </summary>
    /// <param name="context">The EF Core database context</param>
    public RuleRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a rule by its unique ID.
    /// </summary>
    /// <param name="id">The unique identifier</param>
    /// <returns>The rule if found, null if not</returns>
    public async Task<SignalRule?> GetByIdAsync(Guid id)
    {
        return await _context.Rules.FindAsync(id);
    }

    /// <summary>
    /// Gets ALL rules from the database.
    /// Used when listing rules for the admin/trader dashboard.
    /// </summary>
    /// <returns>Collection of all rules (active and inactive)</returns>
    public async Task<IEnumerable<SignalRule>> GetAllAsync()
    {
        return await _context.Rules.ToListAsync();
    }

    /// <summary>
    /// Gets only the ACTIVE rules from the database.
    /// Used when loading rules into the rule engine - we only want to evaluate active rules.
    /// Inactive rules are like "paused" alerts that shouldn't fire.
    /// </summary>
    /// <returns>Collection of active rules only</returns>
    public async Task<IEnumerable<SignalRule>> GetActiveAsync()
    {
        // Filter to only IsActive == true
        // This is what gets loaded into the RuleEngine at startup
        return await _context.Rules.Where(r => r.IsActive).ToListAsync();
    }

    /// <summary>
    /// Adds a new rule to the database.
    /// Called when a trader creates a new alert condition.
    /// </summary>
    /// <param name="rule">The rule to add</param>
    /// <returns>The saved rule with populated ID</returns>
    public async Task<SignalRule> AddAsync(SignalRule rule)
    {
        _context.Rules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    /// <summary>
    /// Deletes a rule from the database by its ID.
    /// Traders use this to remove alert conditions they no longer want.
    /// </summary>
    /// <param name="id">The ID of the rule to delete</param>
    public async Task DeleteAsync(Guid id)
    {
        var rule = await _context.Rules.FindAsync(id);
        if (rule != null)
        {
            _context.Rules.Remove(rule);
            await _context.SaveChangesAsync();
        }
        // If not found, we do nothing (idempotent)
    }
}