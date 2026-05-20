using FinStream.Application.Services;
using FinStream.Domain.Entities;

namespace FinStream.Tests;

public class RuleEngineTests
{
    private readonly RuleEngine _ruleEngine;

    public RuleEngineTests()
    {
        _ruleEngine = new RuleEngine();
    }

    [Fact]
    public void AddRule_ShouldAddRule_WhenValid()
    {
        _ruleEngine.AddRule("TEST_RULE", m => m.Price > 100);

        var rules = _ruleEngine.GetRules();
        Assert.Single(rules);
        Assert.True(rules.ContainsKey("TEST_RULE"));
    }

    [Fact]
    public void AddRule_ShouldThrow_WhenNameIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            _ruleEngine.AddRule("", m => m.Price > 100));
    }

    [Fact]
    public void AddRule_ShouldThrow_WhenConditionIsNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _ruleEngine.AddRule("TEST_RULE", null!));
    }

    [Fact]
    public void Evaluate_ShouldReturnMatchingRules()
    {
        _ruleEngine.AddRule("HIGH_PRICE", m => m.Price > 100);
        _ruleEngine.AddRule("VOLATILE", m => m.Volatility.HasValue && m.Volatility.Value > 2);

        var metric = new MetricSnapshot
        {
            Price = 150,
            Volatility = 3.5m
        };

        var result = _ruleEngine.Evaluate(metric);

        Assert.Equal(2, result.Count);
        Assert.Contains("HIGH_PRICE", result);
        Assert.Contains("VOLATILE", result);
    }

    [Fact]
    public void Evaluate_ShouldReturnEmpty_WhenNoRulesMatch()
    {
        _ruleEngine.AddRule("HIGH_PRICE", m => m.Price > 100);

        var metric = new MetricSnapshot { Price = 50 };

        var result = _ruleEngine.Evaluate(metric);

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_ShouldReturnEmpty_WhenNoRules()
    {
        var metric = new MetricSnapshot { Price = 150 };

        var result = _ruleEngine.Evaluate(metric);

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_ShouldThrow_WhenMetricIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _ruleEngine.Evaluate(null!));
    }

    [Fact]
    public void ClearRules_ShouldRemoveAllRules()
    {
        _ruleEngine.AddRule("RULE1", m => true);
        _ruleEngine.AddRule("RULE2", m => true);

        _ruleEngine.ClearRules();

        Assert.Empty(_ruleEngine.GetRules());
    }

    [Fact]
    public void MultipleRules_ShouldEvaluateIndependently()
    {
        _ruleEngine.AddRule("SPIKE_UP", m => m.PriceChangePct > 5);
        _ruleEngine.AddRule("SPIKE_DOWN", m => m.PriceChangePct < -5);

        var metric1 = new MetricSnapshot { PriceChangePct = 7 };
        var metric2 = new MetricSnapshot { PriceChangePct = -8 };

        var result1 = _ruleEngine.Evaluate(metric1);
        var result2 = _ruleEngine.Evaluate(metric2);

        Assert.Single(result1);
        Assert.Contains("SPIKE_UP", result1);
        Assert.Single(result2);
        Assert.Contains("SPIKE_DOWN", result2);
    }
}