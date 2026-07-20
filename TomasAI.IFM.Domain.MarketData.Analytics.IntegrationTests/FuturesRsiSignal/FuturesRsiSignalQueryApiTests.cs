using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesRsiSignal;

public class FuturesRsiSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    static FuturesRsiSignalReadModel CreateRsiSignal(
        TradeTimePeriodType signalType,
        TimeOnly timestamp,
        decimal price,
        double rsi = 55.0,
        double rsiSlope = 0.1,
        int windowSize = 60)
        => new(
            contractId: SampleData.ContractId,
            valueDate: SampleData.ValueDate,
            timePeriod: signalType,
            periodLength: windowSize,
            timestamp: timestamp,
            price: price,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 0.0,
            rsi: rsi,
            rsiAverage: 0.0,
            rsiSlope: rsiSlope);

    async Task SeedRsiSignalsAsync()
    {
        var baseTime = TimeOnly.FromDateTime(SampleData.Timestamp);

        // OneMinute signal 1
        await dbFixture.MarketDataDb.InsertFuturesRsiSignalAsync(
            CreateRsiSignal(TradeTimePeriodType.OneMinute, baseTime, (decimal)SampleData.FuturesPrice, rsi: 55.0));

        // OneMinute signal 2
        await dbFixture.MarketDataDb.InsertFuturesRsiSignalAsync(
            CreateRsiSignal(TradeTimePeriodType.OneMinute, baseTime.AddMinutes(1), (decimal)SampleData.FuturesPrice + 10m, rsi: 58.0));

        // Daily signal
        await dbFixture.MarketDataDb.InsertFuturesRsiSignalAsync(
            CreateRsiSignal(TradeTimePeriodType.Daily, baseTime, (decimal)SampleData.FuturesPrice, rsi: 52.0, windowSize: 14));
    }

    [Fact]
    public async Task GetFuturesRsiSignal_Ok()
    {
        // arrange...
        await SeedRsiSignalsAsync();

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TimePeriod.Should().Be(SampleData.TimePeriod);
    }

    [Fact]
    public async Task GetFuturesRsiSignal_BySignalType_Ok()
    {
        // arrange...
        await SeedRsiSignalsAsync();

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily, 14);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TimePeriod.Should().Be(TradeTimePeriodType.Daily);
    }

    [Fact]
    public async Task GetFuturesTrendDirectionFromRSISignal_Ok()
    {
        // arrange...
        await SeedRsiSignalsAsync();

        var timestamp = SampleData.Timestamp;
        var startTime = timestamp.AddMinutes(-5);
        var endTime = timestamp.AddMinutes(5);
        var lookbackInterval = 5;

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesTrendDirectionFromRSISignalAsync(
            SampleData.ContractId, SampleData.ValueDate, timestamp, lookbackInterval, startTime, endTime);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
    }
}
