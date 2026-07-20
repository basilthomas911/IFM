using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesTdiSignalCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GenerateFuturesTdiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var futuresTdiSignalId = new FuturesTdiSignalId(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TimeOnly.FromDateTime(DateTime.Now));
        var futuresRsiSignals = new[] {
            new FuturesRsiSignalReadModel(
                contractId: "CONTRACT1",
                valueDate: DateOnly.FromDateTime(DateTime.Now),
                timePeriod: TradeTimePeriodType.OneMinute,
                periodLength: 14,
                timestamp: TimeOnly.FromDateTime(DateTime.Now),
                price: 4500m,
                priceChange: 0.5m,
                priceGain: 0.5m,
                priceLoss: 0.2m,
                averagePriceGain: 0.3m,
                averagePriceLoss: 1.1m,
                rs: 60.0,
                rsi: 59.0,
                rsiAverage: 0.3,
                rsiSlope: 30)
        };
        var response = await api.GenerateFuturesTdiSignalAsync(futuresTdiSignalId, futuresRsiSignals);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
