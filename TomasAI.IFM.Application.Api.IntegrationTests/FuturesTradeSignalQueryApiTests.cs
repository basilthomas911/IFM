using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for FuturesTradeSignal query endpoints.
/// </summary>
public class FuturesTradeSignalQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of a futures trade signal by contract ID and value date.
    /// </summary>
    [Fact]
    public async Task GetFuturesTradeSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesTradeSignalAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTradeSignalV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the last futures trade signal.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTradeSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetLastFuturesTradeSignalAsync();
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTradeSignalV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a futures trade signal by symbol and value date.
    /// </summary>
    [Fact]
    public async Task GetFuturesTradeSignalBySymbol_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesTradeSignalBySymbolAsync("ES", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTradeSignalV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures trade signal IDs for a value date.
    /// </summary>
    [Fact]
    public async Task GetFuturesTradeSignalIds_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesTradeSignalIdsAsync(new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }
}
