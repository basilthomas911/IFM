using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for MarketDataAnalyticsQueryApi endpoints.
/// </summary>
public class MarketDataAnalyticsQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of a futures trade signal.
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
    /// Tests retrieval of a futures trade signal by symbol.
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
    /// Tests retrieval of futures trade signal IDs.
    /// </summary>
    [Fact]
    public async Task GetFuturesTradeSignalIds_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesTradeSignalIdsAsync(new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTradeSignalId[]>();
    }

    /// <summary>
    /// Tests retrieval of a futures RSI signal (default type).
    /// </summary>
    [Fact]
    public async Task GetFuturesRsiSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesRsiSignalAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.OneMinute, 14);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesRsiSignalReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a futures RSI signal with a specific signal type.
    /// </summary>
    [Fact]
    public async Task GetFuturesRsiSignal_WithType_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesRsiSignalAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.OneMinute, 14);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesRsiSignalReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures trend direction from RSI signal.
    /// </summary>
    [Fact]
    public async Task GetFuturesTrendDirectionFromRSISignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var now = DateTime.UtcNow;
        var response = await queryApi.GetFuturesTrendDirectionFromRSISignalAsync(
            "ESU25", new DateOnly(2025, 9, 10), now, 30, now.AddHours(-1), now);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTrendDirectionReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a futures TDI signal.
    /// </summary>
    [Fact]
    public async Task GetFuturesTdiSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesTdiSignalAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTdiSignalReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a futures ITI signal.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.Weekly);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures ITI trend direction changed signals.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiTrendDirectionChangedSignals_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiTrendDirectionChangedSignalsAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.Weekly);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalV2ReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of futures ITI signal data.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalDataAsync("ESU25", new DateOnly(2025, 9, 10), TradeTimePeriodType.Weekly);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalDataReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures ITI MDI distribution.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiMDIDistribution_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiMDIDistributionAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiMDIDistributionReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures ITI MDI distribution by trend.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiMDIDistributionByTrend_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiMDIDistributionByTrendAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiMDIDistributionReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures ITI signal MDI.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalMDI_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalMDIAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalMDIV2ReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of futures ITI signal MDI by trend.
    /// </summary>
    [Fact]
    public async Task GetFuturesItiSignalMDIByTrend_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesItiSignalMDIByTrendAsync("ESU25", new DateOnly(2025, 9, 10), 1);
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesItiSignalMDIV2ReadModel[]>();
    }
}
