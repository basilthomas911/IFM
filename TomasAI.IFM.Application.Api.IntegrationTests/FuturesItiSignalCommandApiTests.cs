using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesItiSignalCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GenerateFuturesItiSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var contractId = "CONTRACT1";
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var timePeriod = TradeTimePeriodType.Weekly;
        var timestamp = DateTime.Now;
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
