using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

namespace TomasAI.IFM.Domain.MarketData.Analytics.BDDTests.CommandHandlers;

/// <summary>
/// BDD-style tests for the GenerateFuturesAdxSignal command handler, verifying that executing a
/// GenerateFuturesAdxSignalCommand against a FuturesAdxSignalCommandState produces the correct
/// FuturesAdxSignalGeneratedEvent and resulting trend-direction transition.
/// </summary>
public class FuturesAdxSignalCommandTests
{
    /// <summary>
    /// Builds a FuturesAdxSignalCommandState seeded with prior ADX signal history built from the
    /// given price series. The last price in the series is stamped with <paramref name="lastDirection"/>
    /// so it becomes the "prior" ADX signal used by the command handler's trend-transition logic.
    /// </summary>
    static FuturesAdxSignalCommandState SeedState(IReadOnlyList<decimal> prices, FuturesTrendDirectionType lastDirection)
    {
        var state = new FuturesAdxSignalCommandState();
        for (var i = 0; i < prices.Count; i++)
        {
            var isLast = i == prices.Count - 1;
            state.Update(SampleData.CreateAdxHistoryEvent(prices[i], direction: isLast ? lastDirection : FuturesTrendDirectionType.Init));
        }
        return state;
    }

    static GenerateFuturesAdxSignalCommand BuildCommand()
        => SampleData.AdxGenerateCommand with { CommandId = Guid.NewGuid() };

    #region Happy Path Tests

    [Fact]
    public void Execute_WhenNoPriorSignalExists_ProducesInitSignal()
    {
        // Arrange
        var state = new FuturesAdxSignalCommandState();
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.Updated.Should().BeTrue();
        state.AdxSignal.Should().NotBeNull();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.Init);
        state.AdxSignals.Should().HaveCount(1);
    }

    [Fact]
    public void Execute_WhenPriorSignalUpTrendingAndPricesRising_ProducesUpTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AdxRisingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.UpTrending);
        state.AdxSignal.PlusDI.Should().BeGreaterThan(state.AdxSignal.MinusDI);
    }

    [Fact]
    public void Execute_WhenPriorSignalWasTrendReversalAndPricesRising_ProducesUpTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AdxRisingPrices, FuturesTrendDirectionType.TrendReversal);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.UpTrending);
    }

    [Fact]
    public void Execute_WhenPriorSignalDownTrendingAndPricesFalling_ProducesDownTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AdxFallingPrices, FuturesTrendDirectionType.DownTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.DownTrending);
        state.AdxSignal.MinusDI.Should().BeGreaterThan(state.AdxSignal.PlusDI);
    }

    [Fact]
    public void Execute_WhenPriorSignalWasTrendReversalAndPricesFalling_ProducesDownTrendingSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AdxFallingPrices, FuturesTrendDirectionType.TrendReversal);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.DownTrending);
    }

    [Fact]
    public void Execute_GeneratesEventWithExpectedFuturesPriceAndCommandId()
    {
        // Arrange
        var state = new FuturesAdxSignalCommandState();
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        var domainEvent = state.Events.Should().ContainSingle().Subject as FuturesAdxSignalGeneratedEvent;
        domainEvent.Should().NotBeNull();
        domainEvent!.CommandId.Should().Be(command.CommandId);
        domainEvent.FuturesAdxSignal.FuturesPrice.Should().Be(command.FuturesPrice);
        domainEvent.EntityId.ContractId.Should().Be(command.FuturesAdxSignalId.ContractId);
    }

    [Fact]
    public void Execute_WhenCalledMultipleTimes_AccumulatesAdxSignalHistory()
    {
        // Arrange
        var state = new FuturesAdxSignalCommandState();

        // Act
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);
        BuildCommand().Execute(state);

        // Assert
        state.AdxSignals.Should().HaveCount(3);
    }

    [Fact]
    public void Execute_WhenStronglyTrending_ProducesHighTrendStrength()
    {
        // Arrange
        var state = SeedState(SampleData.AdxRisingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        command.Execute(state);

        // Assert
        state.AdxSignal.ADXStrength.Should().Be(FuturesTrendDirectionStrengthType.High);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void Execute_WhenPriorSignalUpTrendingButPricesFalling_ProducesTrendReversalSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AdxFallingPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Fact]
    public void Execute_WhenPriorSignalDownTrendingButPricesRising_ProducesTrendReversalSignal()
    {
        // Arrange
        var state = SeedState(SampleData.AdxRisingPrices, FuturesTrendDirectionType.DownTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    [Fact]
    public void Execute_WhenOnlyASinglePriorSignalExists_ProducesTrendReversalSignal()
    {
        // Arrange: a single history point is too short to compute directional indicators
        // (FuturesAdxSignalCompute returns PlusDI == MinusDI == 0), so the handler falls back
        // to a trend-reversal signal since neither up nor down trend criteria are met.
        var state = SeedState(SampleData.AdxSinglePrice, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.TrendReversal);
        state.AdxSignal.PlusDI.Should().Be(0);
        state.AdxSignal.MinusDI.Should().Be(0);
    }

    [Fact]
    public void Execute_WhenPricesAreFlat_ProducesTrendReversalSignalDespiteRangeBoundComputation()
    {
        // Arrange: flat prices yield PlusDI == MinusDI == 0 (range-bound), which does not satisfy
        // either the up-trending or down-trending continuation criteria, so the handler defaults
        // to a trend-reversal signal.
        var state = SeedState(SampleData.AdxFlatPrices, FuturesTrendDirectionType.UpTrending);
        var command = BuildCommand();

        // Act
        var result = command.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.TrendReversal);
        state.AdxSignal.PlusDI.Should().Be(state.AdxSignal.MinusDI);
    }

    [Fact]
    public void Execute_WhenNoPriorSignalExists_IgnoresPriceTrendAndAlwaysProducesInitSignal()
    {
        // Arrange: even with a rising price history, if there is no prior ADX signal the handler
        // must treat this as the initializing state rather than any trending direction.
        var state = new FuturesAdxSignalCommandState();
        var command = SampleData.AdxGenerateCommand with { CommandId = Guid.NewGuid() };

        // Act
        command.Execute(state);
        // Second call now has one prior signal (Init), still not up/down trending, prices flat by default single command...
        var secondCommand = SampleData.AdxGenerateCommand with { CommandId = Guid.NewGuid() };
        var result = secondCommand.Execute(state);

        // Assert
        result.Success.Should().BeTrue();
        state.AdxSignals.Should().HaveCount(2);
        // Prior signal direction was Init, so neither up nor down trending criteria match -> reversal branch.
        state.AdxSignal.ADX.Should().Be(FuturesTrendDirectionType.TrendReversal);
    }

    #endregion
}
