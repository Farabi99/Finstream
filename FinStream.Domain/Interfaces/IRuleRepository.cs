// INTERFACE FILE: Defines the contract for SignalRule data access.
// Rules can be created, deleted, or toggled on/off, but we don't modify them once created.

using FinStream.Domain.Entities;

namespace FinStream.Domain.Interfaces;

/// <summary>
/// Defines the contract for SignalRule data access operations.
/// Traders create rules to define when they want to be alerted about market conditions.
/// </summary>
public interface IRuleRepository
{
    // Get a single rule by its unique ID
    Task<SignalRule?> GetByIdAsync(Guid id);

    // Get ALL rules (both active and inactive)
    // Use this when you need to show traders all their configured rules
    Task<IEnumerable<SignalRule>> GetAllAsync();

    // Get only ACTIVE rules (for the rule engine to evaluate)
    // This is an optimization - the processor only cares about rules that are turned ON
    Task<IEnumerable<SignalRule>> GetActiveAsync();

    // Create a new trading rule
    // Traders define: "Alert me when X happens" (e.g., "alert me when price drops 5%")
    Task<SignalRule> AddAsync(SignalRule rule);

    // Delete a rule permanently (used to remove rules traders no longer want)
    // Note: We don't have Update because rules are immutable once created
    // If a trader wants to change a rule, they delete it and create a new one
    Task DeleteAsync(Guid id);
}