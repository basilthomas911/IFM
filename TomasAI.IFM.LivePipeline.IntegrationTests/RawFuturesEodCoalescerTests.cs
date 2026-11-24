using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Testing;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class RawFuturesEodCoalescerTests
{
    [Fact]
    public async Task Coalescing_retains_both_extrema_final_close_and_original_observation_time()
    {
        var supervisor = Substitute.For<IActorSupervisor>(); supervisor.IsReady.Returns(true);
        var producer = Substitute.For<IActorProducer>(); supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var published = new TaskCompletionSource<FuturesEodTradeBatchRealtimeEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<FuturesEodTradeBatchRealtimeEvent, FuturesEodDataId>(Arg.Any<ActorSubject>(),
            Arg.Any<FuturesEodTradeBatchRealtimeEvent>(), Arg.Any<CancellationToken>()).Returns(call =>
            { published.TrySetResult(call.Arg<FuturesEodTradeBatchRealtimeEvent>()); return ValueTask.CompletedTask; });
        using var buffer = new RawFuturesEodCoalescer(supervisor, NullLogger<RawFuturesEodCoalescer>.Instance);
        var tape = RawFuturesJournalFixture.Batch(4, false); decimal[] prices = [6000m, 6025m, 5980m, 6005m];
        FuturesTickTradeDataChangedEvent? last = null;
        for (var index = 0; index < tape.Records.Length; index++)
        {
            var source = MessagePackSerializer.Deserialize<FuturesTickTradeDataChangedEvent>(tape.Records[index].Payload);
            last = source with { TradeData = source.TradeData with { Price = prices[index], PriceRaw = (long)(prices[index] * 1_000_000_000) } };
            buffer.Observe(new FuturesMarketPriceUpdatedRealtimeEvent { UpdateSource = FuturesMarketPriceUpdateSource.Trade, SourceTrade = last });
        }
        await buffer.StartAsync(CancellationToken.None);
        var view = await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await buffer.StopAsync(CancellationToken.None);
        Assert.Equal(6025m, view.High); Assert.Equal(5980m, view.Low); Assert.Equal(6005m, view.Last.TradeData.Price);
        Assert.Equal(last!.Id, view.Last.Id); Assert.Equal(last.CommandId, view.Last.CommandId);
        Assert.Equal(last.ReceivedOn, view.ReceivedOn); Assert.Equal(last.TradeData.EventTimestampNanoseconds, view.Last.TradeData.EventTimestampNanoseconds);
        Assert.Equal(last.EntityId.ContractId, view.EntityId.ContractId);
    }
}
