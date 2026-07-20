using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ServiceApi;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for MarketDataQueryApi endpoints.
/// </summary>
public class MarketDataQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of the currently traded futures contract.
    /// </summary>
    [Fact]
    public async Task GetCurrentlyTradedFuturesContract_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Ensure test data exists for a currently traded contract in the test DB or mock
        var response = await queryApi.GetCurrentlyTradedFuturesContractAsync("ES");
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<FuturesContractV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of all currently traded futures contracts.
    /// </summary>
    [Fact]
    public async Task GetCurrentlyTradedFuturesContracts_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Ensure test data exists for multiple currently traded contracts
        var response = await queryApi.GetCurrentlyTradedFuturesContractsAsync("ES");
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<FuturesContractV2ReadModel[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of a specific futures contract.
    /// </summary>
    [Fact]
    public async Task GetFuturesContract_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a known contractId from test data
        var contractId = "TEST_CONTRACT_1";
        var response = await queryApi.GetFuturesContractAsync(contractId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<FuturesContractV2ReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a futures contract symbol.
    /// </summary>
    [Fact]
    public async Task GetFuturesContractSymbol_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a known contractId from test data
        var contractId = "TEST_CONTRACT_1";
        var response = await queryApi.GetFuturesContractSymbolAsync(contractId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<string>();
    }

    /// <summary>
    /// Tests retrieval of a specific futures option contract.
    /// </summary>
    [Fact]
    public async Task GetFuturesOptionContract_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a known option contractId from test data
        var contractId = "TEST_OPTION_1";
        var response = await queryApi.GetFuturesOptionContractAsync(contractId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<FuturesOptionContractReadModel>();
    }

    /// <summary>
    /// Tests retrieval of all futures contracts.
    /// </summary>
    [Fact]
    public async Task GetFuturesContracts_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesContractsAsync();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<FuturesContractV2ReadModel[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of all futures option contracts for a symbol.
    /// </summary>
    [Fact]
    public async Task GetFuturesOptionContracts_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a known symbol from test data
        var symbol = "TEST_SYMBOL";
        var response = await queryApi.GetFuturesOptionContractsAsync(symbol);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<FuturesOptionContractReadModel[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of futures option contract IDs.
    /// </summary>
    [Fact]
    public async Task GetFuturesOptionContractIds_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use known option contractIds from test data
        var contractIds = new[] { "TEST_OPTION_1", "TEST_OPTION_2" };
        var response = await queryApi.GetFuturesOptionContractIdsAsync(contractIds);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<string[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of the last yield curve rate.
    /// </summary>
    [Fact]
    public async Task GetLastYieldCurveRate_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        var response = await queryApi.GetLastYieldCurveRateAsync();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<YieldCurveRateReadModel>();
    }

    /// <summary>
    /// Tests retrieval of the last rate of return for a symbol and value date.
    /// </summary>
    [Fact]
    public async Task GetLastRateOfReturn_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a known symbol and valueDate from test data
        var symbol = "TEST_SYMBOL";
        var valueDate = new DateOnly(2025, 1, 1);
        var response = await queryApi.GetLastRateOfReturnAsync(symbol, valueDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<RateOfReturnReadModel>();
    }

    /// <summary>
    /// Tests retrieval of trading days for a date range, market type, and currency type.
    /// </summary>
    [Fact]
    public async Task GetTradingDays_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a date range and types that exist in test data
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 12, 31);
        var marketType = MarketType.Equity;
        var currencyType = CurrencyType.USD;
        var response = await queryApi.GetTradingDaysAsync(startDate, endDate, marketType, currencyType);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<ScalarReadModel<int>>();
    }

    /// <summary>
    /// Tests retrieval of trading dates for a date range, market type, and currency type.
    /// </summary>
    [Fact]
    public async Task GetTradingDates_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a date range and types that exist in test data
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 12, 31);
        var marketType = MarketType.Equity;
        var currencyType = CurrencyType.USD;
        var response = await queryApi.GetTradingDatesAsync(startDate, endDate, marketType, currencyType);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<DateOnly[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of yield curve rates for a date range.
    /// </summary>
    [Fact]
    public async Task GetYieldCurveRates_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a date range that exists in test data
        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 12, 31);
        var response = await queryApi.GetYieldCurveRatesAsync(startDate, endDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<YieldCurveRateReadModel[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of external yield curve rates.
    /// </summary>
    [Fact]
    public async Task GetExternalYieldCurveRates_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        var response = await queryApi.GetExternalYieldCurveRatesAsync();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<YieldCurveRateReadModel[]>();
        response.Value.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests retrieval of yield curve rate years.
    /// </summary>
    [Fact]
    public async Task GetYieldCurveRateYears_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        var response = await queryApi.GetYieldCurveRateYearsAsync();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<YieldCurveRateYearsReadModel>();
    }

    /// <summary>
    /// Tests if a yield curve rate exists for a value date.
    /// </summary>
    [Fact]
    public async Task YieldCurveRateExists_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use a valueDate that exists in test data
        var valueDate = new DateOnly(2025, 1, 1);
        var response = await queryApi.YieldCurveRateExistsAsync(valueDate);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<ScalarReadModel<bool>>();
    }

    /// <summary>
    /// Tests retrieval of the value date.
    /// </summary>
    [Fact]
    public async Task GetValueDate_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        var response = await queryApi.GetValueDateAsync();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<ScalarReadModel<DateOnly>>();
    }

    /// <summary>
    /// Tests retrieval of iron condor market data.
    /// </summary>
    [Fact]
    public async Task GetIronCondorMarketData_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataQueryApi(queryServiceApi);
        // Use known contract IDs and date range from test data
        var response = await queryApi.GetIronCondorMarketDataAsync(
            "TEST_CONTRACT_1", "TEST_OPTION_1", "TEST_OPTION_2", "TEST_OPTION_3", "TEST_OPTION_4", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), MarketType.Equity, CurrencyType.USD);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().BeAssignableTo<IronCondorMarketDataReadModel>();
    }
}
