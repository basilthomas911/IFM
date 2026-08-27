using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal;

public sealed class FuturesTradeSignalCommandStateChangeTests
{
    [Fact]
    public void EverySemanticSnapshotField_CanTriggerADurableUpdate()
    {
        var command = SampleData.CreateTradeSignalUpdateCommand();
        var state = new FuturesTradeSignalCommandState { Id = command.Subject.ThreadId };
        command.Execute(state).Success.Should().BeTrue();
        state.FuturesTradeSignal.Should().NotBeNull();
        var baseline = state.FuturesTradeSignal!;

        FuturesTradeSignalV2ReadModel[] changedSnapshots =
        [
            baseline with { ContractId = $"{baseline.ContractId}-changed" },
            baseline with { ValueDate = baseline.ValueDate.AddDays(1) },
            baseline with { TimePeriod = Different(baseline.TimePeriod) },
            baseline with { Mean = baseline.Mean + 1 },
            baseline with { StdDev = baseline.StdDev + 1 },
            baseline with { FuturesPrice = baseline.FuturesPrice + 1 },
            baseline with { PriceChangePercent = baseline.PriceChangePercent + 1 },
            baseline with { FundRiskPercent = baseline.FundRiskPercent + 1 },
            baseline with { RSI = baseline.RSI + 1 },
            baseline with { RSISlope = baseline.RSISlope + 1 },
            baseline with { TrendType = Different(baseline.TrendType) },
            baseline with { TrendStrength = Different(baseline.TrendStrength) },
            baseline with { TradeSignal = Different(baseline.TradeSignal) },
            baseline with { TDI = Different(baseline.TDI) },
            baseline with { TDIStrength = Different(baseline.TDIStrength) },
            baseline with { MDI = baseline.MDI + 1 },
            baseline with { MDITrend = Different(baseline.MDITrend) },
            baseline with { MDIUpTrendLimit = baseline.MDIUpTrendLimit + 1 },
            baseline with { MDIDownTrendLimit = baseline.MDIDownTrendLimit + 1 },
            baseline with { UpTrendingTrigger = baseline.UpTrendingTrigger + 1 },
            baseline with { DownTrendingTrigger = baseline.DownTrendingTrigger + 1 },
            baseline with { EntryTrigger = baseline.EntryTrigger + 1 },
            baseline with { ExitTrigger = baseline.ExitTrigger + 1 },
            baseline with { TrendDelta = baseline.TrendDelta + 1 },
            baseline with { TrendExtreme = baseline.TrendExtreme + 1 },
            baseline with { TrendReversal = baseline.TrendReversal + 1 },
            baseline with { FiftyDMA = baseline.FiftyDMA + 1 },
            baseline with { TwoHundredDMA = baseline.TwoHundredDMA + 1 },
            baseline with { TradeExecuteState = Different(baseline.TradeExecuteState) }
        ];

        changedSnapshots.Should().OnlyContain(snapshot => state.HasFuturesTradeSignalChanged(snapshot));
    }

    [Fact]
    public void TransportOrderingFields_DoNotCreateFalseBusinessChanges()
    {
        var command = SampleData.CreateTradeSignalUpdateCommand();
        var state = new FuturesTradeSignalCommandState { Id = command.Subject.ThreadId };
        command.Execute(state).Success.Should().BeTrue();
        state.FuturesTradeSignal.Should().NotBeNull();
        var baseline = state.FuturesTradeSignal!;

        state.HasFuturesTradeSignalChanged(baseline with
        {
            SequenceId = baseline.SequenceId + 1,
            Timestamp = baseline.Timestamp.Add(TimeSpan.FromSeconds(1))
        }).Should().BeFalse();
    }

    static TEnum Different<TEnum>(TEnum current)
        where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().First(value => !EqualityComparer<TEnum>.Default.Equals(value, current));
}
