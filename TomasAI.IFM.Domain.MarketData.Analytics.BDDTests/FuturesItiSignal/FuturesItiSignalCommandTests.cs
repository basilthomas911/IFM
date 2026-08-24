using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.FuturesItiSignal;

public class FuturesItiSignalCommandTests
{
    // Every price-driven ITI transition is supported independently for all three live periods.
    public static readonly TheoryData<TimeFrameType> SupportedTimePeriods = new()
    {
        TimeFrameType.Daily,
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    };

    public static readonly TheoryData<TimeFrameType> AllTimePeriods = new()
    {
        TimeFrameType.Daily,
        TimeFrameType.Weekly,
        TimeFrameType.Monthly
    };

    static FuturesItiSignalCommandState NewState() => new();

    /// <summary>
    /// Extracts the resulting read model from the most recently applied event on the state, since the
    /// compute model's internal accessors are not visible outside the domain assembly.
    /// </summary>
    static FuturesItiSignalV2ReadModel LastSignal(FuturesItiSignalCommandState state)
        => ((FuturesItiSignalGeneratedEvent)state.Events[^1]).FuturesItiSignal!;

    static FuturesItiSignalCommandState GivenStartOfDayState(TimeFrameType timePeriod, double? futuresPrice = null)
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
        TimeFrameType timePeriod)
    {
        // Given
        var state = NewState();
        var command = SampleData.GenerateCommandFor(timePeriod);

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        state.Updated.Should().BeTrue();
        state.Events.Should().ContainSingle();
        var signal = LastSignal(state);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.IntrinsicTimeGroupId.Should().Be(0);
        signal.TimeFrameStartValueDate.Should().Be(command.TimeFrameStartValueDate);
        signal.TradingDays.Should().Be(ExpectedTradingDays(timePeriod));
        signal.BandPercentage.Should().Be(0.10);
        signal.BandSize.Should().Be(signal.Threshold * signal.BandPercentage);
        signal.BandLevel.Should().Be(0);
        signal.ReversalLevel.Should().Be(0);
        signal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
        var generated = state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<FuturesItiSignalGeneratedEvent>().Which;
        generated.EntityId.Should().Be(command.EntityId);
        generated.DeriveLongerPeriods.Should().BeFalse();
    }

    // ───── GenerateFuturesItiSignalCommand — Up-trend transitions (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceFallsBelowDownTrendTrigger_ThenTrendDirectionChangesToDownTrend(
        TimeFrameType timePeriod)
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
        result.Success.Should().BeTrue();
        state.Events.Should().HaveCount(2);
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
        signal.IntrinsicTimeGroupId.Should().Be(previous.IntrinsicTimeGroupId + 1);
        signal.BandLevel.Should().Be(0);
        signal.ReversalLevel.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceExceedsTrendExtreme_ThenTrendExtremeIsUpdated(
        TimeFrameType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var higherPrice = LastSignal(state).TrendExtreme + 50;
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = higherPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        signal.IntrinsicTimeGroupId.Should().Be(0);
        signal.TrendExtreme.Should().Be(higherPrice);
        AssertCalculatedLevels(signal);
        signal.BandLevel.Should().BeGreaterThan(0);
        signal.ReversalLevel.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceFallsBelowTrendReversal_ButNotBelowDownTrendTrigger_ThenTrendReversalIsUpdated(
        TimeFrameType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        // Move the trend extreme up first so the reversal level sits above the down-trend trigger.
        var extremeCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).TrendExtreme + 100 };
        extremeCommand.Execute(state);

        var afterExtreme = LastSignal(state);
        var reversalPrice = afterExtreme.TrendReversal - afterExtreme.BandSize - 0.01;
        reversalPrice.Should().BeGreaterThan(afterExtreme.DownTrendTrigger);
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = reversalPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeGroupId.Should().Be(0);
        signal.TrendReversal.Should().Be(reversalPrice);
        AssertCalculatedLevels(signal);
        signal.ReversalLevel.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingUpTrendSignal_WhenPriceStaysInsideBand_ThenNoDurableSignalIsApplied(
        TimeFrameType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var startOfDay = LastSignal(state);
        var eventCount = state.Events.Count;
        var withinRangePrice = startOfDay.BandAnchorPrice + startOfDay.BandSize / 2;
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = withinRangePrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        state.Events.Should().HaveCount(eventCount);
        LastSignal(state).Should().BeSameAs(startOfDay);
    }

    // ───── GenerateFuturesItiSignalCommand — Down-trend transitions (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingDownTrendSignal_WhenPriceExceedsUpTrendTrigger_ThenTrendDirectionChangesToUpTrend(
        TimeFrameType timePeriod)
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
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeGroupId.Should().Be(downTrendSignal.IntrinsicTimeGroupId + 1);
        signal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
        signal.BandLevel.Should().Be(0);
        signal.ReversalLevel.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingDownTrendSignal_WhenPriceFallsBelowTrendExtreme_ThenTrendExtremeIsUpdated(
        TimeFrameType timePeriod)
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
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        signal.IntrinsicTimeGroupId.Should().Be(downTrendSignal.IntrinsicTimeGroupId);
        signal.TrendExtreme.Should().Be(lowerPrice);
        AssertCalculatedLevels(signal);
        signal.BandLevel.Should().BeGreaterThan(0);
        signal.ReversalLevel.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(SupportedTimePeriods))]
    public void GivenExistingDownTrendSignal_WhenPriceExceedsTrendReversal_ButNotAboveUpTrendTrigger_ThenTrendReversalIsUpdated(
        TimeFrameType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var downTrendCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).DownTrendTrigger - 10 };
        downTrendCommand.Execute(state);
        LastSignal(state).IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);

        var extremeCommand = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = LastSignal(state).TrendExtreme - 100 };
        extremeCommand.Execute(state);

        var afterExtreme = LastSignal(state);
        var reversalPrice = afterExtreme.TrendReversal + afterExtreme.BandSize + 0.01;
        reversalPrice.Should().BeLessThan(afterExtreme.UpTrendTrigger);
        var command = SampleData.GenerateCommandFor(timePeriod) with { FuturesPrice = reversalPrice };

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
        signal.IntrinsicTimeGroupId.Should().Be(afterExtreme.IntrinsicTimeGroupId);
        signal.TrendReversal.Should().Be(reversalPrice);
        AssertCalculatedLevels(signal);
        signal.ReversalLevel.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenAnActiveTimeFrame_WhenPricesTraverseBothDirections_ThenEveryPriceChangeModeCompletesInOrder(
        TimeFrameType timePeriod)
    {
        // Given
        var state = NewState();
        var timestamp = SampleData.Timestamp;

        FuturesItiSignalV2ReadModel GenerateAt(double price)
        {
            var priorEventCount = state.Events.Count;
            var command = SampleData.GenerateCommandFor(timePeriod) with
            {
                Timestamp = timestamp = timestamp.AddSeconds(1),
                FuturesPrice = price,
                VixFuturesPrice = 20
            };

            var result = command.Execute(state);

            result.Success.Should().BeTrue();
            state.Events.Should().HaveCount(priorEventCount + 1);
            var generated = state.Events[^1].Should()
                .BeOfType<FuturesItiSignalGeneratedEvent>().Which;
            generated.EntityId.Should().Be(command.EntityId);
            generated.FuturesItiSignal.Should().NotBeNull();
            generated.DeriveLongerPeriods.Should().BeFalse();
            return generated.FuturesItiSignal!;
        }

        // When: start, extend, pull back, move within the structure, change
        // direction, and repeat the same structural cycle in the down trend.
        var started = GenerateAt((double)SampleData.FuturesPrice);

        var eventCountBeforeInsideBand = state.Events.Count;
        var insideBand = SampleData.GenerateCommandFor(timePeriod) with
        {
            Timestamp = timestamp = timestamp.AddSeconds(1),
            FuturesPrice = started.BandAnchorPrice + started.BandSize / 2,
            VixFuturesPrice = 20
        };
        insideBand.Execute(state).Success.Should().BeTrue();
        state.Events.Should().HaveCount(eventCountBeforeInsideBand);

        var upExtreme = GenerateAt(started.TrendExtreme + started.BandSize * 1.01);
        upExtreme.BandLevel.Should().BeGreaterThanOrEqualTo(started.BandPercentage);
        var upThresholdExtension = GenerateAt(started.TrendPrice + started.Threshold * 1.25);
        upThresholdExtension.BandLevel.Should().BeGreaterThan(1);
        upThresholdExtension.ReversalLevel.Should().Be(0);
        var upReversal = GenerateAt(upThresholdExtension.TrendReversal - upThresholdExtension.BandSize * 1.01);
        upReversal.IntrinsicPrice.Should().BeGreaterThan(upReversal.DownTrendTrigger);
        upReversal.ReversalLevel.Should().BeGreaterThan(0);
        var upTrending = GenerateAt(upReversal.BandAnchorPrice + upReversal.BandSize * 1.01);
        upTrending.ReversalLevel.Should().BeLessThan(upReversal.ReversalLevel);

        var downDirection = GenerateAt(upTrending.DownTrendTrigger);
        var downExtreme = GenerateAt(downDirection.TrendExtreme - downDirection.BandSize * 1.01);
        downExtreme.BandLevel.Should().BeGreaterThanOrEqualTo(downDirection.BandPercentage);
        var downThresholdExtension = GenerateAt(
            downDirection.TrendPrice - downDirection.Threshold * 1.25);
        downThresholdExtension.BandLevel.Should().BeGreaterThan(1);
        downThresholdExtension.ReversalLevel.Should().Be(0);
        var downReversal = GenerateAt(
            downThresholdExtension.TrendReversal + downThresholdExtension.BandSize * 1.01);
        downReversal.IntrinsicPrice.Should().BeLessThan(downReversal.UpTrendTrigger);
        downReversal.ReversalLevel.Should().BeGreaterThan(0);
        var downTrending = GenerateAt(downReversal.BandAnchorPrice - downReversal.BandSize * 1.01);
        downTrending.ReversalLevel.Should().BeLessThan(downReversal.ReversalLevel);

        _ = GenerateAt(downTrending.UpTrendTrigger);

        var holdCommand = new SetFuturesItiSignalHoldTradeCommand(
            SampleData.ContractId,
            SampleData.ValueDate,
            timePeriod,
            timestamp = timestamp.AddSeconds(1));
        holdCommand.Execute(state).Success.Should().BeTrue();
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Hold);

        var clearCommand = new ClearFuturesItiSignalHoldTradeCommand(
            SampleData.ContractId,
            SampleData.ValueDate,
            timePeriod,
            timestamp.AddSeconds(1));
        clearCommand.Execute(state).Success.Should().BeTrue();
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Ready);

        // Then
        var signals = state.Events
            .Cast<FuturesItiSignalGeneratedEvent>()
            .Select(@event => @event.FuturesItiSignal!)
            .ToArray();

        signals.Select(signal => signal.IntrinsicTimeMode).Should().Equal(
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.Trending,
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendExtremeChanged,
            IntrinsicTimeModeType.TrendReversalChanged,
            IntrinsicTimeModeType.Trending,
            IntrinsicTimeModeType.TrendDirectionChanged,
            IntrinsicTimeModeType.HoldTradeChanged,
            IntrinsicTimeModeType.HoldTradeChanged);
        signals.Select(signal => signal.IntrinsicTimeTrend).Should().Equal(
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.DownTrend,
            IntrinsicTimeTrendType.DownTrend,
            IntrinsicTimeTrendType.DownTrend,
            IntrinsicTimeTrendType.DownTrend,
            IntrinsicTimeTrendType.DownTrend,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeTrendType.UpTrend);
        signals.Select(signal => signal.IntrinsicTimeGroupId).Should().Equal(
            0, 0, 0, 0, 0,
            1, 1, 1, 1, 1,
            2, 2, 2);
        signals.Should().OnlyContain(signal =>
            signal.TimePeriod == timePeriod
            && signal.TimeFrameStartValueDate == SampleData.ValueDate
            && signal.BandPercentage == 0.10
            && signal.BandSize > 0
            && signal.Threshold > 0);
        foreach (var signal in signals)
            AssertCalculatedLevels(signal);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenAnExistingTimeFrame_WhenTheNextCalendarFrameStarts_ThenAGroupZeroStartSignalIsGenerated(
        TimeFrameType timePeriod)
    {
        // Given
        var state = GivenStartOfDayState(timePeriod);
        var nextFrameStart = timePeriod switch
        {
            TimeFrameType.Daily => SampleData.ValueDate.AddDays(1),
            TimeFrameType.Weekly => SampleData.ValueDate.AddDays(7),
            TimeFrameType.Monthly => SampleData.ValueDate.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(timePeriod))
        };
        var command = new GenerateFuturesItiSignalCommand(
            SampleData.ContractId,
            nextFrameStart,
            timePeriod,
            SampleData.Timestamp.AddDays(31),
            (double)SampleData.FuturesPrice,
            20,
            nextFrameStart);

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        signal.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        signal.IntrinsicTimeGroupId.Should().Be(0);
        signal.ValueDate.Should().Be(nextFrameStart);
        signal.TimeFrameStartValueDate.Should().Be(nextFrameStart);
    }

    // ───── SetFuturesItiSignalHoldTradeCommand (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalInReadyState_WhenSetFuturesItiSignalHoldTradeCommandIsExecuted_ThenTradeStateBecomesHold(
        TimeFrameType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        var before = LastSignal(state);
        var command = SampleData.SetHoldTradeCommandFor(timePeriod);

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.HoldTradeChanged);
        AssertAnalyticalStatePreserved(before, signal);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalAlreadyOnHold_WhenSetFuturesItiSignalHoldTradeCommandIsExecuted_ThenNoUpdateOccurs(
        TimeFrameType timePeriod)
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
        result.Success.Should().BeFalse();
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
        result.Success.Should().BeFalse();
        state.Events.Should().BeEmpty();
    }

    // ───── ClearFuturesItiSignalHoldTradeCommand (Given / When / Then) ─────

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalOnHold_WhenClearFuturesItiSignalHoldTradeCommandIsExecuted_ThenTradeStateBecomesReady(
        TimeFrameType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        SampleData.SetHoldTradeCommandFor(timePeriod).Execute(state);
        var before = LastSignal(state);
        before.TradeState.Should().Be(IntrinsicTimeTradeState.Hold);
        var command = SampleData.ClearHoldTradeCommandFor(timePeriod);

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeTrue();
        var signal = LastSignal(state);
        signal.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
        signal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.HoldTradeChanged);
        AssertAnalyticalStatePreserved(before, signal);
    }

    [Theory]
    [MemberData(nameof(AllTimePeriods))]
    public void GivenSignalAlreadyReady_WhenClearFuturesItiSignalHoldTradeCommandIsExecuted_ThenNoUpdateOccurs(
        TimeFrameType timePeriod)
    {
        // Given
        var state = NewState();
        SeedReadyState(state, timePeriod);
        var command = SampleData.ClearHoldTradeCommandFor(timePeriod);
        var eventCountBefore = state.Events.Count;

        // When
        var result = command.Execute(state);

        // Then
        result.Success.Should().BeFalse();
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
        result.Success.Should().BeFalse();
        state.Events.Should().BeEmpty();
    }

    /// <summary>
    /// Seeds the given state through the production generation path so hold-trade tests verify that
    /// Daily, Weekly, and Monthly analytical state survives subsequent mutations.
    /// </summary>
    static void SeedReadyState(FuturesItiSignalCommandState state, TimeFrameType timePeriod)
    {
        var result = SampleData.GenerateCommandFor(timePeriod).Execute(state);

        result.Success.Should().BeTrue();
        LastSignal(state).TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
    }

    /// <summary>
    /// Verifies that a hold-state mutation changes only mutation-specific fields and retains the
    /// complete generated signal used by the next price update.
    /// </summary>
    static void AssertAnalyticalStatePreserved(
        FuturesItiSignalV2ReadModel before,
        FuturesItiSignalV2ReadModel after)
    {
        after.ContractId.Should().Be(before.ContractId);
        after.ValueDate.Should().Be(before.ValueDate);
        after.TimePeriod.Should().Be(before.TimePeriod);
        after.IntrinsicTimeGroupId.Should().Be(before.IntrinsicTimeGroupId);
        after.IntrinsicPrice.Should().Be(before.IntrinsicPrice);
        after.IntrinsicTimeTrend.Should().Be(before.IntrinsicTimeTrend);
        after.TrendPrice.Should().Be(before.TrendPrice);
        after.TrendExtreme.Should().Be(before.TrendExtreme);
        after.TrendReversal.Should().Be(before.TrendReversal);
        after.TrendDelta.Should().Be(before.TrendDelta);
        after.TargetDelta.Should().Be(before.TargetDelta);
        after.Lambda.Should().Be(before.Lambda);
        after.TradingDays.Should().Be(before.TradingDays);
        after.Threshold.Should().Be(before.Threshold);
        after.UpTrendTrigger.Should().Be(before.UpTrendTrigger);
        after.DownTrendTrigger.Should().Be(before.DownTrendTrigger);
        after.TimeFrameStartValueDate.Should().Be(before.TimeFrameStartValueDate);
        after.BandAnchorPrice.Should().Be(before.BandAnchorPrice);
        after.BandPercentage.Should().Be(before.BandPercentage);
        after.BandSize.Should().Be(before.BandSize);
        after.BandLevel.Should().Be(before.BandLevel);
        after.ReversalLevel.Should().Be(before.ReversalLevel);
    }

    /// <summary>
    /// Verifies both strategy levels against the values persisted on a generated signal.
    /// </summary>
    static void AssertCalculatedLevels(FuturesItiSignalV2ReadModel signal)
    {
        var directionalMovement = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            ? signal.IntrinsicPrice - signal.TrendPrice
            : signal.TrendPrice - signal.IntrinsicPrice;
        var expectedBandLevel = signal.Threshold <= 0
            ? 0
            : directionalMovement / signal.Threshold;
        signal.BandLevel.Should().BeApproximately(expectedBandLevel, 1e-10);

        var trendExcursion = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            ? signal.TrendExtreme - signal.TrendPrice
            : signal.TrendPrice - signal.TrendExtreme;
        var retracement = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            ? signal.TrendExtreme - signal.IntrinsicPrice
            : signal.IntrinsicPrice - signal.TrendExtreme;
        var expectedReversalLevel = trendExcursion <= 0
            ? 0
            : Math.Max(0, retracement / trendExcursion);
        signal.ReversalLevel.Should().BeApproximately(expectedReversalLevel, 1e-10);
    }

    static int ExpectedTradingDays(TimeFrameType timePeriod)
        => timePeriod switch
        {
            TimeFrameType.Daily => 1,
            TimeFrameType.Weekly => 5,
            TimeFrameType.Monthly => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(timePeriod))
        };
}
