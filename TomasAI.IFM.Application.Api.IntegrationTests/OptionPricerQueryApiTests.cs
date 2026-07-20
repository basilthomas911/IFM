using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using Xunit;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for OptionPricerQueryApi endpoints.
/// </summary>
public class OptionPricerQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Tests retrieval of configured option pricer devices.
    /// </summary>
    [Fact]
    public async Task GetOptionPricerDevices_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionPricerQueryApi(queryServiceApi);

        var response = await queryApi.GetOptionPricerDevicesAsync();

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<OptionPricerDevicesReadModel>();
    }

    /// <summary>
    /// Tests retrieval of a spread distribution for a trade.
    /// </summary>
    [Fact]
    public async Task GetSpreadDistribution_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionPricerQueryApi(queryServiceApi);

        var response = await queryApi.GetSpreadDistributionAsync(1, TradeType.ShortPut, TradeStatus.Open, new DateOnly(2025, 10, 10), 30);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<SpreadDistributionReadModel>();
    }

    /// <summary>
    /// Tests whether a spread distribution job is reported as in progress.
    /// </summary>
    [Fact]
    public async Task IsSpreadDistributionJobInProgress_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new OptionPricerQueryApi(queryServiceApi);

        var response = await queryApi.IsSpreadDistributionJobInProgressAsync(1, 1);

        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<ScalarReadModel<bool>>();
    }
}
