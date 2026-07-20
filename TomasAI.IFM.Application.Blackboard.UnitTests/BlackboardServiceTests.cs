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
    public void Constructor_InitializesAllProperties()
    {
        // Arrange & Act
        var sut = new BlackboardService(_redisCache, _jsonSerializer);

        // Assert
        sut.OptionTrade.Should().NotBeNull();
        sut.ReferenceLookup.Should().NotBeNull();
        sut.TradePositionAction.Should().NotBeNull();
        sut.TradePlanForwardLossLimit.Should().NotBeNull();
        sut.HedgePositionTradeId.Should().NotBeNull();
        sut.FuturesTickData.Should().NotBeNull();
        sut.FuturesOptionTickData.Should().NotBeNull();
        sut.FuturesTickDataStreamingParameter.Should().NotBeNull();
        sut.FuturesOptionTickDataStreamingParameter.Should().NotBeNull();
        sut.FuturesEodData.Should().NotBeNull();
        sut.VixFuturesEodData.Should().NotBeNull();
        sut.FuturesEodDataRange.Should().NotBeNull();
        sut.NormalCurveTable.Should().NotBeNull();
        sut.FuturesContract.Should().NotBeNull();
        sut.VixFuturesContractId.Should().NotBeNull();
        sut.TradeOrder.Should().NotBeNull();
        sut.DomainEvents.Should().NotBeNull();
        sut.IronCondorMDILimit.Should().NotBeNull();
        sut.FuturesContractSymbol.Should().NotBeNull();
        sut.FuturesItiSignalAveragePredictedTrendDelta.Should().NotBeNull();
        sut.FuturesItiSignalAveragePredictedTrendDeltaRange.Should().NotBeNull();
        sut.FuturesItiSignalMDI.Should().NotBeNull();
        sut.FuturesOptionQuote.Should().NotBeNull();
        sut.FuturesOptionQuoteData.Should().NotBeNull();
        sut.ForwardLossRatioMap.Should().NotBeNull();
        sut.StopLossLimit.Should().NotBeNull();
        sut.SignalProcessor.Should().NotBeNull();
        sut.FundBalance.Should().NotBeNull();
        sut.EventStreamId.Should().NotBeNull();
        sut.EventNameId.Should().NotBeNull();
        sut.FuturesOpenPrice.Should().NotBeNull();
        sut.VixFuturesOpenPrice.Should().NotBeNull();
        sut.StreamingRequestId.Should().NotBeNull();
        sut.SequenceCounter.Should().NotBeNull();
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
