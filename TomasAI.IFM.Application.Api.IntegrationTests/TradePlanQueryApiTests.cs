using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.TradePlan.ViewModels;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class TradePlanQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _json_serializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of the last iron condor stop loss limit for an order/trade.
    /// </summary>
    [Fact]
    public async Task GetStopLossLimit_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _json_serializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new TradePlanQueryApi(queryServiceApi);
        int orderId = 1;
        int tradeId = 1;

        // act
        var response = await queryApi.GetIronCondorStopLossLimitAsync(orderId, tradeId);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePlanStopLossLimitReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a range of forward loss ratios for trade plans.
    /// </summary>
    [Fact]
    public async Task GetTradePlanForwardLossRatios_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _json_serializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new TradePlanQueryApi(queryServiceApi);
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2020, 12, 31);

        // act
        var response = await queryApi.GetIronCondorTradePlanForwardLossRatiosAsync(startDate, endDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePlanForwardLossRatioReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of forward loss ratio for a specific value date.
    /// </summary>
    [Fact]
    public async Task GetTradePlanForwardLossRatio_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _json_serializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new TradePlanQueryApi(queryServiceApi);
        var valueDate = new System.DateOnly(2020, 1, 1);

        // act
        var response = await queryApi.GetIronCondorTradePlanForwardLossRatioAsync(valueDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<TradePlanForwardLossRatioReadModel>();
    }

    /// <summary>
    /// Tests retrieval of trade plans for a specific order/trade and value date.
    /// </summary>
    [Fact]
    public async Task GetTradePlans_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _json_serializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new TradePlanQueryApi(queryServiceApi);
        int orderId = 1;
        int tradeId = 1;
        var valueDate = new System.DateOnly(2020, 1, 1);

        // act
        var response = await queryApi.GetIronCondorTradePlansAsync(orderId, tradeId, valueDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<IronCondorTradePlanReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of iron condor forward delta for a specific value date and trade type.
    /// </summary>
    [Fact]
    public async Task GetIronCondorForwardDelta_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _json_serializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new TradePlanQueryApi(queryServiceApi);
        var vixContractId = "VX20251010";
        var valueDate = new DateOnly(2020, 1, 1);
        var tradeType = TradeType.ShortIronCondor;
        var riskPositionType = RiskPositionType.Medium;

        // act
        var response = await queryApi.GetIronCondorForwardDeltaAsync(vixContractId, valueDate, tradeType, riskPositionType);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<IronCondorForwardDeltaDataModel>();
    }
}
