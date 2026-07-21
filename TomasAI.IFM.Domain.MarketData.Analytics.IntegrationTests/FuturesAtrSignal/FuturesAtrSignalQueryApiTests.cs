using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesAtrSignal;

public class FuturesAtrSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    async Task SeedAtrSignalAsync()
    {
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        await dbFixture.MarketDataDb.DeleteFuturesAtrSignalAsync(contractId, valueDate);

        await dbFixture.MarketDataDb.InsertFuturesAtrSignalAsync(
            SampleData.CreateAtrSignalViewModel(FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium));
    }

    [Fact]
    public async Task GetFuturesAtrSignal_Ok()
    {
        // arrange...
        await SeedAtrSignalAsync();

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesAtrSignalAsync(SampleData.ContractId, SampleData.ValueDate, TradeTimePeriodType.Daily, SampleData.PeriodLength);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.ATR.Should().Be(FuturesTrendDirectionType.UpTrending);
        response.Value.ATRStrength.Should().Be(FuturesTrendDirectionStrengthType.Medium);
    }
}
