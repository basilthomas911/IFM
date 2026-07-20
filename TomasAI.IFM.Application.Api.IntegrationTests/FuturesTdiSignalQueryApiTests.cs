using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesTdiSignalQueryApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GetFuturesTdiSignal_Ok()
    {
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var queryApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await queryApi.GetFuturesTdiSignalAsync("ESU25", new DateOnly(2025, 9, 10));
        response.Success.Should().BeTrue();
        response.Value.Should().BeAssignableTo<FuturesTdiSignalReadModel>();
    }
}
