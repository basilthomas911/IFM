using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FundQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of all funds.
    /// </summary>
    [Fact]
    public async Task GetFunds_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);

        // act
        var response = await queryApi.GetFundsAsync();

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of all fund orders.
    /// </summary>
    [Fact]
    public async Task GetFundOrders_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);

        // act
        var response = await queryApi.GetFundOrdersAsync();

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundOrderReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of all fund order trades.
    /// </summary>
    [Fact]
    public async Task GetFundOrderTrades_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);

        // act
        var response = await queryApi.GetFundOrderTradesAsync();

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundOrderTradeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of fund transactions for a specific fund and date range.
    /// </summary>
    [Fact]
    public async Task GetFundTransactions_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2020, 12, 31);

        // act
        var response = await queryApi.GetFundTransactionsAsync(fundId, startDate, endDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundTransactionReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of the current fund balance for a specific fund.
    /// </summary>
    [Fact]
    public async Task GetFundBalance_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;

        // act
        var response = await queryApi.GetFundBalanceAsync(fundId);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundBalanceReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the opening fund balance for a specific fund and value date.
    /// </summary>
    [Fact]
    public async Task GetOpeningFundBalance_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;
        var valueDate = new DateOnly(2020, 1, 1);

        // act
        var response = await queryApi.GetOpeningFundBalanceAsync(fundId, valueDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundBalanceReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the closing fund balance for a specific fund and value date.
    /// </summary>
    [Fact]
    public async Task GetClosingFundBalance_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;
        var valueDate = new DateOnly(2020, 12, 31);

        // act
        var response = await queryApi.GetClosingFundBalanceAsync(fundId, valueDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundBalanceReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the fund PnL report for a specific fund and date range.
    /// </summary>
    [Fact]
    public async Task GetFundPnlReport_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2020, 12, 31);

        // act
        var response = await queryApi.GetFundPnlReportAsync(fundId, startDate, endDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundPnlReportReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the fund ID from an order ID.
    /// </summary>
    [Fact]
    public async Task GetFundIdFromOrderId_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int orderId = 1;

        // act
        var response = await queryApi.GetFundIdFromOrderIdAsync(orderId);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarReadModel<int>>();
    }

    /// <summary>
    /// Tests retrieval of the fund win/loss ratio for a specific fund and date range.
    /// </summary>
    [Fact]
    public async Task GetFundWinLossRatio_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2020, 12, 31);

        // act
        var response = await queryApi.GetFundWinLossRatioAsync(fundId, startDate, endDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundWinLossRatioReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the fund drawdown balances for a specific fund and date range.
    /// </summary>
    [Fact]
    public async Task GetFundDrawdownBalances_Ok()
    {
        // arrange
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new FundQueryApi(queryServiceApi);
        int fundId = 1;
        var startDate = new DateOnly(2020, 1, 1);
        var endDate = new DateOnly(2020, 12, 31);

        // act
        var response = await queryApi.GetFundDrawdownBalancesAsync(fundId, startDate, endDate);

        // assert
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FundDrawdownBalancesReadModel>();
    }
}
