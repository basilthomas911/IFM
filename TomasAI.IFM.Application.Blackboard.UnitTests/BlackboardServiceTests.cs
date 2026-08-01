using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class BlackboardServiceTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var sut = new BlackboardService(_redisCache, _jsonSerializer);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullRedisCache_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new BlackboardService(null!, _jsonSerializer);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullJsonSerializer_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new BlackboardService(_redisCache, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_InitializesAllDomainRootsAndCacheModels()
    {
        // Arrange & Act
        var sut = new BlackboardService(_redisCache, _jsonSerializer);

        // Assert
        sut.Application.SequenceCounter.Should().NotBeNull();

        sut.EventSourcing.DomainEvents.Should().NotBeNull();
        sut.EventSourcing.EventStreamId.Should().NotBeNull();
        sut.EventSourcing.EventNameId.Should().NotBeNull();
        sut.EventSourcing.EventProjectorState.Should().NotBeNull();

        sut.Fund.FundBalance.Should().NotBeNull();
        sut.MarketData.RiskFreeRate.Should().NotBeNull();

        sut.MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDelta
            .Should().NotBeNull();
        sut.MarketDataAnalytics.FuturesItiSignalAveragePredictedTrendDeltaRange
            .Should().NotBeNull();
        sut.MarketDataAnalytics.FuturesItiSignalMDI.Should().NotBeNull();
        sut.MarketDataAnalytics.FuturesRsiSignal.Should().NotBeNull();
        sut.MarketDataAnalytics.FuturesRsiDailySignal.Should().NotBeNull();

        sut.MarketDataFeed.FuturesTickData.Should().NotBeNull();
        sut.MarketDataFeed.FuturesOptionTickData.Should().NotBeNull();
        sut.MarketDataFeed.FuturesOptionTickPriceData.Should().NotBeNull();
        sut.MarketDataFeed.FuturesTickDataStreamingParameter.Should().NotBeNull();
        sut.MarketDataFeed.FuturesOptionTickDataStreamingParameter.Should().NotBeNull();
        sut.MarketDataFeed.FuturesEodData.Should().NotBeNull();
        sut.MarketDataFeed.VixFuturesEodData.Should().NotBeNull();
        sut.MarketDataFeed.FuturesEodDataRange.Should().NotBeNull();
        sut.MarketDataFeed.NormalCurveTable.Should().NotBeNull();
        sut.MarketDataFeed.VixFuturesContractId.Should().NotBeNull();
        sut.MarketDataFeed.FuturesOptionQuote.Should().NotBeNull();
        sut.MarketDataFeed.FuturesOptionQuoteData.Should().NotBeNull();
        sut.MarketDataFeed.FuturesOpenPrice.Should().NotBeNull();
        sut.MarketDataFeed.VixFuturesOpenPrice.Should().NotBeNull();
        sut.MarketDataFeed.StreamingRequestId.Should().NotBeNull();

        sut.MarketDataSecurities.DatabentoContractMapping.Should().NotBeNull();
        sut.MarketDataSecurities.FuturesContract.Should().NotBeNull();
        sut.MarketDataSecurities.FuturesContractSymbol.Should().NotBeNull();

        sut.Reference.ReferenceLookup.Should().NotBeNull();

        sut.Trade.OptionTrade.Should().NotBeNull();
        sut.Trade.TradePositionAction.Should().NotBeNull();
        sut.Trade.TradePlanForwardLossLimit.Should().NotBeNull();
        sut.Trade.HedgePositionTradeId.Should().NotBeNull();
        sut.Trade.TradeOrder.Should().NotBeNull();
        sut.Trade.IronCondorMDILimit.Should().NotBeNull();
        sut.Trade.ForwardLossRatioMap.Should().NotBeNull();
        sut.Trade.StopLossLimit.Should().NotBeNull();
        sut.Trade.SignalProcessor.Should().NotBeNull();
    }

    [Fact]
    public void CompatibilityAliasesReferenceTheDomainRootModels()
    {
        var sut = new BlackboardService(_redisCache, _jsonSerializer);

#pragma warning disable CS0618
        sut.FuturesContract.Should().BeSameAs(
            sut.MarketDataSecurities.FuturesContract);
        sut.FuturesOptionTickDataStreamingParameter.Should().BeSameAs(
            sut.MarketDataFeed.FuturesOptionTickDataStreamingParameter);
        sut.EventProjectorState.Should().BeSameAs(
            sut.EventSourcing.EventProjectorState);
#pragma warning restore CS0618

        sut.MarketDataFeed.FuturesOptionTickPriceData.Should().BeSameAs(
            sut.MarketDataFeed.FuturesOptionTickData);
    }

    [Fact]
    public void Service_ImplementsIBlackboardService()
    {
        // Arrange & Act
        var sut = new BlackboardService(_redisCache, _jsonSerializer);

        // Assert
        sut.Should().BeAssignableTo<IBlackboardService>();
    }
}
