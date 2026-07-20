using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class FuturesTickDataModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesTickDataModel _sut;

    public FuturesTickDataModelTests()
    {
        _sut = new FuturesTickDataModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 12, 1);
        var expectedKey = "test-key";
        var cachedJson = "{\"data\":true}";
        var expected = new FuturesTickDataV2ReadModel();

        _jsonSerializer.Serialize(Arg.Any<object>()).Returns(expectedKey);
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<FuturesTickDataV2ReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(contractId, valueDate);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 12, 1);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(contractId, valueDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WhenCacheReturnsEmpty_ReturnsDefault()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 12, 1);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);

        // Act
        var result = _sut.Get(contractId, valueDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 12, 1);
        var data = new FuturesTickDataV2ReadModel();
        var expectedKey = "test-key";
        var serializedValue = "{\"serialized\":true}";

        _jsonSerializer.Serialize(Arg.Is<object>(o => o != null && o != (object)data)).Returns(expectedKey);
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(contractId, valueDate, data);

        // Assert
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class FuturesOptionTickDataModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesOptionTickDataModel _sut;

    public FuturesOptionTickDataModelTests()
    {
        _sut = new FuturesOptionTickDataModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contractId = "ESZ4_C5000";
        var valueDate = new DateOnly(2024, 12, 1);
        var expectedKey = "key";
        var cachedJson = "{}";
        var expected = new FuturesOptionTickDataV2ReadModel();

        _jsonSerializer.Serialize(Arg.Any<object>()).Returns(expectedKey);
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<FuturesOptionTickDataV2ReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(contractId, valueDate);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get("ESZ4_C5000", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new FuturesOptionTickDataV2ReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set("ESZ4_C5000", new DateOnly(2024, 12, 1), data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class FuturesEodDataModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesEodDataModel _sut;

    public FuturesEodDataModelTests()
    {
        _sut = new FuturesEodDataModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var expected = new FuturesEodDataV2ReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get("ESZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get("ESZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new FuturesEodDataV2ReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set("ESZ4", new DateOnly(2024, 12, 1), data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class FundBalanceModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FundBalanceModel _sut;

    public FundBalanceModelTests()
    {
        _sut = new FundBalanceModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var expected = new FundBalanceReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<FundBalanceReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new FundBalanceReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(1, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class TradeOrderModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly TradeOrderModel _sut;

    public TradeOrderModelTests()
    {
        _sut = new TradeOrderModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new Shared.TradeOrder.TradeOrderEntityId(1, 2, new DateOnly(2024, 12, 1));
        var expected = new Shared.TradeOrder.ViewModels.TradeOrderReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.TradeOrder.ViewModels.TradeOrderReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new Shared.TradeOrder.TradeOrderEntityId(1, 2, new DateOnly(2024, 12, 1));
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new Shared.TradeOrder.TradeOrderEntityId(1, 2, new DateOnly(2024, 12, 1));
        var data = new Shared.TradeOrder.ViewModels.TradeOrderReadModel();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class TradePositionActionModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly TradePositionActionModel _sut;

    public TradePositionActionModelTests()
    {
        _sut = new TradePositionActionModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new Shared.Trade.TradePositionEntityId();
        var expected = new Shared.Trade.ViewModels.TradePositionActionReadModel(Shared.Trade.ActionSource.TradePosition, Shared.Trade.ActionType.PlaceOpenOrder, Shared.Trade.ActionSubType.None, Shared.Trade.ActionState.Normal, "test");
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.Trade.ViewModels.TradePositionActionReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new Shared.Trade.TradePositionEntityId();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new Shared.Trade.TradePositionEntityId();
        var data = new Shared.Trade.ViewModels.TradePositionActionReadModel(Shared.Trade.ActionSource.TradePosition, Shared.Trade.ActionType.PlaceOpenOrder, Shared.Trade.ActionSubType.None, Shared.Trade.ActionState.Normal, "test");
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class HedgePositionTradeIdModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly HedgePositionTradeIdModel _sut;

    public HedgePositionTradeIdModelTests()
    {
        _sut = new HedgePositionTradeIdModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new Shared.Trade.TradePositionEntityId();
        var expected = new Shared.Trade.OptionTradeEntityId(1, 2);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.Trade.OptionTradeEntityId>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new Shared.Trade.TradePositionEntityId();
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new Shared.Trade.TradePositionEntityId();
        var optionTradeId = new Shared.Trade.OptionTradeEntityId(1, 2);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(optionTradeId).Returns("value");

        // Act
        _sut.Set(entityId, optionTradeId);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class StopLossLimitModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly StopLossLimitModel _sut;

    public StopLossLimitModelTests()
    {
        _sut = new StopLossLimitModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new Shared.Trade.OptionTradeEntityId(1, 2);
        var expected = new Shared.Trade.ViewModels.TradePlanStopLossLimitReadModel(1.5);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.Trade.ViewModels.TradePlanStopLossLimitReadModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new Shared.Trade.OptionTradeEntityId(1, 2);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new Shared.Trade.OptionTradeEntityId(1, 2);
        var data = new Shared.Trade.ViewModels.TradePlanStopLossLimitReadModel(2.0);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class IronCondorMDILimitModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly IronCondorMDILimitModel _sut;

    public IronCondorMDILimitModelTests()
    {
        _sut = new IronCondorMDILimitModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new Shared.Trade.OptionTradeEntityId(1, 2);
        var valueDate = new DateOnly(2024, 12, 1);
        var expected = new Shared.Trade.ViewModels.IronCondorMDILimitDataModel(entityId, valueDate, 1.0, 0.8, 1.5);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.Trade.ViewModels.IronCondorMDILimitDataModel>("{}").Returns(expected);

        // Act
        var result = _sut.Get(entityId, valueDate);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new Shared.Trade.OptionTradeEntityId(1, 2);
        var valueDate = new DateOnly(2024, 12, 1);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(entityId, valueDate);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new Shared.Trade.OptionTradeEntityId(1, 2);
        var valueDate = new DateOnly(2024, 12, 1);
        var data = new Shared.Trade.ViewModels.IronCondorMDILimitDataModel(entityId, valueDate, 1.0, 0.8, 1.5);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(entityId, valueDate, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}

public class FuturesOptionTickDataStreamingParameterModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesOptionTickDataStreamingParameterModel _sut;

    public FuturesOptionTickDataStreamingParameterModelTests()
    {
        _sut = new FuturesOptionTickDataStreamingParameterModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contract = new Shared.MarketData.ViewModels.FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        var optionContract = new Shared.MarketData.ViewModels.FuturesOptionContractReadModel();
        var expected = new Shared.MarketDataFeed.FuturesOptionTickDataStreamingParameter(100, new DateOnly(2024, 12, 1), new DateOnly(2024, 12, 20), 0.05, contract, optionContract);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns("{}");
        _jsonSerializer.Deserialize<Shared.MarketDataFeed.FuturesOptionTickDataStreamingParameter>("{}").Returns(expected);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns((string?)null);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var contract = new Shared.MarketData.ViewModels.FuturesContractV2ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        var optionContract = new Shared.MarketData.ViewModels.FuturesOptionContractReadModel();
        var data = new Shared.MarketDataFeed.FuturesOptionTickDataStreamingParameter(100, new DateOnly(2024, 12, 1), new DateOnly(2024, 12, 20), 0.05, contract, optionContract);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _jsonSerializer.Serialize(data).Returns("value");

        // Act
        _sut.Set(123, data);

        // Assert
        _redisCache.Received(1).Set("key", Arg.Any<string>());
    }
}
