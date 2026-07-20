using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesBarData;

public class FuturesBarDataQueryApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetFuturesBarData_Ok()
    {
        // arrange...
        var futuresBarData = SampleData.FuturesBarData;
        await dbFixture.MarketDataDb.DeleteFuturesBarDataAsync(futuresBarData.Id);
        await dbFixture.MarketDataDb.InsertFuturesBarDataAsync(futuresBarData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetFuturesBarDataAsync(
            futuresBarData.ContractId, futuresBarData.Symbol, futuresBarData.ValueDate,
            futuresBarData.BarDate.AddMinutes(-1), futuresBarData.BarDate.AddMinutes(1));

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNullOrEmpty();

        var barDataRecord = response.Value!.First(e => e.ContractId == futuresBarData.ContractId);
        barDataRecord.Should().NotBeNull();
        barDataRecord.ContractId.Should().Be(futuresBarData.ContractId);
        barDataRecord.Symbol.Should().Be(futuresBarData.Symbol);
        barDataRecord.ValueDate.Should().Be(futuresBarData.ValueDate);
        barDataRecord.BarValue.Should().Be(futuresBarData.BarValue);
        barDataRecord.BarRateType.Should().Be(futuresBarData.BarRateType);
    }

    [Fact]
    public async Task GetLastFuturesBarData_Ok()
    {
        // arrange...
        var futuresBarData = SampleData.FuturesBarData;
        await dbFixture.MarketDataDb.DeleteFuturesBarDataAsync(futuresBarData.Id);
        await dbFixture.MarketDataDb.InsertFuturesBarDataAsync(futuresBarData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetLastFuturesBarDataAsync(
            futuresBarData.ContractId, futuresBarData.Symbol, futuresBarData.ValueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();

        var barDataRecord = response.Value;
        barDataRecord.Should().NotBeNull();
        barDataRecord.ContractId.Should().Be(futuresBarData.ContractId);
        barDataRecord.Symbol.Should().Be(futuresBarData.Symbol);
        barDataRecord.ValueDate.Should().Be(futuresBarData.ValueDate);
        barDataRecord.BarValue.Should().Be(futuresBarData.BarValue);
        barDataRecord.BarRateType.Should().Be(futuresBarData.BarRateType);
    }
}
