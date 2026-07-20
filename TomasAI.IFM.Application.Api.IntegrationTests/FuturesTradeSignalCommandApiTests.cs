using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesTradeSignalCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

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
}
