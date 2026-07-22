using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.CommandHandlers;

/// <summary>
/// BDD-style tests for the GenerateFuturesMacdSignal command handler, verifying that executing a
/// GenerateFuturesMacdSignalCommand against a FuturesMacdSignalCommandState produces the correct
/// FuturesMacdSignalGeneratedEvent and resulting trend-direction transition, across Daily, Weekly
/// and Monthly time periods.
/// </summary>
public class FuturesMacdSignalCommandTests
{
    /// <summary>
    /// Builds a FuturesMacdSignalCommandState seeded with prior MACD signal history built from the
    /// given price series. The last price in the series is stamped with <paramref name="lastDirection"/>
    /// so it becomes the "prior" MACD signal used by the command handler's trend-transition logic.
    /// </summary>
    static FuturesMacdSignalCommandState SeedState(IReadOnlyList<decimal> prices, TradeTimePeriodType timePeriod, FuturesTrendDirectionType lastDirection)
    {
        var state = new FuturesMacdSignalCommandState();
        for (var i = 0; i < prices.Count; i++)
        {
            var isLast = i == prices.Count - 1;
            state.Update(SampleData.CreateMacdHistoryEvent(prices[i], timePeriod, direction: isLast ? lastDirection : FuturesTrendDirectionType.Init));
        }
        return state;
    }

    static GenerateFuturesMacdSignalCommand BuildCommand(TradeTimePeriodType timePeriod = TradeTimePeriodType.Daily)
        => SampleData.MacdGenerateCommandFor(timePeriod) with { CommandId = Guid.NewGuid() };

    static readonly TradeTimePeriodType[] AllTimePeriods =
        [TradeTimePeriodType.Daily, TradeTimePeriodType.Weekly, TradeTimePeriodType.Monthly];

    #region Happy Path Tests

    [Fact]
    public void Execute_WhenNoPriorSignalExists_ProducesInitSignal()
    {
        // Arrange
        var state = new FuturesMacdSignalCommandState();
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.Updated.Should().BeTrue();
        state.MacdSignal.Should().NotBeNull();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.Init);
        state.MacdSignals.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorSignalUpTrendingAndPricesRising_ProducesUpTrendingSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.MacdRisingPrices, timePeriod, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.UpTrending);
        state.MacdSignal.MacdLine.Should().BeGreaterThan(state.MacdSignal.SignalLine);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorSignalWasTrendReversalAndPricesRising_ProducesUpTrendingSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.MacdRisingPrices, timePeriod, FuturesTrendDirectionType.TrendReversal);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.UpTrending);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorSignalDownTrendingAndPricesFalling_ProducesDownTrendingSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.MacdFallingPrices, timePeriod, FuturesTrendDirectionType.DownTrending);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.DownTrending);
        state.MacdSignal.SignalLine.Should().BeGreaterThan(state.MacdSignal.MacdLine);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorSignalWasTrendReversalAndPricesFalling_ProducesDownTrendingSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.MacdFallingPrices, timePeriod, FuturesTrendDirectionType.TrendReversal);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.DownTrending);
    }

    [Fact]
    public void Execute_GeneratesEventWithExpectedFuturesPriceAndCommandId()
    {
        // Arrange
        var state = new FuturesMacdSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesMacdSignalGeneratedEvent;
        domainEvent.Should().NotBeNull();
        domainEvent!.CommandId.Should().Be(command.CommandId);
        domainEvent.FuturesMacdSignal.FuturesPrice.Should().Be(command.FuturesPrice);
        domainEvent.EntityId.ContractId.Should().Be(command.FuturesMacdSignalId.ContractId);
    }

    [Fact]
    public void Execute_WhenCalledMultipleTimes_AccumulatesMacdSignalHistory()
    {
        // Arrange
        var state = new FuturesMacdSignalCommandState();

        // Act
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);

        // Assert
        state.MacdSignals.Should().HaveCount(3);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_EntityId_ReflectsRequestedTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = new FuturesMacdSignalCommandState();
        var command = BuildCommand(timePeriod);

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesMacdSignalGeneratedEvent;
        domainEvent!.EntityId.TimePeriod.Should().Be(timePeriod);
        state.MacdSignal.TimePeriod.Should().Be(timePeriod);
    }

    #endregion

    #region Edge Case Tests

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorSignalUpTrendingButPricesFalling_ProducesFlatSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange - the prior signal (UpTrending) conflicts with the newly computed DownTrending
        // direction, and since the prior signal is neither DownTrending nor TrendReversal, the
        // handler cannot classify it as down-trending either, so it falls back to Flat.
        var state = SeedState(SampleData.MacdFallingPrices, timePeriod, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.Flat);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorSignalDownTrendingButPricesRising_ProducesFlatSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange - the prior signal (DownTrending) conflicts with the newly computed UpTrending
        // direction, and since the prior signal is neither UpTrending nor TrendReversal, the
        // handler cannot classify it as up-trending either, so it falls back to Flat.
        var state = SeedState(SampleData.MacdRisingPrices, timePeriod, FuturesTrendDirectionType.DownTrending);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.Flat);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPricesAreFlat_ProducesFlatSignal(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.MacdFlatPrices, timePeriod, FuturesTrendDirectionType.Flat);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().Be(FuturesTrendDirectionType.Flat);
    }

    [Fact]
    public void Execute_WithSinglePriorPricePoint_DoesNotThrowAndProducesSignal()
    {
        // Arrange
        var state = SeedState(SampleData.MacdSinglePrice, TradeTimePeriodType.Daily, FuturesTrendDirectionType.Init);
        var command = BuildCommand();

        // Act
        var act = () => command.Execute(state);

        // Assert
        act.Should().NotThrow();
        state.MacdSignals.Should().HaveCount(2);
    }

    [Fact]
    public void Execute_WhenPriorSignalFlatAndPricesRising_ProducesUpTrendingOrFlatSignal()
    {
        // Arrange - prior "Flat" is neither UpTrending, DownTrending nor TrendReversal, so the
        // handler falls back to the raw computed trend direction (which can still flip to Flat
        // if the histogram/MACD-vs-signal comparison does not clear the trending thresholds).
        var state = SeedState(SampleData.MacdRisingPrices, TradeTimePeriodType.Daily, FuturesTrendDirectionType.Flat);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.MacdSignal.MACD.Should().BeOneOf(FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionType.Flat);
    }

    [Fact]
    public void Execute_ResultGuidResult_MatchesCommandId()
    {
        // Arrange
        var state = new FuturesMacdSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesMacdSignalGeneratedEvent;

        // Assert
        domainEvent!.CommandId.Should().Be(command.CommandId);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_AcrossAllTimePeriods_AlwaysMarksStateAsUpdated(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = new FuturesMacdSignalCommandState();
        var command = BuildCommand(timePeriod);

        // Act
        command.Execute(state);

        // Assert
        state.Updated.Should().BeTrue();
    }

    #endregion
}
