using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using Xunit;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for MarketDataFeedQueryApi endpoints.
/// </summary>
public class MarketDataFeedQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of the last futures tick data for a contract and value date.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTickData_ByValueDate_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetLastFuturesTickDataAsync("ES20251010", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTickDataV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the last futures tick data by tick date/time.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesTickData_ByTickDate_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetLastFuturesTickDataAsync("ES20251010", DateTime.UtcNow);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTickDataV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the last futures option tick data for a contract and value date.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesOptionTickData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetLastFuturesOptionTickDataAsync("ES20251010C4500", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesOptionTickDataV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures EOD data for a contract and value date.
    /// </summary>
    [Fact]
    public async Task GetFuturesEodData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetFuturesEodDataAsync("ES20251010", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesEodDataV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the latest futures EOD data.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesEodData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetLastFuturesEodDataAsync("ES20251010", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesEodDataV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures EOD data by date range.
    /// </summary>
    [Fact]
    public async Task GetFuturesEodDataByDateRange_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetFuturesEodDataAsync("ES20251010", new DateOnly(2025, 10, 9), new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesEodDataV2ReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of futures bar data for a contract / symbol within a time range.
    /// </summary>
    [Fact]
    public async Task GetFuturesBarData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var start = DateTime.UtcNow.AddMinutes(-10);
        var end = DateTime.UtcNow;
        var response = await queryApi.GetFuturesBarDataAsync("ES20251010", "ES", new DateOnly(2025, 10, 10), start, end);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesBarDataReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of the last futures bar data.
    /// </summary>
    [Fact]
    public async Task GetLastFuturesBarData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetLastFuturesBarDataAsync("ES20251010", "ES", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesBarDataReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of iron condor market data feed for given legs and value date.
    /// </summary>
    [Fact]
    public async Task GetIronCondorMarketDataFeed_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetIronCondorMarketDataFeedAsync(
            "ES20251010",
            "ES20251010P4450",
            "ES20251010P4400",
            "ES20251010C4550",
            "ES20251010C4600",
            new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<IronCondorMarketDataFeedReadModel?>();
    }

    /// <summary>
    /// Tests retrieval of futures EOD data parameters.
    /// </summary>
    [Fact]
    public async Task GetFuturesEodDataParameters_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetFuturesEodDataParametersAsync("ES20251010", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesEodDataParametersReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a futures option contract via the market data feed API.
    /// </summary>
    [Fact]
    public async Task GetFuturesOptionContract_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var qf = new FuturesOptionContractReadModel(
            contractId: "ES20251010C4500",
            description: "ES Oct 10 2025 Call 4500",
            symbol: "ES",
            localSymbol: "ES 20251010 C4500",
            securityType: "OPT",
            currency: "USD",
            exchange: "GLOBEX",
            multiplier: "50",
            contractMonth: new DateOnly(2025, 10, 10),
            strikePrice: 4500,
            optionType: "CALL");

        var response = await queryApi.GetFuturesOptionContractAsync("ES20251010C4500", qf);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesOptionContractReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures option spread data.
    /// </summary>
    [Fact]
    public async Task GetFuturesOptionSpreadData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var shortQ = new FuturesOptionContractReadModel(
            contractId: "ES20251010P4450",
            description: "Short Put",
            symbol: "ES",
            localSymbol: "ES 20251010 P4450",
            securityType: "OPT",
            currency: "USD",
            exchange: "GLOBEX",
            multiplier: "50",
            contractMonth: new DateOnly(2025, 10, 10),
            strikePrice: 4450,
            optionType: "PUT");

        var longQ = shortQ with { ContractId = "ES20251010P4400", StrikePrice = 4400, LocalSymbol = "ES 20251010 P4400" };

        var response = await queryApi.GetFuturesOptionSpreadDataAsync(
            new DateOnly(2025, 10, 10),
            new DateOnly(2025, 10, 10),
            assetPrice: 4500.25,
            riskFreeRate: 0.01,
            timeValue: 30.0,
            qfShortOptionContract: shortQ,
            qfLongOptionContract: longQ);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesOptionSpreadDataReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the normal curve table.
    /// </summary>
    [Fact]
    public async Task GetNormalCurveTable_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetNormalCurveTableAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<NormalCurveTableReadModel>();
    }

    /// <summary>
    /// Tests retrieval of VIX futures EOD data.
    /// </summary>
    [Fact]
    public async Task GetVixFuturesEodData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetVixFuturesEodDataAsync("VIX20251010", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<VixFuturesEodDataReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of the last VIX futures EOD data.
    /// </summary>
    [Fact]
    public async Task GetLastVixFuturesEodData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetLastVixFuturesEodDataAsync("VIX20251010", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<VixFuturesEodDataReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures risk position type for a value date and trade type.
    /// </summary>
    [Fact]
    public async Task GetFuturesRiskPositionType_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetFuturesRiskPositionTypeAsync(new DateOnly(2025, 10, 10), TradeType.ShortPut);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<RiskPositionTypeReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures EOD moving averages.
    /// </summary>
    [Fact]
    public async Task GetFuturesEodMovingAverages_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetFuturesEodMovingAveragesAsync("ES20251010", "ES", new DateOnly(2025, 10, 10));

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesEodDataMovingAveragesReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a streaming request id.
    /// </summary>
    [Fact]
    public async Task GetStreamingRequestId_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetStreamingRequestIdAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarValue<int>>();
    }

    /// <summary>
    /// Tests retrieval of an option quote id.
    /// </summary>
    [Fact]
    public async Task GetOptionQuoteId_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataFeedQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionQuoteIdAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarValue<int>>();
    }
}
