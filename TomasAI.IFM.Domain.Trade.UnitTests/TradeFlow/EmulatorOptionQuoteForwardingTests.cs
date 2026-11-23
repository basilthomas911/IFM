using NSubstitute;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class EmulatorOptionQuoteForwardingTests
{
    [Fact]
    public async Task Native_quote_keeps_actual_market_time_depth_and_generation_even_with_local_offset()
    {
        var context = Substitute.For<IBrokerOrderRealtimeContext>();
        var broker = Substitute.For<ITradeBroker>(); context.TradeBroker.Returns(broker);
        var at = new DateTimeOffset(2026,11,18,14,10,42,TimeSpan.FromHours(-5));
        var tick = new FuturesOptionTickDataV2ReadModel { ContractId="ES20261211P7175", SourceGeneration=Guid.NewGuid(),
            ObservedAtUtc=at, BidPrice=92.5, AskPrice=92.75, BidSize=3, AskSize=26 };
        var source = new FuturesTickTradeDataChangedEvent { OptionMarketPriceObservation = new("ES20261218",
            OptionMarketPriceBasis.QuoteMidpoint,tick,123,at.UtcDateTime) };
        await source.ForwardOptionQuoteAsync(context);
        await broker.Received(1).PublishMarketQuoteAsync(Arg.Is<BrokerMarketQuote>(q => q.ContractId==tick.ContractId
            && q.Bid==92.5m && q.Ask==92.75m && q.BidSize==3 && q.AskSize==26
            && q.MarketTimeUtc==at.UtcDateTime && q.SourceSequence==123
            && q.SourceEpoch==(BitConverter.ToInt64(tick.SourceGeneration.ToByteArray(),0)&long.MaxValue)));
    }

    [Theory]
    [InlineData("trade")][InlineData("pricing")][InlineData("crossed")][InlineData("unidentified")]
    public async Task Non_executable_observations_do_not_refresh_the_execution_book(string invalid)
    {
        var context = Substitute.For<IBrokerOrderRealtimeContext>();
        var broker = Substitute.For<ITradeBroker>(); context.TradeBroker.Returns(broker);
        var at=DateTimeOffset.UtcNow;
        var tick=new FuturesOptionTickDataV2ReadModel { ContractId="ES20261211P7175",SourceGeneration=Guid.NewGuid(),
            ObservedAtUtc=at,BidPrice=1,AskPrice=2,BidSize=1,AskSize=1,
            IsPricingUpdate=invalid=="pricing",IsTradeObservation=invalid=="trade" };
        if(invalid=="crossed")tick=tick with{AskPrice=.5};
        if(invalid=="unidentified")tick=tick with{SourceGeneration=Guid.Empty};
        await new FuturesTickTradeDataChangedEvent{OptionMarketPriceObservation=new("ES20261218",OptionMarketPriceBasis.QuoteMidpoint,tick,123,at.UtcDateTime)}.ForwardOptionQuoteAsync(context);
        await broker.DidNotReceive().PublishMarketQuoteAsync(Arg.Any<BrokerMarketQuote>());
    }
}
