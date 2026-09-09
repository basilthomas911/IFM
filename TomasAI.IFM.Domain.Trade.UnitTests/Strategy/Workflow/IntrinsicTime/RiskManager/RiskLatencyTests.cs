using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

public sealed class RiskLatencyTests
{
    [Theory]
    [InlineData("Emulator", false)]
    [InlineData("Development", false)]
    [InlineData("Paper", false)]
    [InlineData("Test", false)]
    [InlineData("Production", true)]
    [InlineData("Live", true)]
    [InlineData("", true)]
    public async Task Age_is_observed_in_non_production_and_enforced_otherwise(string environment, bool enforced)
    {
        var input = await CompositionFixture.Command(integrationTiming: true);
        var candidate = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()).Calculate(input).Candidate!;
        var at = input.EvaluatedAtUtc.AddSeconds(2);
        var calculate = () => RiskUnitModel.ReadLegs(candidate, input.MarketSnapshot, at, environment);
        RiskLatency.EnforcesAgeLimit(environment).Should().Be(enforced);
        if (enforced) calculate.Should().Throw<RiskCalculationException>().Which.ReasonCode.Should().Be("RM.INPUT.STALE");
        else calculate().Should().ContainSingle();
    }

    [Fact]
    public async Task Missing_quotes_are_reported_as_unavailable_without_throwing_from_telemetry()
    {
        var input = await RiskFixture.Command();
        var observation = RiskLatency.Measure(input with { MarketSnapshot = input.MarketSnapshot with { Instruments = [] } });
        observation.OldestQuoteAgeMilliseconds.Should().BeNull();
        RiskLatency.Record(input, observation);
    }

    [Fact]
    public async Task Non_production_still_rejects_expired_or_future_evidence()
    {
        var input = await CompositionFixture.Command(integrationTiming: true);
        var candidate = new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()).Calculate(input).Candidate!;
        var expired = () => RiskUnitModel.ReadLegs(candidate, input.MarketSnapshot, candidate.ValidUntilUtc, "Emulator");
        expired.Should().Throw<RiskCalculationException>();
        RiskLatency.WithinAgeLimit("Paper", input.EvaluatedAtUtc, input.EvaluatedAtUtc.AddSeconds(1)).Should().BeFalse();
    }
}
