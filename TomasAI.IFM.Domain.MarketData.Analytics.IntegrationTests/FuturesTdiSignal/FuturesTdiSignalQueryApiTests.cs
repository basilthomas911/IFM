using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTdiSignal;

public class FuturesTdiSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GetFuturesTdiSignal_Ok()
    {
        var expectedSignal = new FuturesTdiSignalReadModel(
            contractId: SampleData.ContractId,
            valueDate: new DateOnly(2099, 12, 30),
            timePeriod: TradeTimePeriodType.Daily,
            timestamp: new TimeOnly(10, 0, 0),
            upTrendCount: 3,
            downTrendCount: 0,
            tdi: FuturesTrendDirectionType.UpTrending,
            tdiStrength: FuturesTrendDirectionStrengthType.High);

        await dbFixture.MarketDataDb.InsertFuturesTdiSignalAsync(expectedSignal);

        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesTdiSignalAsync(expectedSignal.ContractId, expectedSignal.ValueDate);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(expectedSignal.ContractId);
        response.Value.ValueDate.Should().Be(expectedSignal.ValueDate);
        response.Value.TimePeriod.Should().Be(expectedSignal.TimePeriod);
        response.Value.Timestamp.Should().Be(expectedSignal.Timestamp);
        response.Value.TDI.Should().Be(expectedSignal.TDI);
        response.Value.TDIStrength.Should().Be(expectedSignal.TDIStrength);
    }
}
