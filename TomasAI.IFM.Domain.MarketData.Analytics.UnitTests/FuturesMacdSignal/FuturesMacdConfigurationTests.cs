using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesMacdSignal;

public sealed class FuturesMacdConfigurationTests
{
    [Fact]
    public void Standard_UsesConventionalMacdPeriods()
    {
        var configuration = FuturesMacdConfiguration.Standard;

        configuration.SignalEmaPeriod.Should().Be(9);
        configuration.FastEmaPeriod.Should().Be(12);
        configuration.SlowEmaPeriod.Should().Be(26);
    }

    [Fact]
    public void EntityIdentity_ContainsAllThreeEmaPeriods()
    {
        var valueDate = new DateOnly(2026, 8, 14);
        var standard = new FuturesMacdSignalEntityId(
            "ESU6",
            valueDate,
            TimeFrameType.OneMinute);
        var custom = new FuturesMacdSignalEntityId(
            "ESU6",
            valueDate,
            TimeFrameType.OneMinute,
            signalEmaPeriod: 7,
            fastEmaPeriod: 10,
            slowEmaPeriod: 30);

        standard.Format().Should().EndWith(".OneMinute.9.12.26");
        custom.Format().Should().EndWith(".OneMinute.7.10.30");
        custom.Format().Should().NotBe(standard.Format());
    }

    [Fact]
    public void Compute_UsesConventionalRecursiveEmaCalculationAndCurrentPrice()
    {
        FuturesMacdSignalCompute.Create(
            100m,
            [],
            FuturesMacdConfiguration.Standard,
            out var first);
        var previous = new FuturesMacdSignalReadModel(
            "ESU6",
            new DateOnly(2026, 8, 14),
            TimeFrameType.OneMinute,
            signalEmaPeriod: 9,
            fastEmaPeriod: 12,
            slowEmaPeriod: 26,
            timestamp: new TimeOnly(10, 0),
            futuresPrice: 100m,
            macdLine: first.MacdLine,
            signalLine: first.SignalLine,
            histogram: first.Histogram,
            macd: FuturesTrendDirectionType.Init,
            macdStrength: FuturesTrendDirectionStrengthType.Low,
            fastEma: first.FastEma,
            slowEma: first.SlowEma);

        FuturesMacdSignalCompute.Create(
            110m,
            [previous],
            FuturesMacdConfiguration.Standard,
            out var second);

        second.FastEma.Should().BeApproximately(101.5384615385d, 0.0000000001d);
        second.SlowEma.Should().BeApproximately(100.7407407407d, 0.0000000001d);
        second.MacdLine.Should().BeApproximately(0.7977207977d, 0.0000000001d);
        second.SignalLine.Should().BeApproximately(0.1595441595d, 0.0000000001d);
        second.Histogram.Should().BeApproximately(0.6381766382d, 0.0000000001d);
    }

    [Fact]
    public void Validation_RejectsFastPeriodThatIsNotLessThanSlowPeriod()
    {
        var configuration = new FuturesMacdConfiguration(
            signalEmaPeriod: 9,
            fastEmaPeriod: 26,
            slowEmaPeriod: 12);

        var errors = new FuturesMacdConfigurationValidationRules().Execute(configuration);

        errors.Should().ContainSingle(error =>
            error.ErrorMessage.Contains("FastEmaPeriod", StringComparison.Ordinal));
    }
}
