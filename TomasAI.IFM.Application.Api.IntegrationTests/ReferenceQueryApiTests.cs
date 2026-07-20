using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Reference;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class ReferenceQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of market data definition types.
    /// </summary>
    [Fact]
    public async Task GetMarketDataDefinitionTypes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetMarketDataDefinitionTypesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<LookupTypeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of reference data definition types.
    /// </summary>
    [Fact]
    public async Task GetReferenceDataDefinitionTypes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetReferenceDataDefinitionTypesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<LookupTypeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of system admin function types.
    /// </summary>
    [Fact]
    public async Task GetSystemAdminFunctionTypes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetSystemAdminFunctionTypesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<LookupTypeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of lookup types.
    /// </summary>
    [Fact]
    public async Task GetLookupTypes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetLookupTypesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<LookupTypeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of lookup types by name.
    /// </summary>
    [Fact]
    public async Task GetLookupTypesByName_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetLookupTypesAsync("Currency");

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<LookupTypeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of lookup type names.
    /// </summary>
    [Fact]
    public async Task GetLookupTypeNames_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetLookupTypeNamesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<string[]>();
    }

    /// <summary>
    /// Tests retrieval of lookup type short codes.
    /// </summary>
    [Fact]
    public async Task GetLookupTypeShortCodes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetLookupTypeShortCodesAsync("Currency");

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<LookupTypeShortCodeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of next seed id.
    /// </summary>
    [Fact]
    public async Task GetNextSeedId_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetNextSeedIdAsync("TestSeed");

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarReadModel<int>>();
    }

    /// <summary>
    /// Tests retrieval of current seed id.
    /// </summary>
    [Fact]
    public async Task GetCurrentSeedId_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetCurrentSeedIdAsync("TestSeed");

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarReadModel<int>>();
    }

    /// <summary>
    /// Tests retrieval of default futures contract definitions.
    /// </summary>
    [Fact]
    public async Task GetDefaultFuturesContractDefinitions_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetDefaultFuturesContractDefinitionsAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<DefaultFuturesContractDefinitionsReadModel>();
    }

    /// <summary>
    /// Tests retrieval of futures option strike price definitions.
    /// </summary>
    [Fact]
    public async Task GetFuturesOptionStrikePriceDefinitions_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetFuturesOptionStrikePriceDefinitionsAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesOptionStrikePriceReadModel>();
    }

    /// <summary>
    /// Tests lookup type short code exists check.
    /// </summary>
    [Fact]
    public async Task LookupTypeShortCodeExists_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.LookupTypeShortCodeExistsAsync("Currency", "USD");

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarReadModel<bool>>();
    }

    /// <summary>
    /// Tests retrieval of economic calendars for a date, view type and country.
    /// </summary>
    [Fact]
    public async Task GetEconomicCalendars_ByParams_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetEconomicCalendarAsync(DateTime.UtcNow, EconomicCalendarViewType.Today, "US");

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<EconomicCalendarReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of all economic calendars.
    /// </summary>
    [Fact]
    public async Task GetEconomicCalendars_All_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetEconomicCalendarsAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<EconomicCalendarReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of external economic calendars.
    /// </summary>
    [Fact]
    public async Task GetExternalEconomicCalendars_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetExternalEconomicCalendarsAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<EconomicCalendarReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of economic calendar date string.
    /// </summary>
    [Fact]
    public async Task GetEconomicCalendarDate_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetEconomicCalendarDateAsync(System.DateTime.UtcNow.Date, EconomicCalendarViewType.Today);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<string>();
    }

    /// <summary>
    /// Tests retrieval of economic calendar country codes.
    /// </summary>
    [Fact]
    public async Task GetEconomicCalendarCountryCodes_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetEconomicCalendarCountryCodesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<EconomicCalendarCountryCodeReadModel[]>();
    }

    /// <summary>
    /// Tests retrieval of MDI forward loss ratios.
    /// </summary>
    [Fact]
    public async Task GetMDIForwardLossRatios_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new ReferenceQueryApi(queryServiceApi);

        var response = await queryApi.GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType.UpTrend, TradeType.ShortIronCondor);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<MDIForwardLossRatioReadModel[]>();
    }
}
