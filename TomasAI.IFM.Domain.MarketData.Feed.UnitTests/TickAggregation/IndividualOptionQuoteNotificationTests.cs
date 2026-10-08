using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.TickAggregation;

public sealed class IndividualOptionQuoteNotificationTests
{
    [Theory]
    [InlineData(OptionMarketPriceBasis.QuoteMidpoint, false)]
    [InlineData(OptionMarketPriceBasis.Trade, true)]
    public async Task Option_observation_notifies_ui_and_only_actual_trades_enter_trade_storage(OptionMarketPriceBasis basis, bool persisted)
    {
        var context = Substitute.For<ITickAggregationRealtimeContext>();
        var data = new FuturesOptionTickDataV2ReadModel { ContractId = "ES20261120C8450", ValueDate = new(2026,10,7), TickId = 42, OptionPrice = 10, GreeksAvailable = false };
        var changed = new FuturesTickTradeDataChangedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = new(data.ContractId, data.ValueDate, AssetTypeId.FuturesOption),
            TickDataId = new(data.ContractId, data.ValueDate, 42, DateTime.UtcNow),
            OptionMarketPriceObservation = new("ES20261218", basis, data, 42, DateTime.UtcNow)
        };
        await TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime.FuturesTickTradeDataChanged.ExecuteAsync(changed, context);
        await context.Received(1).SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(
            Arg.Is<OptionTradeTickPriceDataUpdatedEvent>(value => value.OptionTickData == data && value.CommandId == changed.CommandId && value.Id == changed.Id));
        var writes = context.Projector.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "ProcessRealtimeEventAsync");
        Assert.Equal(persisted ? 1 : 0, writes.Count());
    }
}
