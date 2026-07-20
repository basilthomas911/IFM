using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.CommandHandlers;

public class FuturesItiSignalCommandTests
{
    // Trading-day lookup in the compute model only supports Weekly and Monthly; Daily is intentionally unsupported.
    public static readonly TheoryData<TradeTimePeriodType> SupportedTimePeriods = new()
    {
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    public static readonly TheoryData<TradeTimePeriodType> AllTimePeriods = new()
    {
        TradeTimePeriodType.Daily,
        TradeTimePeriodType.Weekly,
        TradeTimePeriodType.Monthly
    };

    static FuturesItiSignalCommandState NewState() => new();

    /// <summary>
    /// Extracts the resulting read model from the most recently applied event on the state, since the
    /// compute model's internal accessors are not visible outside the domain assembly.
    /// </summary>
    static FuturesItiSignalV2ReadModel LastSignal(FuturesItiSignalCommandState state)
        => ((FuturesItiSignalGeneratedEvent)state.Events[^1]).FuturesItiSignal!;

    static FuturesItiSignalCommandState GivenStartOfDayState(TradeTimePeriodType timePeriod, double? futuresPrice = null)
    {
        var state = NewState();
        var command = SampleData.GenerateCommandFor(timePeriod) with
        {
            FuturesPrice = futuresPrice ?? (double)SampleData.FuturesPrice
        };
        command.Execute(state);
        return state;
    }

    // ───── GenerateFuturesItiSignalCommand — Start of day (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenNoExistingSignal_WhenGenerateFuturesItiSignalCommandIsExecuted_ThenStartOfDaySignalIsApplied(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = NewState();
        var command = SampleData.GenerateCommandFor(timePeriod);

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        state.Updated.Should().BeTrue();
        state.Events.Should().ContainSingle();
        var signal = LastSignal(state);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
    }

