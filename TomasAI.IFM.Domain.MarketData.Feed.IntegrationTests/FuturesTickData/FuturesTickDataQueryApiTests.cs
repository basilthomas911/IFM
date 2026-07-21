using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesTickData;

public class FuturesTickDataQueryApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetLastFuturesTickData_Ok()
    {
        // arrange...
        var tickData = SampleData.UnderlyingFuturesTickData;
        await dbFixture.MarketDataDb.DeleteFuturesTickDataAsync(tickData.ContractId, tickData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesTickDataAsync(tickData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetLastFuturesTickDataAsync(tickData.ContractId, tickData.ValueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();

        var tickDataRecord = response.Value;
        tickDataRecord.Should().NotBeNull();
        tickDataRecord!.ContractId.Should().Be(tickData.ContractId);
        tickDataRecord.ValueDate.Should().Be(tickData.ValueDate);
        tickDataRecord.Price.Should().Be(tickData.Price);
        tickDataRecord.Size.Should().Be(tickData.Size);
    }

    [Fact]
    public async Task GetLastFuturesTickDataByTickDate_Ok()
    {
        // arrange...
        var tickData = SampleData.UnderlyingFuturesTickData;
        await dbFixture.MarketDataDb.DeleteFuturesTickDataAsync(tickData.ContractId, tickData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesTickDataAsync(tickData);
        var tickDate = tickData.ValueDate.ToDateTime(tickData.TickTime);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetLastFuturesTickDataAsync(tickData.ContractId, tickDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();

        var tickDataRecord = response.Value;
        tickDataRecord.Should().NotBeNull();
        tickDataRecord!.ContractId.Should().Be(tickData.ContractId);
        tickDataRecord.Price.Should().Be(tickData.Price);
        tickDataRecord.Size.Should().Be(tickData.Size);
    }
}
