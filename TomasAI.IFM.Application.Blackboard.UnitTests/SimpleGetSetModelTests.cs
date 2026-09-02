using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

public class FuturesEodDataModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesEodDataCacheModel _sut;

    public FuturesEodDataModelTests()
    {
        _sut = new FuturesEodDataCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var expectedKey = "FuturesEodData:ESZ4. 20241201";
        var cachedJson = "{}";
        var expected = new FuturesEodDataV2ReadModel();
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get("ESZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<FuturesEodDataV2ReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var expectedKey = "FuturesEodData:ESZ4. 20241201";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get("ESZ4", new DateOnly(2024, 12, 1));

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<FuturesEodDataV2ReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new FuturesEodDataV2ReadModel();
        var expectedKey = "FuturesEodData:ESZ4. 20241201";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set("ESZ4", new DateOnly(2024, 12, 1), data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class FundBalanceModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FundBalanceCacheModel _sut;

    public FundBalanceModelTests()
    {
        _sut = new FundBalanceCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var expectedKey = "FundBalanceByOrderId:1";
        var cachedJson = "{}";
        var expected = new FundBalanceReadModel();
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<FundBalanceReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<FundBalanceReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var expectedKey = "FundBalanceByOrderId:1";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(1);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<FundBalanceReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var data = new FundBalanceReadModel();
        var expectedKey = "FundBalanceByOrderId:1";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(1, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class TradeOrderModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly TradeOrderCacheModel _sut;

    public TradeOrderModelTests()
    {
        _sut = new TradeOrderCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.TradeOrderEntityId(1, 2, new DateOnly(2024, 12, 1));
        var expectedKey = "TradeOrder:1.2.20241201";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels.TradeOrderReadModel();
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels.TradeOrderReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels.TradeOrderReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.TradeOrderEntityId(1, 2, new DateOnly(2024, 12, 1));
        var expectedKey = "TradeOrder:1.2.20241201";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels.TradeOrderReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.TradeOrderEntityId(1, 2, new DateOnly(2024, 12, 1));
        var data = new global::TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels.TradeOrderReadModel();
        var expectedKey = "TradeOrder:1.2.20241201";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(entityId, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class TradePositionActionModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly TradePositionActionCacheModel _sut;

    public TradePositionActionModelTests()
    {
        _sut = new TradePositionActionCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradePositionEntityId();
        var expectedKey = "TradePositionAction:0.0.0001-01-01.Unknown.Open.0";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePositionActionReadModel(global::TomasAI.IFM.Domain.Trade.Shared.ActionSource.TradePosition, global::TomasAI.IFM.Domain.Trade.Shared.ActionType.PlaceOpenOrder, global::TomasAI.IFM.Domain.Trade.Shared.ActionSubType.None, global::TomasAI.IFM.Domain.Trade.Shared.ActionState.Normal, "test");
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePositionActionReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePositionActionReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradePositionEntityId();
        var expectedKey = "TradePositionAction:0.0.0001-01-01.Unknown.Open.0";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePositionActionReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradePositionEntityId();
        var data = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePositionActionReadModel(global::TomasAI.IFM.Domain.Trade.Shared.ActionSource.TradePosition, global::TomasAI.IFM.Domain.Trade.Shared.ActionType.PlaceOpenOrder, global::TomasAI.IFM.Domain.Trade.Shared.ActionSubType.None, global::TomasAI.IFM.Domain.Trade.Shared.ActionState.Normal, "test");
        var expectedKey = "TradePositionAction:0.0.0001-01-01.Unknown.Open.0";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(entityId, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class HedgePositionTradeIdModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly HedgePositionTradeIdCacheModel _sut;

    public HedgePositionTradeIdModelTests()
    {
        _sut = new HedgePositionTradeIdCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradePositionEntityId();
        var expectedKey = "HedgePositionTradeId:0.0.0001-01-01.Unknown.Open.0";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradePositionEntityId();
        var expectedKey = "HedgePositionTradeId:0.0.0001-01-01.Unknown.Open.0";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.TradePositionEntityId();
        var optionTradeId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var expectedKey = "HedgePositionTradeId:0.0.0001-01-01.Unknown.Open.0";
        var serializedValue = "value";
        _jsonSerializer.Serialize(optionTradeId).Returns(serializedValue);

        // Act
        _sut.Set(entityId, optionTradeId);

        // Assert
        _jsonSerializer.Received(1).Serialize(optionTradeId);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class StopLossLimitModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly StopLossLimitCacheModel _sut;

    public StopLossLimitModelTests()
    {
        _sut = new StopLossLimitCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var expectedKey = "StopLossLimit:1.2";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanStopLossLimitReadModel(1.5);
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanStopLossLimitReadModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanStopLossLimitReadModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var expectedKey = "StopLossLimit:1.2";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanStopLossLimitReadModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var data = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradePlanStopLossLimitReadModel(2.0);
        var expectedKey = "StopLossLimit:1.2";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(entityId, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }

    [Fact]
    public void Exists_UsesCanonicalCacheKey()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var expectedKey = "StopLossLimit:1.2";
        _redisCache.TryGet(expectedKey, out Arg.Any<string?>())
            .Returns(callInfo =>
            {
                callInfo[1] = "value";
                return true;
            });

        // Act
        var result = _sut.Exists(entityId);

        // Assert
        result.Should().BeTrue();
        _redisCache.Received(1).TryGet(expectedKey, out Arg.Any<string?>());
    }

    [Fact]
    public void Remove_UsesCanonicalCacheKey()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var expectedKey = "StopLossLimit:1.2";

        // Act
        _sut.Remove(entityId);

        // Assert
        _redisCache.Received(1).Remove(expectedKey);
    }
}

public class IronCondorMDILimitModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly IronCondorMDILimitCacheModel _sut;

    public IronCondorMDILimitModelTests()
    {
        _sut = new IronCondorMDILimitCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var valueDate = new DateOnly(2024, 12, 1);
        var expectedKey = "IronCondorMDILimit:1.2,20241201";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.IronCondorMDILimitDataModel(entityId, valueDate, 1.0, 0.8, 1.5);
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.IronCondorMDILimitDataModel>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(entityId, valueDate);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.IronCondorMDILimitDataModel>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var valueDate = new DateOnly(2024, 12, 1);
        var expectedKey = "IronCondorMDILimit:1.2,20241201";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(entityId, valueDate);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.IronCondorMDILimitDataModel>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var entityId = new global::TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2);
        var valueDate = new DateOnly(2024, 12, 1);
        var data = new global::TomasAI.IFM.Domain.Trade.Shared.ViewModels.IronCondorMDILimitDataModel(entityId, valueDate, 1.0, 0.8, 1.5);
        var expectedKey = "IronCondorMDILimit:1.2,20241201";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(entityId, valueDate, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}

public class FuturesOptionTickDataStreamingParameterModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesOptionTickDataStreamingParameterCacheModel _sut;

    public FuturesOptionTickDataStreamingParameterModelTests()
    {
        _sut = new FuturesOptionTickDataStreamingParameterCacheModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public void Get_WhenCacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contract = new FuturesContractV3ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        var optionContract = new global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel();
        var expectedKey = "FuturesOptionTickDataStreamingParameter:123";
        var cachedJson = "{}";
        var expected = new global::TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesOptionTickDataStreamingParameter(100, new DateOnly(2024, 12, 1), new DateOnly(2024, 12, 20), 0.05, contract, optionContract);
        _redisCache.Get(expectedKey).Returns(cachedJson);
        _jsonSerializer.Deserialize<global::TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesOptionTickDataStreamingParameter>(cachedJson).Returns(expected);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().Be(expected);
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.Received(1).Deserialize<global::TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesOptionTickDataStreamingParameter>(cachedJson);
    }

    [Fact]
    public void Get_WhenCacheMiss_ReturnsDefault()
    {
        // Arrange
        var expectedKey = "FuturesOptionTickDataStreamingParameter:123";
        _redisCache.Get(expectedKey).Returns((string?)null);

        // Act
        var result = _sut.Get(123);

        // Assert
        result.Should().BeNull();
        _redisCache.Received(1).Get(expectedKey);
        _jsonSerializer.DidNotReceive().Deserialize<global::TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesOptionTickDataStreamingParameter>(Arg.Any<string>());
    }

    [Fact]
    public void Set_SerializesAndCachesValue()
    {
        // Arrange
        var contract = new FuturesContractV3ReadModel("ESZ4", "ES Dec 2024", "ES", "ESZ4", "FUT", "USD", "CME", "50", new DateOnly(2024, 12, 20), true);
        var optionContract = new global::TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel();
        var data = new global::TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesOptionTickDataStreamingParameter(100, new DateOnly(2024, 12, 1), new DateOnly(2024, 12, 20), 0.05, contract, optionContract);
        var expectedKey = "FuturesOptionTickDataStreamingParameter:123";
        var serializedValue = "value";
        _jsonSerializer.Serialize(data).Returns(serializedValue);

        // Act
        _sut.Set(123, data);

        // Assert
        _jsonSerializer.Received(1).Serialize(data);
        _redisCache.Received(1).Set(expectedKey, serializedValue);
    }
}
