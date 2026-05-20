// SERVICE FILE: SignalEvaluator bridges the gap between Rules (database) and RuleEngine (memory).
// It converts database rule definitions into executable conditions, then triggers signals when rules fire.
// Think of it as a "translator": the database says "PRICECHANGE_PCT_GT > 5", this creates the code to check it.

using FinStream.Domain.Entities;

namespace FinStream.Application.Services;

/// <summary>
/// Bridges the RuleEngine with database rules.
/// Converts rule definitions into executable conditions, then creates SignalEvents when rules trigger.
/// </summary>
public class SignalEvaluator
{
    // The RuleEngine that does the actual evaluation
    private readonly RuleEngine _ruleEngine;

    // Rules from the database (loaded at startup or when rules change)
    private readonly IEnumerable<SignalRule> _rules;

    /// <summary>
    /// Creates a new SignalEvaluator.
    /// </summary>
    /// <param name="ruleEngine">The rule engine to use for evaluation</param>
    /// <param name="rules">The rules to evaluate</param>
    public SignalEvaluator(RuleEngine ruleEngine, IEnumerable<SignalRule> rules)
    {
        // Validate inputs - we can't work without these
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>
    /// Loads rules from database into the rule engine.
    /// This is called on startup and whenever rules change.
    /// </summary>
    public void InitializeRules()
    {
        // Clear existing rules (start fresh)
        _ruleEngine.ClearRules();

        // Only add ACTIVE rules (ignore disabled rules)
        foreach (var rule in _rules.Where(r => r.IsActive))
        {
            // Convert the database rule definition into an executable condition
            var condition = CreateCondition(rule);

            // Add to the rule engine
            _ruleEngine.AddRule(rule.Name, condition);
        }
    }

    /// <summary>
    /// Converts a SignalRule (from database) into an executable condition function.
    /// This is where the "translation" happens: database strings become code.
    /// </summary>
    /// <param name="rule">The rule definition from the database</param>
    /// <returns>A function that returns true when the rule should fire</returns>
    private Func<MetricSnapshot, bool> CreateCondition(SignalRule rule)
    {
        // Pattern matching: check the ConditionType and create the right condition
        return rule.ConditionType.ToUpperInvariant() switch
        {
            // "Price went up by more than X%"
            "PRICECHANGE_PCT_GT" => m => m.PriceChangePct.HasValue && m.PriceChangePct.Value > rule.Threshold,

            // "Price went down by more than X%"
            "PRICECHANGE_PCT_LT" => m => m.PriceChangePct.HasValue && m.PriceChangePct.Value < rule.Threshold,

            // "Price is swinging more than X%"
            "VOLATILITY_GT" => m => m.Volatility.HasValue && m.Volatility.Value > rule.Threshold,

            // "Price is above its moving average" (bullish signal)
            "SMA_GT" => m => m.Sma.HasValue && m.Price > m.Sma.Value,

            // "Price is below its moving average" (bearish signal)
            "SMA_LT" => m => m.Sma.HasValue && m.Price < m.Sma.Value,

            // "Price is above EMA" (short-term bullish)
            "EMA_GT" => m => m.Ema.HasValue && m.Price > m.Ema.Value,

            // "Price is below EMA" (short-term bearish)
            "EMA_LT" => m => m.Ema.HasValue && m.Price < m.Ema.Value,

            // Unknown type: never fires
            _ => _ => false
        };
    }

    /// <summary>
    /// Evaluates all rules against a metric and creates SignalEvents for any triggered rules.
    /// </summary>
    /// <param name="metric">The current metrics to evaluate</param>
    /// <param name="instrumentId">Which instrument these metrics are for</param>
    /// <returns>SignalEvents for each rule that fired</returns>
    public IEnumerable<SignalEvent> Evaluate(MetricSnapshot metric, Guid instrumentId)
    {
        // Ask the rule engine which rules triggered
        var triggeredRuleNames = _ruleEngine.Evaluate(metric);

        // Convert rule names to SignalEvent objects
        // Each signal records: which instrument, which rule, what price, when
        return triggeredRuleNames.Select(name => new SignalEvent
        {
            InstrumentId = instrumentId,
            RuleName = name,
            Price = metric.Price,
            TriggeredAt = metric.Timestamp
        });
    }
}