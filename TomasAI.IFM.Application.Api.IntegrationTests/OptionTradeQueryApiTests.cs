using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using Xunit;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for option trade query API endpoints.
/// </summary>
public class OptionTradeQueryApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Gets trade history for an order.
    /// </summary>
    [Fact]
    public async Task GetTradeHistory_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradeHistoryAsync(1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradeHistoryReadModel[]>();
    }

    /// <summary>
    /// Gets option leg contract ids.
    /// </summary>
    [Fact]
    public async Task GetOptionLegContractIds_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionLegContractIdsAsync(1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<string[]>();
    }

    /// <summary>
    /// Gets trade limit for a trade.
    /// </summary>
    [Fact]
    public async Task GetTradeLimit_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradeLimitAsync(1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradeLimitReadModel>();
    }

    /// <summary>
    /// Gets trade type limit for a trade.
    /// </summary>
    [Fact]
    public async Task GetTradeTypeLimit_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradeTypeLimitAsync(1, TradeType.ShortIronCondor);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradeTypeLimitReadModel>();
    }

    /// <summary>
    /// Gets trade quantity.
    /// </summary>
    [Fact]
    public async Task GetTradeQuantity_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradeQuantityAsync(1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarReadModel<int>>();
    }

    /// <summary>
    /// Gets an option trade for an order and trade.
    /// </summary>
    [Fact]
    public async Task GetOptionTrade_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionTradeAsync(1, 1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<OptionTradeReadModel>();
    }

    /// <summary>
    /// Gets option trade spread data.
    /// </summary>
    [Fact]
    public async Task GetOptionTradeSpreadData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionTradeSpreadDataAsync(1, 1, TradeType.ShortIronCondor, new DateOnly(2025,10,10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<OptionTradeSpreadsDataModel>();
    }

    /// <summary>
    /// Gets option trade spread bar data for a given range.
    /// </summary>
    [Fact]
    public async Task GetOptionTradeSpreadBarData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionTradeSpreadBarDataAsync(1, 1, TradeType.ShortIronCondor, new DateOnly(2025,10,10), System.DateTime.UtcNow.AddDays(-1), System.DateTime.UtcNow);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<OptionTradeSpreadBarsDataModel[]>();
    }

    /// <summary>
    /// Gets all option trades for an order.
    /// </summary>
    [Fact]
    public async Task GetOptionTrades_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionTradesAsync(1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<OptionTradeReadModel[]>();
    }

    /// <summary>
    /// Gets trade positions for an order and trade.
    /// </summary>
    [Fact]
    public async Task GetTradePositions_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradePositionsAsync(1, 1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePositionReadModel[]>();
    }

    /// <summary>
    /// Gets a single trade position.
    /// </summary>
    [Fact]
    public async Task GetTradePosition_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradePositionAsync(1, 1, TradeType.ShortIronCondor, new DateOnly(2025,10,10), 30, TradeStatus.Open);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePositionReadModel>();
    }

    /// <summary>
    /// Gets iron condor trade price for a trade and date.
    /// </summary>
    [Fact]
    public async Task GetIronCondorTradePrice_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetIronCondorTradePriceAsync(1, new DateOnly(2025,10,10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePriceReadModel>();
    }

    /// <summary>
    /// Gets trade plan summary for an order/trade.
    /// </summary>
    [Fact]
    public async Task GetTradePlanSummary_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradePlanSummaryAsync(1, 1, new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePlanActionReadModel[]>();
    }

    /// <summary>
    /// Gets trade position trade types.
    /// </summary>
    [Fact]
    public async Task GetTradePositionTradeTypes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetTradePositionTradeTypesAsync(1, 1, new DateOnly(2025,10,10), 30, TradeStatus.Open);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<string[]>();
    }

    /// <summary>
    /// Gets iron condor MDI limit for a trade and date.
    /// </summary>
    [Fact]
    public async Task GetIronCondorMDILimit_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionTradeQueryApi(queryServiceApi);

        var response = await queryApi.GetIronCondorMDILimitAsync(1, 1, new DateOnly(2025,10,10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<IronCondorMDILimitDataModel>();
    }
}
