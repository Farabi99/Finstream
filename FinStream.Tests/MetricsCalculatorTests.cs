using FinStream.Application.Services;
using FinStream.Domain.Entities;
using FinStream.Domain.ValueObjects;

namespace FinStream.Tests;

public class MetricsCalculatorTests
{
    private readonly MetricsCalculator _calculator;

    public MetricsCalculatorTests()
    {
        _calculator = new MetricsCalculator(windowSize: 5);
    }

    [Fact]
    public void CalculateSma_ShouldReturnCorrectAverage()
    {
        var prices = new List<decimal> { 100, 102, 104, 106, 108 };

        var result = _calculator.CalculateSma(prices);

        Assert.Equal(104m, result);
    }

    [Fact]
    public void CalculateSma_ShouldReturnZero_WhenEmptyList()
    {
        var prices = new List<decimal>();

        var result = _calculator.CalculateSma(prices);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateEma_ShouldApplySmoothingFactor()
    {
        var previousEma = 100m;
        var currentPrice = 110m;

        var result = _calculator.CalculateEma(currentPrice, previousEma, period: 20);

        Assert.True(result > previousEma);
        Assert.True(result < currentPrice);
    }

    [Fact]
    public void CalculateVolatility_ShouldReturnCoefficientOfVariation()
    {
        var prices = new List<decimal> { 100, 102, 104, 106, 108 };

        var result = _calculator.CalculateVolatility(prices);

        Assert.True(result > 0);
        Assert.True(result < 100);
    }

    [Fact]
    public void CalculateVolatility_ShouldReturnZero_WhenSinglePrice()
    {
        var prices = new List<decimal> { 100 };

        var result = _calculator.CalculateVolatility(prices);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void Calculate_ShouldReturnMetricSnapshot()
    {
        var tick = Tick.Create("AAPL", 150m, DateTime.UtcNow);

        var result = _calculator.Calculate(tick, null);

        Assert.NotNull(result);
        Assert.Equal(150m, result.Price);
    }

    [Fact]
    public void Calculate_ShouldCalculatePriceChangePct()
    {
        var previousMetric = new MetricSnapshot { Price = 100m };
        var tick = Tick.Create("AAPL", 105m);

        var result = _calculator.Calculate(tick, previousMetric);

        Assert.Equal(5m, result.PriceChangePct);
    }

    [Fact]
    public void Calculate_ShouldAccumulatePriceHistory()
    {
        var tick1 = Tick.Create("AAPL", 100m);
        var tick2 = Tick.Create("AAPL", 101m);
        var tick3 = Tick.Create("AAPL", 102m);

        _calculator.Calculate(tick1, null);
        _calculator.Calculate(tick2, null);
        var result = _calculator.Calculate(tick3, null);

        Assert.Equal(102m, result.Price);
        Assert.NotNull(result.Sma);
    }

    [Fact]
    public void Calculate_ShouldMaintainWindowSize()
    {
        for (int i = 0; i < 10; i++)
        {
            var tick = Tick.Create("AAPL", 100m + i);
            _calculator.Calculate(tick, null);
        }

        var tickFinal = Tick.Create("AAPL", 115m);
        var result = _calculator.Calculate(tickFinal, null);

        Assert.True(result.Sma > 0);
    }
}