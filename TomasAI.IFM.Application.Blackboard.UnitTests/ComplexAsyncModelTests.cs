using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Blackboard.UnitTests;

#region FuturesEodDataRangeModel Tests

public class FuturesEodDataRangeModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesEodDataRangeModel _sut;

    public FuturesEodDataRangeModelTests()
    {
        _sut = new FuturesEodDataRangeModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var eodData = new[] { new FuturesEodDataV2ReadModel(contractId, valueDate, "ES", 5000m, 5100m, 4900m, 5050m, 1000) };
        var serialized = "{\"data\"}";
        _redisCache.Get(Arg.Any<string>()).Returns(string.Empty, serialized);
        var callback = Substitute.For<Func<string, DateOnly, DateOnly, ValueTask<FuturesEodDataV2ReadModel[]>>>();
        callback(contractId, Arg.Any<DateOnly>(), valueDate).Returns(new ValueTask<FuturesEodDataV2ReadModel[]>(eodData));
        _jsonSerializer.Serialize(eodData).Returns(serialized);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(serialized).Returns(eodData);

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().BeEquivalentTo(eodData);
        _redisCache.Received().Set(Arg.Any<string>(), serialized);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_NullCallback_ReturnsEmpty()
    {
        // Arrange
        _redisCache.Get(Arg.Any<string>()).Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateOnly, DateOnly, ValueTask<FuturesEodDataV2ReadModel[]>>>();
        callback(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>())
            .Returns(new ValueTask<FuturesEodDataV2ReadModel[]>((FuturesEodDataV2ReadModel[]?)null));

        // Act
        var result = await _sut.GetAsync("ESZ4", new DateOnly(2024, 6, 15), callback);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAsync_CacheHit_DataIsFresh_ReturnsCachedData()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var eodData = new[] { new FuturesEodDataV2ReadModel(contractId, valueDate, "ES", 5000m, 5100m, 4900m, 5050m, 1000) };
        var serialized = "{\"data\"}";
        _redisCache.Get(Arg.Any<string>()).Returns(serialized);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(serialized).Returns(eodData);
        var callback = Substitute.For<Func<string, DateOnly, DateOnly, ValueTask<FuturesEodDataV2ReadModel[]>>>();

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().BeEquivalentTo(eodData);
        await callback.DidNotReceive().Invoke(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GetAsync_CacheHit_StaleData_RefreshesCache()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var staleDate = new DateOnly(2024, 6, 14);
        var staleData = new[] { new FuturesEodDataV2ReadModel(contractId, staleDate, "ES", 4900m, 5000m, 4800m, 4950m, 900) };
        var freshData = new[] { new FuturesEodDataV2ReadModel(contractId, valueDate, "ES", 5000m, 5100m, 4900m, 5050m, 1000) };
        var staleJson = "{\"stale\"}";
        var freshJson = "{\"fresh\"}";

        _redisCache.Get(Arg.Any<string>()).Returns(staleJson, freshJson);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(staleJson).Returns(staleData);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(freshJson).Returns(freshData);
        var callback = Substitute.For<Func<string, DateOnly, DateOnly, ValueTask<FuturesEodDataV2ReadModel[]>>>();
        callback(contractId, Arg.Any<DateOnly>(), valueDate).Returns(new ValueTask<FuturesEodDataV2ReadModel[]>(freshData));
        _jsonSerializer.Serialize(freshData).Returns(freshJson);

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().BeEquivalentTo(freshData);
        await callback.Received(1).Invoke(contractId, Arg.Any<DateOnly>(), valueDate);
    }

    [Fact]
    public async Task GetAsync_CacheHit_EmptyArray_RefreshesCache()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var emptyData = Array.Empty<FuturesEodDataV2ReadModel>();
        var freshData = new[] { new FuturesEodDataV2ReadModel(contractId, valueDate, "ES", 5000m, 5100m, 4900m, 5050m, 1000) };
        var emptyJson = "{\"empty\"}";
        var freshJson = "{\"fresh\"}";

        _redisCache.Get(Arg.Any<string>()).Returns(emptyJson, freshJson);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(emptyJson).Returns(emptyData);
        _jsonSerializer.Deserialize<FuturesEodDataV2ReadModel[]>(freshJson).Returns(freshData);
        var callback = Substitute.For<Func<string, DateOnly, DateOnly, ValueTask<FuturesEodDataV2ReadModel[]>>>();
        callback(contractId, Arg.Any<DateOnly>(), valueDate).Returns(new ValueTask<FuturesEodDataV2ReadModel[]>(freshData));
        _jsonSerializer.Serialize(freshData).Returns(freshJson);

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().BeEquivalentTo(freshData);
        await callback.Received(1).Invoke(contractId, Arg.Any<DateOnly>(), valueDate);
    }

    [Fact]
    public void Remove_RemovesCacheEntry()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("removeKey");

        // Act
        _sut.Remove("ESZ4", new DateOnly(2024, 6, 15));

        // Assert
        _redisCache.Received(1).Remove("removeKey");
    }
}

