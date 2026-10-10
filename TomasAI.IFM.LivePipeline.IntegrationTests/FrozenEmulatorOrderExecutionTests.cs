using TomasAI.IFM.Application.Api.Server.Core.Trading.Emulation;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class FrozenEmulatorOrderExecutionTests
{
    [Theory]
    [InlineData(FuturesMarketState.Closed, true)]
    [InlineData(FuturesMarketState.LiveTrading, false)]
    public async Task Frozen_quotes_match_only_when_market_is_closed(FuturesMarketState state, bool shouldFill)
    {
        var now = DateTimeOffset.UtcNow;
        var sessions = Substitute.For<IFuturesMarketSessionAuthority>();
        sessions.Current.Returns(new MarketSessionReadModel { State = state });
        var ledger = new EmulatorLedger(EmulatorScenario.Development("FROZEN-TEST"), new SystemEmulatorClock());
        var broker = new FrozenEmulatorOrderExecutionBroker(new EmulatedOrderExecutionBroker(ledger),
            sessions, new SystemEmulatorClock());
        var first = $"A-{Guid.NewGuid():N}";
        var second = $"B-{Guid.NewGuid():N}";
        FrozenEmulatorQuoteStore.Publish([Quote(first, 10m, 11m), Quote(second, 9m, 10m)], now);
        var request = new FrameworkOrderRequest("FROZEN-TEST", Guid.NewGuid().ToString("N"),
            Guid.NewGuid(), Guid.NewGuid(), FrameworkOrderShape.VerticalSpread, false,
            [new(Guid.NewGuid(), first, 1, 100m, null, 1, 1m),
             new(Guid.NewGuid(), second, -1, 105m, null, 1, 1m)],
            5m, 5m, 5m, 0.05m, DateTime.UtcNow.AddMinutes(5), "approval", "reference",
            1_000m, 1_000m);

        var receipt = await broker.PlaceAsync(request);

        Assert.Equal(FrameworkDispatchOutcome.AcceptedForDispatch, receipt.Outcome);
        var observations = await broker.ReconcileAsync(request.BrokerOrderId);
        Assert.Equal(shouldFill, observations.Any(value => value.Kind == FrameworkObservationKind.Execution));
    }

    private static EvaluatedOptionContractReadModel Quote(string id, decimal bid, decimal ask) =>
        new(id, 100m, true, bid, ask, 10, 10, null, null, null, null, null, null,
            null, null, null, null, null, false, true, null, null, null);
}
