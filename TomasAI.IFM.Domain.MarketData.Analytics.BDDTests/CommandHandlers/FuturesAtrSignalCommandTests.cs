using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.CommandHandlers;

/// <summary>
/// BDD-style tests for the GenerateFuturesAtrSignal and GenerateFuturesAtrDailySignal command
/// handlers, verifying that executing either command against a FuturesAtrSignalCommandState
/// produces the correct generated event and resulting trend-direction transition.
/// </summary>
/// <remarks>
/// FuturesAtrSignalCommandState.AtrSignal/AtrSignals are internal (and not visible to this
/// assembly), so assertions here read the latest generated signal back off the public
/// state.Events collection instead.
/// </remarks>
public class FuturesAtrSignalCommandTests
{
    static FuturesAtrSignalReadModel LastAtrSignal(FuturesAtrSignalCommandState state)
        => state.Events.OfType<FuturesAtrSignalGeneratedEvent>().Last().FuturesAtrSignal;

    static FuturesAtrSignalReadModel LastAtrDailySignal(FuturesAtrSignalCommandState state)
        => state.Events.OfType<FuturesAtrDailySignalGeneratedEvent>().Last().FuturesAtrSignal;

    static FuturesAtrSignalCommandState SeedState(IReadOnlyList<decimal> prices, FuturesTrendDirectionType lastDirection)
    {
        var state = new FuturesAtrSignalCommandState();
        for (var i = 0; i < prices.Count; i++)
        {
            var isLast = i == prices.Count - 1;
            state.Update(SampleData.CreateAtrHistoryEvent(prices[i], direction: isLast ? lastDirection : FuturesTrendDirectionType.Init));
        }
        return state;
    }

    static FuturesAtrSignalCommandState SeedDailyState(IReadOnlyList<decimal> prices, FuturesTrendDirectionType lastDirection)
    {
        var state = new FuturesAtrSignalCommandState();
        for (var i = 0; i < prices.Count; i++)
        {
            var isLast = i == prices.Count - 1;
            state.Update(SampleData.CreateAtrDailyHistoryEvent(prices[i], direction: isLast ? lastDirection : FuturesTrendDirectionType.Init));
        }
        return state;
    }

    static GenerateFuturesAtrSignalCommand BuildCommand()
        => SampleData.AtrGenerateCommand with { CommandId = Guid.NewGuid() };

    static GenerateFuturesAtrDailySignalCommand BuildDailyCommand()
        => SampleData.AtrGenerateDailyCommand with { CommandId = Guid.NewGuid() };

    #region GenerateFuturesAtrSignalCommand - Happy Path Tests

