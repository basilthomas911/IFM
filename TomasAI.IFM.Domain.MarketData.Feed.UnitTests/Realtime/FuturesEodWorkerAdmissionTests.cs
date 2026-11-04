using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Realtime;

/// <summary>Unrelated options and released streams cannot consume the bounded futures workers.</summary>
public sealed class FuturesEodWorkerAdmissionTests
{
    [Fact]
    public async Task Option_ticks_are_filtered_before_worker_or_provider_lookup()
    {
        var context = Substitute.For<IFuturesEodDataRealtimeContext>();
        var market = Substitute.For<IMarketDataApi>();
        context.MarketDataApi.Returns(market);
        context.TradeDispatch.Returns(new FuturesEodTradeDispatchState());
        context.EnableAsyncTradeWorker.Returns(true);
        context.Logger.Returns(NullLogger<FuturesEodDataRealtimeActor>.Instance);
        context.StartTradeWorkers();
        for (var index = 0; index < 100; index++)
            await context.ReceiveTradeAsync(context, new FuturesTickTradeDataInsertedEvent { AssetTypeId = AssetTypeId.FuturesOption });
        Assert.Empty(context.TradeDispatch.Workers);
        Assert.Equal(0, context.TradeDispatch.Handoffs);
        market.DidNotReceive().IsTickDataStreamActive(Arg.Any<string>());
        await context.StopTradeWorkersAsync();
    }
}
