using System.Globalization;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Pricing;
using Composer = TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CompositionFixtureCalendarTests
{
    [Theory]
    [InlineData("BullCallDebit", TimeFrameType.Monthly, "2026-09-09T17:59:00-04:00", 60, TreasuryTenor.ThreeMonth)]
    [InlineData("BullCallDebit", TimeFrameType.Monthly, "2026-09-09T18:00:00-04:00", 59, TreasuryTenor.TwoMonth)]
    [InlineData("BearPutDebit", TimeFrameType.Daily, "2026-10-09T17:59:00-04:00", 30, TreasuryTenor.TwoMonth)]
    [InlineData("BearPutDebit", TimeFrameType.Daily, "2026-10-09T18:00:00-04:00", 29, TreasuryTenor.OneMonth)]
    [InlineData("ShortBalancedIronCondor", TimeFrameType.Weekly, "2026-10-09T17:59:00-04:00", 45, TreasuryTenor.TwoMonth)]
    [InlineData("ShortBalancedIronCondor", TimeFrameType.Weekly, "2026-10-09T18:00:00-04:00", 44, TreasuryTenor.TwoMonth)]
    public async Task Synthetic_option_context_and_composition_remain_qualified_at_exchange_value_date_rollover(
        string variant, TimeFrameType horizon, string localTime, int expectedTradingDays, TreasuryTenor expectedTenor)
    {
        // These expiries cross the November DST change. At 18:00 ET valuation rolls to the
        // next value date while the same UTC expiry time becomes 17:00 ET and does not roll.
        var at = DateTimeOffset.Parse(localTime, CultureInfo.InvariantCulture).ToUniversalTime();
        var request = await CompositionFixture.Command(variant, horizon, at.UtcDateTime);
        // The selection fixture accepts its result one millisecond after the requested
        // instant; validate at the Composer invocation's actual frozen valuation time.
        var evaluatedAt = new DateTimeOffset(request.EvaluatedAtUtc);
        Assert.NotEmpty(request.MarketSnapshot.Instruments);
        foreach (var item in request.MarketSnapshot.Instruments)
        {
            Assert.NotNull(item.Instrument.Pricing);
            var context = CompositionSnapshotAdapter.To(item.Instrument.Pricing);
            Assert.Equal(expectedTradingDays, OptionPricingQualification.CountTradingDays(context.Calendar, context.Contract, evaluatedAt));
            Assert.Equal(expectedTenor, context.Rate.Tenor);
            Assert.Equal(4m, context.Rate.RatePercent);
            Assert.Equal(2 * double.LogP1(.04 / 2), context.Rate.AnnualContinuousRate);
            Assert.Null(Black76PricingModel.ValidateContext(context, evaluatedAt));
        }
        var result = new Composer(new Black76ComposerPricer()).Calculate(request);
        Assert.True(result.Outcome == CompositionOutcome.Composed,
            string.Join(",", result.CandidateDiagnostics.Select(x => $"{x.ReasonCode}={x.Count}")));
        Assert.NotNull(result.Candidate);
        Assert.Equal(horizon, result.Candidate.TargetHorizon);
    }
}