    [Fact]
    public void Execute_WhenNoPriorSignalExists_ProducesInitSignal()
    {
        // Arrange
        var state = new FuturesAtrSignalCommandState();
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.Events.Should().ContainSingle();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.Init);
    }

    [Fact]
    public void Execute_WhenPriorSignalUpTrendingAndVolatilityRising_ProducesUpTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AtrRisingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.UpTrending);
        LastAtrSignal(state).TrueRange.Should().BeGreaterThan(LastAtrSignal(state).AtrValue);
    }

    [Fact]
    public void Execute_WhenPriorSignalWasTrendReversalAndVolatilityRising_ProducesUpTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AtrRisingPrices, FuturesTrendDirectionType.TrendReversal);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.UpTrending);
    }

    [Fact]
    public void Execute_WhenPriorSignalDownTrendingAndVolatilityFalling_ProducesDownTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AtrFallingPrices, FuturesTrendDirectionType.DownTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.DownTrending);
        LastAtrSignal(state).AtrValue.Should().BeGreaterThan(LastAtrSignal(state).TrueRange);
    }

    [Fact]
    public void Execute_WhenPriorSignalWasTrendReversalAndVolatilityFalling_ProducesDownTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AtrFallingPrices, FuturesTrendDirectionType.TrendReversal);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.DownTrending);
    }

    [Fact]
    public void Execute_GeneratesEventWithExpectedFuturesPriceAndCommandId()
    {
        // Arrange
        var state = new FuturesAtrSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesAtrSignalGeneratedEvent;
        domainEvent.Should().NotBeNull();
        domainEvent!.CommandId.Should().Be(command.CommandId);
        domainEvent.FuturesAtrSignal.FuturesPrice.Should().Be(command.FuturesPrice);
        domainEvent.EntityId.ContractId.Should().Be(command.FuturesAtrSignalId.ContractId);
    }

    [Fact]
    public void Execute_WhenCalledMultipleTimes_AccumulatesAtrSignalHistory()
    {
        // Arrange
        var state = new FuturesAtrSignalCommandState();

        // Act
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);

        // Assert
        state.Events.OfType<FuturesAtrSignalGeneratedEvent>().Should().HaveCount(3);
    }

    [Fact]
    public void Execute_WhenStronglyTrending_ProducesHighTrendStrength()
    {
        // Arrange
        var state = SeedState(SampleData.AtrRisingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        LastAtrSignal(state).ATRStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
    }

    #endregion

    #region GenerateFuturesAtrSignalCommand - Edge Case Tests

    [Fact]
    public void Execute_WhenPriorSignalUpTrendingButVolatilityFalling_ProducesTrendReversalSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AtrFallingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Fact]
    public void Execute_WhenPriorSignalDownTrendingButVolatilityRising_ProducesTrendReversalSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AtrRisingPrices, FuturesTrendDirectionType.DownTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Fact]
    public void Execute_WhenOnlyASinglePriorSignalExists_ProducesTrendReversalSignal()
    {
        // Arrange: a single history point is too short to compute a True Range / ATR
        // (FuturesAtrSignalCompute returns TrueRange == AtrValue == 0), so the handler falls back
        // to a trend-reversal signal since neither up nor down trend criteria are met.
        var state = SeedState(SampleData.AtrSinglePrice, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
        LastAtrSignal(state).TrueRange.Should().Be(0);
        LastAtrSignal(state).AtrValue.Should().Be(0);
    }

    [Fact]
    public void Execute_WhenPricesAreFlat_ProducesTrendReversalSignalDespiteRangeBoundComputation()
    {
        // Arrange: flat prices yield TrueRange == AtrValue == 0 (range-bound), which does not
        // satisfy either the up-trending or down-trending continuation criteria, so the handler
        // defaults to a trend-reversal signal.
        var state = SeedState(SampleData.AtrFlatPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
        LastAtrSignal(state).AtrValue.Should().Be(LastAtrSignal(state).TrueRange);
    }

    [Fact]
    public void Execute_WhenNoPriorSignalExists_IgnoresPriceTrendAndAlwaysProducesInitSignal()
    {
        // Arrange: even after a first signal, if that prior signal was itself just initializing
        // (Init), the handler must not treat the second call as up/down trending.
        var state = new FuturesAtrSignalCommandState();
        var command = SampleData.AtrGenerateCommand with { CommandId = Guid.NewGuid() };

        // Act
        command.Execute(state);
        var secondCommand = SampleData.AtrGenerateCommand with { CommandId = Guid.NewGuid() };
        var result = secondCommand.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.Events.OfType<FuturesAtrSignalGeneratedEvent>().Should().HaveCount(2);
        // Prior signal direction was Init, so neither up nor down trending criteria match -> reversal branch.
        LastAtrSignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    #endregion

    #region GenerateFuturesAtrDailySignalCommand - Happy Path Tests

    [Fact]
    public void Execute_Daily_WhenNoPriorSignalExists_ProducesInitSignal()
    {
        // Arrange
        var state = new FuturesAtrSignalCommandState();
        var command = BuildDailyCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.Events.Should().ContainSingle();
        LastAtrDailySignal(state).ATR.Should().Be(FuturesTrendDirectionType.Init);
    }

    [Fact]
    public void Execute_Daily_WhenPriorSignalUpTrendingAndVolatilityRising_ProducesUpTrendingSignal()
    {
        // Arrange
        var state = SeedDailyState(SampleData.AtrRisingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildDailyCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrDailySignal(state).ATR.Should().Be(FuturesTrendDirectionType.UpTrending);
    }

    [Fact]
    public void Execute_Daily_WhenPriorSignalDownTrendingAndVolatilityFalling_ProducesDownTrendingSignal()
    {
        // Arrange
        var state = SeedDailyState(SampleData.AtrFallingPrices, FuturesTrendDirectionType.DownTrending);
        var command = BuildDailyCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrDailySignal(state).ATR.Should().Be(FuturesTrendDirectionType.DownTrending);
    }

    [Fact]
    public void Execute_Daily_GeneratesEventWithExpectedFuturesPriceAndCommandId()
    {
        // Arrange
        var state = new FuturesAtrSignalCommandState();
        var command = BuildDailyCommand();

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesAtrDailySignalGeneratedEvent;
        domainEvent.Should().NotBeNull();
        domainEvent!.CommandId.Should().Be(command.CommandId);
        domainEvent.FuturesAtrSignal.FuturesPrice.Should().Be(command.FuturesPrice);
        domainEvent.EntityId.ContractId.Should().Be(command.FuturesAtrSignalId.ContractId);
    }

    #endregion

    #region GenerateFuturesAtrDailySignalCommand - Edge Case Tests

    [Fact]
    public void Execute_Daily_WhenOnlyASinglePriorSignalExists_ProducesTrendReversalSignal()
    {
        // Arrange: a single history point is too short to compute a True Range / ATR, so the
        // handler falls back to a trend-reversal signal.
        var state = SeedDailyState(SampleData.AtrSinglePrice, FuturesTrendDirectionType.UpTrending);
        var command = BuildDailyCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrDailySignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
        LastAtrDailySignal(state).TrueRange.Should().Be(0);
        LastAtrDailySignal(state).AtrValue.Should().Be(0);
    }

    [Fact]
    public void Execute_Daily_WhenPriorSignalUpTrendingButVolatilityFalling_ProducesTrendReversalSignal()
    {
        // Arrange
        var state = SeedDailyState(SampleData.AtrFallingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildDailyCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        LastAtrDailySignal(state).ATR.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    #endregion
}
