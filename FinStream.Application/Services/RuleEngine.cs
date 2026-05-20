// SERVICE FILE: The RuleEngine evaluates metrics against trader-defined rules.
// This is the "brain" of our alerting system - it checks IF any rules were broken.
// Think of it like a smoke detector system: it constantly watches the metrics and shouts when something happens.

using FinStream.Domain.Entities;

namespace FinStream.Application.Services;

/// <summary>
/// The "brain" that evaluates trading rules against market metrics.
/// This is where we check: "Did the price drop 5%? Did volatility spike?"
/// </summary>
public class RuleEngine
{
    // Internal storage: Maps rule names to their condition functions
    // Key = rule name (e.g., "SPIKE"), Value = function that returns true/false
    // We use a Dictionary for O(1) lookup when evaluating
    private readonly Dictionary<string, Func<MetricSnapshot, bool>> _rules = new();

    /// <summary>
    /// Adds a new trading rule to the engine.
    /// Example: AddRule("SPIKE", m => m.PriceChangePct > 5)
    /// </summary>
    /// <param name="name">Human-readable name (e.g., "SPIKE", "DIP")</param>
    /// <param name="condition">Lambda function that returns true when rule should fire</param>
    public void AddRule(string name, Func<MetricSnapshot, bool> condition)
    {
        // Validation: rule names cannot be empty
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rule name cannot be empty", nameof(name));

        // Validation: condition function is required
        if (condition is null)
            throw new ArgumentNullException(nameof(condition));

        // Store the rule (overwrites if same name exists - allows rule updates)
        _rules[name] = condition;
    }

    /// <summary>
    /// Evaluates all rules against a metric snapshot.
    /// Returns the NAMES of all rules that triggered.
    /// </summary>
    /// <param name="metrics">The current market metrics to evaluate</param>
    /// <returns>List of rule names that fired (e.g., ["SPIKE", "VOLATILE"])</returns>
    public IReadOnlyList<string> Evaluate(MetricSnapshot metrics)
    {
        if (metrics is null)
            throw new ArgumentNullException(nameof(metrics));

        var triggeredRules = new List<string>();

        // Check each rule's condition against the metrics
        foreach (var rule in _rules)
        {
            // Call the condition function - if it returns true, rule triggered
            if (rule.Value(metrics))
            {
                triggeredRules.Add(rule.Key);
            }
        }

        return triggeredRules;
    }

    /// <summary>
    /// Returns all registered rules (for debugging/admin purposes).
    /// </summary>
    public IReadOnlyDictionary<string, Func<MetricSnapshot, bool>> GetRules() => _rules;

    /// <summary>
    /// Removes all rules (used when reloading from database).
    /// </summary>
    public void ClearRules() => _rules.Clear();
}