#endregion

#region FuturesItiSignalAveragePredictedTrendDeltaModel Tests

public class FuturesItiSignalAveragePredictedTrendDeltaModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesItiSignalAveragePredictedTrendDeltaModel _sut;

    public FuturesItiSignalAveragePredictedTrendDeltaModelTests()
    {
        _sut = new FuturesItiSignalAveragePredictedTrendDeltaModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var cachedJson = "{\"cached\"}";
        var expected = new FuturesItiSignalAveragePredictedTrendDeltaDataModel(contractId, valueDate, 1.5, -0.8, 55.0, 45.0);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(cachedJson);
        _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaDataModel>(cachedJson).Returns(expected);
        var callback = Substitute.For<Func<string, DateOnly, Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel>>>();

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().Be(expected);
        await callback.DidNotReceive().Invoke(Arg.Any<string>(), Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var callbackResult = new FuturesItiSignalAveragePredictedTrendDeltaDataModel(contractId, valueDate, 2.0, -1.0, 60.0, 40.0);
        var serialized = "{\"serialized\"}";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateOnly, Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel>>>();
        callback(contractId, valueDate).Returns(Task.FromResult(callbackResult));
        _jsonSerializer.Serialize(callbackResult).Returns(serialized);
        _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaDataModel>(serialized).Returns(callbackResult);

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().Be(callbackResult);
        _redisCache.Received(1).Set("key", serialized);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_NullCallback_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateOnly, Task<FuturesItiSignalAveragePredictedTrendDeltaDataModel>>>();
        callback(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalAveragePredictedTrendDeltaDataModel>(null!));

        // Act
        var result = await _sut.GetAsync("ESZ4", new DateOnly(2024, 1, 1), callback);

        // Assert
        result.Should().BeNull();
        _redisCache.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void BddConstructor_DoesNotThrow()
    {
        // Act
        var model = new FuturesItiSignalAveragePredictedTrendDeltaModel();

        // Assert
        model.Should().NotBeNull();
    }
}

#endregion

#region FuturesItiSignalAveragePredictedTrendDeltaRangeModel Tests

public class FuturesItiSignalAveragePredictedTrendDeltaRangeModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesItiSignalAveragePredictedTrendDeltaRangeModel _sut;

    public FuturesItiSignalAveragePredictedTrendDeltaRangeModelTests()
    {
        _sut = new FuturesItiSignalAveragePredictedTrendDeltaRangeModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedValue()
    {
        // Arrange
        var symbol = "ES";
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 6, 15);
        var cachedJson = "{\"cached\"}";
        var expected = new FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel(symbol, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 15), 1.2, -0.5);
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(cachedJson);
        _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>(cachedJson).Returns(expected);
        var callback = Substitute.For<Func<string, DateTime, DateTime, Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>>>();

        // Act
        var result = await _sut.GetAsync(symbol, startDate, endDate, callback);

        // Assert
        result.Should().Be(expected);
        await callback.DidNotReceive().Invoke(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var symbol = "ES";
        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 6, 15);
        var callbackResult = new FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel(symbol, new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 15), 1.5, -0.7);
        var serialized = "{\"serialized\"}";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateTime, DateTime, Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>>>();
        callback(symbol, startDate, endDate).Returns(Task.FromResult(callbackResult));
        _jsonSerializer.Serialize(callbackResult).Returns(serialized);
        _jsonSerializer.Deserialize<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>(serialized).Returns(callbackResult);

        // Act
        var result = await _sut.GetAsync(symbol, startDate, endDate, callback);

        // Assert
        result.Should().Be(callbackResult);
        _redisCache.Received(1).Set("key", serialized);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_NullCallback_ReturnsDefault()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateTime, DateTime, Task<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>>>();
        callback(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(Task.FromResult<FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel>(null!));

        // Act
        var result = await _sut.GetAsync("ES", DateTime.Now.AddYears(-1), DateTime.Now, callback);

        // Assert
        result.Should().BeNull();
        _redisCache.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void BddConstructor_DoesNotThrow()
    {
        // Act
        var model = new FuturesItiSignalAveragePredictedTrendDeltaRangeModel();

        // Assert
        model.Should().NotBeNull();
    }
}