    [Fact]
    public void GivenNoExistingSignal_WhenGenerateFuturesItiSignalCommandIsExecutedForDailyPeriod_ThenThrowsArgumentOutOfRangeException()
    {
        // Given
        var state = NewState();
        var command = SampleData.GenerateCommandFor(TradeTimePeriodType.Daily);

        // When
        var act = () => command.Execute(state);

        // Then
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ───── GenerateFuturesItiSignalCommand — Up-trend transitions (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceFallsBelowDownTrendTrigger_ThenTrendDirectionChangesToDownTrend(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var previous = LastSignal(state);
        var command = SampleData.GenerateCommandFor(timePeriod) with
        {
            FuturesPrice = previous.DownTrendTrigger - 10
        };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        state.Events.Should().HaveCount(2);
        LastSignal(state).IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceExceedsTrendExtreme_ThenTrendExtremeIsUpdated(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var higherPrice = LastSignal(state).TrendExtreme + 50;
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = higherPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        signal.TrendExtreme.Should().Be(higherPrice);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceFallsBelowTrendReversal_ButNotBelowDownTrendTrigger_ThenTrendReversalIsUpdated(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        // Move the trend extreme up first so the reversal level sits above the down-trend trigger.
        var extremeCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).TrendExtreme + 100 };
        extremeCommand.Execute(state);

        var afterExtreme = LastSignal(state);
        var reversalPrice = afterExtreme.TrendReversal - 1;
        reversalPrice.Should().BeGreaterThan(afterExtreme.DownTrendTrigger);
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = reversalPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        signal.TrendReversal.Should().Be(reversalPrice);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceStaysWithinRange_ThenSignalIsTrending(
        TradeTimePeriodType timePeriod)
    {
        // Given: nudge the reversal level down slightly so a subsequent price between the reversal
        // and the extreme lands inside the "no change" range, since the initial start-of-day signal
        // has trend price, extreme, and reversal all equal (any other price would trip a transition).
        var state = GivenStartOfDayState(timePeriod);
        var startOfDay = LastSignal(state);
        var reversalCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = startOfDay.TrendReversal - 1 };
        reversalCommand.Execute(state);

        var afterReversal = LastSignal(state);
        var withinRangePrice = (afterReversal.TrendReversal + afterReversal.TrendExtreme) / 2;
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = withinRangePrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.Trending);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
    }

    // ───── GenerateFuturesItiSignalCommand — Down-trend transitions (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingDownTrendSignal_WhenPriceExceedsUpTrendTrigger_ThenTrendDirectionChangesToUpTrend(
        TradeTimePeriodType timePeriod)
    {
        // Given: force a down-trend by dropping the price below the initial down-trend trigger.
        var state = GivenStartOfDayState(timePeriod);
        var downTrendCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).DownTrendTrigger - 10 };
        downTrendCommand.Execute(state);
        var downTrendSignal = LastSignal(state);
        downTrendSignal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);

        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = downTrendSignal.UpTrendTrigger + 10 };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingDownTrendSignal_WhenPriceFallsBelowTrendExtreme_ThenTrendExtremeIsUpdated(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var downTrendCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).DownTrendTrigger - 10 };
        downTrendCommand.Execute(state);
        var downTrendSignal = LastSignal(state);
        downTrendSignal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);

        var lowerPrice = downTrendSignal.TrendExtreme - 5;
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = lowerPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        signal.TrendExtreme.Should().Be(lowerPrice);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingDownTrendSignal_WhenPriceExceedsTrendReversal_ButNotAboveUpTrendTrigger_ThenTrendReversalIsUpdated(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var downTrendCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).DownTrendTrigger - 10 };
        downTrendCommand.Execute(state);
        LastSignal(state).IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);

        var extremeCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).TrendExtreme - 100 };
        extremeCommand.Execute(state);

        var afterExtreme = LastSignal(state);
        var reversalPrice = afterExtreme.TrendReversal + 1;
        reversalPrice.Should().BeLessThan(afterExtreme.UpTrendTrigger);
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = reversalPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        signal.TrendReversal.Should().Be(reversalPrice);
    }

    // ───── SetFuturesItiSignalHoldTradeCommand (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalInReadyState_WhenSetFuturesItiSignalHoldTradeCommandIsExecuted_ThenTradeStateBecomesHold(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        var command = SampleData.SetHoldTradeCommandFor(timePeriod);

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalAlreadyOnHold_WhenSetFuturesItiSignalHoldTradeCommandIsExecuted_ThenNoUpdateOccurs(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        SampleData.SetHoldTradeCommandFor(timePeriod).Execute(state);
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
        var command = SampleData.SetHoldTradeCommandFor(timePeriod);
        var eventCountBefore = state.Events.Count;

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeFalse();
        state.Events.Should().HaveCount(eventCountBefore);
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
    }

    [Fact]
    public void GivenNoExistingSignal_WhenSetFuturesItiSignalHoldTradeCommandIsExecuted_ThenNoUpdateOccurs()
    {
        // Given
        var state = NewState();
        var command = SampleData.SetHoldTradeCommand;

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeFalse();
        state.Events.Should().BeEmpty();
    }

    // ───── ClearFuturesItiSignalHoldTradeCommand (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalOnHold_WhenClearFuturesItiSignalHoldTradeCommandIsExecuted_ThenTradeStateBecomesReady(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        SampleData.SetHoldTradeCommandFor(timePeriod).Execute(state);
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
        var command = SampleData.ClearHoldTradeCommandFor(timePeriod);

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeTrue();
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalAlreadyReady_WhenClearFuturesItiSignalHoldTradeCommandIsExecuted_ThenNoUpdateOccurs(
        TradeTimePeriodType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        var command = SampleData.ClearHoldTradeCommandFor(timePeriod);
        var eventCountBefore = state.Events.Count;

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeFalse();
        state.Events.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void GivenNoExistingSignal_WhenClearFuturesItiSignalHoldTradeCommandIsExecuted_ThenNoUpdateOccurs()
    {
        // Given
        var state = NewState();
        var command = SampleData.ClearHoldTradeCommand;

        // When
        var result = command.Execute(state);

        // Then
        result.Should().BeFalse();
        state.Events.Should().BeEmpty();
    }

    /// <summary>
    /// Seeds the given state with a Ready trade state without invoking the Compute model's trading-day
    /// lookup, so hold-trade command tests can exercise Daily, Weekly, and Monthly periods uniformly.
    /// </summary>
    static void SeedReadyState(FuturesItiSignalCommandState state, TradeTimePeriodType timePeriod)
    {
        var entityId = SampleData.EntityIdFor(timePeriod);
        var seedEvent = new FuturesItiSignalGeneratedEvent
        {
            EntityId = entityId,
            FuturesItiSignal = SampleData.StartOfDayEvent.FuturesItiSignal! with
            {
                ContractId = entityId.ContractId,
                ValueDate = entityId.ValueDate,
                TimePeriod = timePeriod,
                TradeState = IntrinsicTimeTradeState.Ready
            },
            CreatedOn = SampleData.Timestamp,
            CreatedBy = "test"
        };
        state.Apply(seedEvent, addEvent: false);
    }
}
