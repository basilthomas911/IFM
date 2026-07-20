using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class MarketDataAnalyticsCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task StartFuturesRsiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var entityId = new FuturesRsiSignalEntityId("CONTRACT1", DateOnly.FromDateTime(DateTime.Now), TradeTimePeriodType.FifteenSeconds, 14);
        var response = await api.StartFuturesRsiSignalAsync(entityId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StopFuturesRsiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var entityId = new FuturesRsiSignalEntityId("CONTRACT1", DateOnly.FromDateTime(DateTime.Now), TradeTimePeriodType.FifteenSeconds, 14);
        var response = await api.StopFuturesRsiSignalAsync(entityId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateFuturesRsiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var eodData = new FuturesEodDataV2ReadModel(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            "SYM",
            100, 110, 90, 105, 1000,
            0.05, 1.2, 1.0, 120, 100, 80,
            MarketDirectionType.NeutralUp,
            MarketVolatilityType.Normal,
            PriceDirectionType.Flat,
            PriceVolatilityType.Unknown,
            0.2, 500
        );
        var response = await api.GenerateFuturesRsiSignalAsync(eodData, TradeTimePeriodType.Daily, 14);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateFuturesRsiDailySignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var eodData = new FuturesEodDataV2ReadModel(
            "SYM20251010",
            DateOnly.FromDateTime(DateTime.Now),
            "SYM",
            100, 110, 90, 105, 1000,
            0.05, 1.2, 1.0, 120, 100, 80,
            MarketDirectionType.NeutralUp,
            MarketVolatilityType.Normal,
            PriceDirectionType.Flat,
            PriceVolatilityType.Unknown,
            0.2, 500
        );
        var response = await api.GenerateFuturesRsiDailySignalAsync(eodData, TradeTimePeriodType.Daily, 14);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task UpdateFuturesTradeSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var eodData = new FuturesEodDataV2ReadModel(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            "SYM",
            100, 110, 90, 105, 1000,
            0.05, 1.2, 1.0, 120, 100, 80,
            MarketDirectionType.NeutralUp,
            MarketVolatilityType.Normal,
            PriceDirectionType.Flat,
            PriceVolatilityType.Unknown,
            0.2, 500
        );
        var rsiSignal = new FuturesRsiSignalReadModel(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TradeTimePeriodType.None,
            14,
            TimeOnly.FromDateTime(DateTime.Now),
            105, 0, 0, 0, 0, 0, 0, 0, 0, 0
        );
        var tdiSignal = new FuturesTdiSignalReadModel(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TradeTimePeriodType.FifteenSeconds,
            TimeOnly.FromDateTime(DateTime.Now),
            1, 1,
            FuturesTrendDirectionType.Init,
            FuturesTrendDirectionStrengthType.Low
        );
        var itiSignal = new FuturesItiSignalDataReadModel(null, null, null);
        decimal vixFuturesPrice = 20m;
        var response = await api.UpdateFuturesTradeSignalAsync(eodData, rsiSignal, tdiSignal, itiSignal, vixFuturesPrice);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateFuturesTdiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var tdiSignalId = new FuturesTdiSignalId(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TimeOnly.FromDateTime(DateTime.Now)
        );
        var rsiSignals = new[] {
            new FuturesRsiSignalReadModel(
                "CONTRACT1",
                DateOnly.FromDateTime(DateTime.Now),
                TradeTimePeriodType.None,
                14,
                TimeOnly.FromDateTime(DateTime.Now),
                105, 0, 0, 0, 0, 0, 0, 0, 0, 0
            )
        };
        var response = await api.GenerateFuturesTdiSignalAsync(tdiSignalId, rsiSignals);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateFuturesItiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var contractId = "CONTRACT1";
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var timestamp = DateTime.Now;
        var timePeriod = TradeTimePeriodType.Weekly;
        double futuresPrice = 100;
        double vixFuturesPrice = 0;
        var response = await api.GenerateFuturesItiSignalAsync(contractId, valueDate, timePeriod, timestamp, futuresPrice, vixFuturesPrice);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task SetFuturesItiSignalHoldTrade_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var id = new FuturesItiSignalId("CONTRACT1", DateOnly.FromDateTime(DateTime.Now), TradeTimePeriodType.Weekly, DateTime.Now);
        var response = await api.SetFuturesItiSignalHoldTradeAsync(id);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ClearFuturesItiSignalHoldTrade_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var id = new FuturesItiSignalId("CONTRACT1", DateOnly.FromDateTime(DateTime.Now), TradeTimePeriodType.Weekly, DateTime.Now);
        var response = await api.ClearFuturesItiSignalHoldTradeAsync(id);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