#endregion

#region FuturesItiSignalMDIModel Tests

public class FuturesItiSignalMDIModelTests
{
    private readonly IRedisCache _redisCache = Substitute.For<IRedisCache>();
    private readonly IJsonSerializer _jsonSerializer = Substitute.For<IJsonSerializer>();
    private readonly FuturesItiSignalMDIModel _sut;

    public FuturesItiSignalMDIModelTests()
    {
        _sut = new FuturesItiSignalMDIModel(_redisCache, _jsonSerializer);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsDeserializedArray()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var cachedJson = "{\"cached\"}";
        var expected = new[] { new FuturesItiSignalMDIV2ReadModel(contractId, valueDate, DateTime.Now, IntrinsicTimeTrendType.UpTrend, 1.5) };
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(cachedJson);
        _jsonSerializer.Deserialize<FuturesItiSignalMDIV2ReadModel[]>(cachedJson).Returns(expected);
        var callback = Substitute.For<Func<string, DateOnly, Task<FuturesItiSignalMDIV2ReadModel[]>>>();

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().BeEquivalentTo(expected);
        await callback.DidNotReceive().Invoke(Arg.Any<string>(), Arg.Any<DateOnly>());
    }

    [Fact]
    public async Task GetAsync_CacheMiss_CallsCallbackAndCaches()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var callbackResult = new[] { new FuturesItiSignalMDIV2ReadModel(contractId, valueDate, DateTime.Now, IntrinsicTimeTrendType.DownTrend, -0.8) };
        var serialized = "{\"serialized\"}";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateOnly, Task<FuturesItiSignalMDIV2ReadModel[]>>>();
        callback(contractId, valueDate).Returns(Task.FromResult(callbackResult));
        _jsonSerializer.Serialize(callbackResult).Returns(serialized);
        _jsonSerializer.Deserialize<FuturesItiSignalMDIV2ReadModel[]>(serialized).Returns(callbackResult);

        // Act
        var result = await _sut.GetAsync(contractId, valueDate, callback);

        // Assert
        result.Should().BeEquivalentTo(callbackResult);
        _redisCache.Received(1).Set("key", serialized);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_NullCallbackResult_ReturnsEmptyArray()
    {
        // Arrange
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);
        var callback = Substitute.For<Func<string, DateOnly, Task<FuturesItiSignalMDIV2ReadModel[]>>>();
        callback(Arg.Any<string>(), Arg.Any<DateOnly>())
            .Returns(Task.FromResult<FuturesItiSignalMDIV2ReadModel[]>(null!));

        // Act
        var result = await _sut.GetAsync("ESZ4", new DateOnly(2024, 1, 1), callback);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Set_KeyExistsInCache_UpdatesValue()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var data = new[] { new FuturesItiSignalMDIV2ReadModel(contractId, valueDate, DateTime.Now, IntrinsicTimeTrendType.UpTrend, 2.0) };
        var serialized = "{\"serialized\"}";
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key", serialized);
        _redisCache.Get("key").Returns("existingValue");

        // Act
        _sut.Set(contractId, valueDate, data);

        // Assert
        _redisCache.Received(1).Set("key", serialized);
    }

    [Fact]
    public void Set_KeyNotInCache_DoesNotUpdate()
    {
        // Arrange
        var contractId = "ESZ4";
        var valueDate = new DateOnly(2024, 6, 15);
        var data = new[] { new FuturesItiSignalMDIV2ReadModel() };
        _jsonSerializer.Serialize(Arg.Any<object>()).Returns("key");
        _redisCache.Get("key").Returns(string.Empty);

        // Act
        _sut.Set(contractId, valueDate, data);

        // Assert
        _redisCache.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void BddConstructor_DoesNotThrow()
    {
        // Act
        var model = new FuturesItiSignalMDIModel();

        // Assert
        model.Should().NotBeNull();
    }
}

#endregion
