using FinStream.Application.Services;
using FinStream.Domain.Entities;

namespace FinStream.Tests;

public class SignalEvaluatorTests
{
    private readonly RuleEngine _ruleEngine;
    private readonly SignalEvaluator _signalEvaluator;
    private readonly List<SignalRule> _rules;

    public SignalEvaluatorTests()
    {
        _ruleEngine = new RuleEngine();
        _rules = new List<SignalRule>
        {
            new() { Name = "SPIKE", ConditionType = "PRICECHANGE_PCT_GT", Threshold = 5.0m, IsActive = true },
            new() { Name = "DIP", ConditionType = "PRICECHANGE_PCT_LT", Threshold = -5.0m, IsActive = true },
            new() { Name = "VOLATILE", ConditionType = "VOLATILITY_GT", Threshold = 3.0m, IsActive = true }
        };
        _signalEvaluator = new SignalEvaluator(_ruleEngine, _rules);
    }

    [Fact]
    public void InitializeRules_ShouldAddRulesToEngine()
    {
        _signalEvaluator.InitializeRules();

        var rules = _ruleEngine.GetRules();
        Assert.Equal(3, rules.Count);
        Assert.Contains("SPIKE", rules.Keys);
        Assert.Contains("DIP", rules.Keys);
        Assert.Contains("VOLATILE", rules.Keys);
    }

    [Fact]
    public void InitializeRules_ShouldIgnoreInactiveRules()
    {
        _rules[0].IsActive = false;
        _signalEvaluator.InitializeRules();

        var rules = _ruleEngine.GetRules();
        Assert.Equal(2, rules.Count);
        Assert.DoesNotContain("SPIKE", rules.Keys);
    }

    [Fact]
    public void Evaluate_ShouldReturnMatchingSignals()
    {
        _signalEvaluator.InitializeRules();

        var metric = new MetricSnapshot
        {
            PriceChangePct = 7.5m,
            Volatility = 4.0m,
            Price = 150m
        };

        var signals = _signalEvaluator.Evaluate(metric, Guid.NewGuid()).ToList();

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, s => s.RuleName == "SPIKE");
        Assert.Contains(signals, s => s.RuleName == "VOLATILE");
    }

    [Fact]
    public void Evaluate_ShouldReturnEmpty_WhenNoMatch()
    {
        _signalEvaluator.InitializeRules();

        var metric = new MetricSnapshot
        {
            PriceChangePct = 1.0m,
            Volatility = 1.0m,
            Price = 100m
        };

        var signals = _signalEvaluator.Evaluate(metric, Guid.NewGuid()).ToList();

        Assert.Empty(signals);
    }

    [Fact]
    public void Evaluate_ShouldSetInstrumentId()
    {
        _signalEvaluator.InitializeRules();
        var instrumentId = Guid.NewGuid();

        var metric = new MetricSnapshot { PriceChangePct = 10m };
        var signals = _signalEvaluator.Evaluate(metric, instrumentId).ToList();

        Assert.Single(signals);
        Assert.Equal(instrumentId, signals[0].InstrumentId);
    }

    [Fact]
    public void Evaluate_ShouldSetPriceAndTimestamp()
    {
        _signalEvaluator.InitializeRules();
        var timestamp = DateTime.UtcNow;

        var metric = new MetricSnapshot
        {
            PriceChangePct = 10m,
            Price = 150m,
            Timestamp = timestamp
        };
        var signals = _signalEvaluator.Evaluate(metric, Guid.NewGuid()).ToList();

        Assert.Single(signals);
        Assert.Equal(150m, signals[0].Price);
        Assert.Equal(timestamp, signals[0].TriggeredAt);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenRuleEngineIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SignalEvaluator(null!, _rules));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenRulesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new SignalEvaluator(_ruleEngine, null!));
    }
}