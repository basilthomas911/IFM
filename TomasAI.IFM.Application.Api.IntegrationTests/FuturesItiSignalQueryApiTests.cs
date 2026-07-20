using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesItiSignalQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GetFuturesItiSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.Weekly);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalV2ReadModel>();
    }

    [Fact]
    public async Task GetFuturesItiTrendDirectionChangedSignals_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiTrendDirectionChangedSignalsAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.Weekly);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFuturesItiSignalData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalDataAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.Weekly);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalDataReadModel>();
    }

    [Fact]
    public async Task GetFuturesItiSignalMDI_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalMDIAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFuturesItiSignalMDIByTrend_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalMDIByTrendAsync("ESU25", new DateOnly(2025, 9, 10), 1);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }
}
