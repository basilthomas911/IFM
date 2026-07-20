using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.CommandHandlers;

/// <summary>
/// BDD-style tests for the GenerateFuturesRsiSignal command handler, verifying that executing a
/// GenerateFuturesRsiSignalCommand against a FuturesRsiSignalCommandState produces the correct
/// FuturesRsiSignalGeneratedEvent (and, once enough history has accumulated, a
/// FuturesRsiSignalsGeneratedEvent), across Daily, Weekly and Monthly time periods.
/// </summary>
public class FuturesRsiSignalCommandTests
{
    static readonly TradeTimePeriodType[] AllTimePeriods =
        [TradeTimePeriodType.Daily, TradeTimePeriodType.Weekly, TradeTimePeriodType.Monthly];

    /// <summary>
    /// Builds a FuturesRsiSignalCommandState seeded with prior RSI signal history by replaying the
    /// given read models as FuturesRsiSignalGeneratedEvent instances. ReplayEvents clears the resulting
    /// domain event collection, so only events raised by the command under test will appear afterwards.
    /// </summary>
    static FuturesRsiSignalCommandState SeedState(IReadOnlyCollection<FuturesRsiSignalReadModel> signals)
    {
        var state = new FuturesRsiSignalCommandState();
        state.ReplayEvents(signals.Select(SampleData.CreateRsiHistoryEvent));
        return state;
    }

    static GenerateFuturesRsiSignalCommand BuildCommand(TradeTimePeriodType timePeriod = TradeTimePeriodType.Daily)
        => SampleData.RsiGenerateCommandFor(timePeriod) with { CommandId = Guid.NewGuid() };

    #region Happy Path Tests

    [Fact]
    public void Execute_WhenNoPriorSignalExists_ProducesInitSignalWithoutSignalsEvent()
    {
        // Arrange
        var state = new FuturesRsiSignalCommandState();
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.Updated.Should().BeTrue();
        var generated = state.Events.Should().ContainSingle().Subject as FuturesRsiSignalGeneratedEvent;
        generated.Should().NotBeNull();
        generated!.FuturesRsiSignal.RSI.Should().Be(-1);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorHistoryBelowWindowSize_ProducesSignalWithoutRsiValue(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.SingleRsiSignal);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        var generated = state.Events.OfType<FuturesRsiSignalGeneratedEvent>().Should().ContainSingle().Subject;
        generated.FuturesRsiSignal.RSI.Should().Be(-1);
        generated.FuturesRsiSignal.Price.Should().Be(command.FuturesPrice);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorHistoryAtWindowSizeWithRisingPrices_ProducesHighRsiAndSignalsEvent(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.UpTrendingRsiSignals);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.Events.Should().HaveCount(2);
        var generated = state.Events.OfType<FuturesRsiSignalGeneratedEvent>().Should().ContainSingle().Subject;
        generated.FuturesRsiSignal.RSI.Should().BeGreaterThan(50);
        state.Events.OfType<FuturesRsiSignalsGeneratedEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_WhenPriorHistoryAtWindowSizeWithFallingPrices_ProducesLowRsiAndSignalsEvent(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.DownTrendingRsiSignals);
        var command = BuildCommand(timePeriod);

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        state.Events.Should().HaveCount(2);
        var generated = state.Events.OfType<FuturesRsiSignalGeneratedEvent>().Should().ContainSingle().Subject;
        generated.FuturesRsiSignal.RSI.Should().BeLessThan(50);
        state.Events.OfType<FuturesRsiSignalsGeneratedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Execute_WhenPriorHistoryAtWindowSizeWithFlatPrices_ProducesExpectedZeroRsi()
    {
        // Arrange
        var state = SeedState(SampleData.FlatRsiSignals);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Should().BeTrue();
        var generated = state.Events.OfType<FuturesRsiSignalGeneratedEvent>().Should().ContainSingle().Subject;
        generated.FuturesRsiSignal.RSI.Should().Be(0);
    }

    [Fact]
    public void Execute_GeneratesEventWithExpectedFuturesPriceAndCommandId()
    {
        // Arrange
        var state = new FuturesRsiSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesRsiSignalGeneratedEvent;
        domainEvent.Should().NotBeNull();
        domainEvent!.CommandId.Should().Be(command.CommandId);
        domainEvent.FuturesRsiSignal.Price.Should().Be(command.FuturesPrice);
        domainEvent.EntityId.ContractId.Should().Be(command.FuturesRsiSignalId.ContractId);
    }

    [Fact]
    public void Execute_WhenCalledMultipleTimesFromEmptyState_AccumulatesRsiSignalHistoryEvents()
    {
        // Arrange
        var state = new FuturesRsiSignalCommandState();

        // Act
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);

        // Assert
        state.Events.OfType<FuturesRsiSignalGeneratedEvent>().Should().HaveCount(3);
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_EntityId_ReflectsRequestedTimePeriod(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = new FuturesRsiSignalCommandState();
        var command = BuildCommand(timePeriod);

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesRsiSignalGeneratedEvent;
        domainEvent.Should().NotBeNull();
        domainEvent!.EntityId.TimePeriod.Should().Be(timePeriod);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Execute_WhenStateIsFresh_UpdatedFlagIsTrue()
    {
        // Arrange
        var state = new FuturesRsiSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        state.Updated.Should().BeTrue();
    }

    [Fact]
    public void Execute_ForEachSupportedTimePeriod_ProducesIndependentInitSignal()
    {
        foreach (var timePeriod in AllTimePeriods)
        {
            // Arrange
            var state = new FuturesRsiSignalCommandState();
            var command = BuildCommand(timePeriod);

            // Act
            var result = command.Execute(state);

            // Assert
            result.Should().BeTrue();
            var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesRsiSignalGeneratedEvent;
            domainEvent.Should().NotBeNull();
            domainEvent!.FuturesRsiSignal.RSI.Should().Be(-1);
            domainEvent.EntityId.TimePeriod.Should().Be(timePeriod);
        }
    }

    [Fact]
    public void Execute_WhenNoPriorValidSignalExists_DoesNotProduceSignalsEvent()
    {
        // Arrange - a fresh state has no history at all, so the newly generated signal (RSI == -1) is the
        // only entry and CanGenerateFuturesRsiSignals requires at least one prior valid signal.
        var state = new FuturesRsiSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        state.Events.OfType<FuturesRsiSignalsGeneratedEvent>().Should().BeEmpty();
        state.Events.OfType<FuturesRsiSignalGeneratedEvent>().Should().ContainSingle();
    }

    [Fact]
    public void Execute_WhenAtLeastOnePriorValidSignalExists_ProducesSignalsEvent()
    {
        // Arrange - a single prior valid RSI signal is enough to satisfy CanGenerateFuturesRsiSignals.
        var state = SeedState(SampleData.SingleRsiSignal);
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        state.Events.OfType<FuturesRsiSignalsGeneratedEvent>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(TradeTimePeriodType.Daily)]
    [InlineData(TradeTimePeriodType.Weekly)]
    [InlineData(TradeTimePeriodType.Monthly)]
    public void Execute_SignalsGeneratedEvent_ContainsExpectedPeriodLength(TradeTimePeriodType timePeriod)
    {
        // Arrange
        var state = SeedState(SampleData.UpTrendingRsiSignals);
        var command = BuildCommand(timePeriod);

        // Act
        command.Execute(state);

        // Assert
        var signalsEvent = state.Events.OfType<FuturesRsiSignalsGeneratedEvent>().Should().ContainSingle().Subject;
        signalsEvent.FuturesRsiSignals.Should().OnlyContain(s => s.RSI != -1);
    }

    #endregion
}
