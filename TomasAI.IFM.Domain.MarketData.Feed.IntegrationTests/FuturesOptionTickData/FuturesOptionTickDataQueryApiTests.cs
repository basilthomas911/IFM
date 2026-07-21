using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesOptionTickData;

public class FuturesOptionTickDataQueryApiTests(WebApplicationFactory<Program> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataFeedFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetLastFuturesOptionTickData_Ok()
    {
        // arrange...
        var optionTickData = SampleData.ShortOptionTickData;
        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(optionTickData);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var marketDataFeedApi = new MarketDataFeedQueryApi(queryServiceApi);
        var response = await marketDataFeedApi.GetLastFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();

        var tickDataRecord = response.Value;
        tickDataRecord.Should().NotBeNull();
        tickDataRecord!.ContractId.Should().Be(optionTickData.ContractId);
        tickDataRecord.ValueDate.Should().Be(optionTickData.ValueDate);
        tickDataRecord.BidPrice.Should().Be(optionTickData.BidPrice);
        tickDataRecord.AskPrice.Should().Be(optionTickData.AskPrice);
        tickDataRecord.BidSize.Should().Be(optionTickData.BidSize);
        tickDataRecord.AskSize.Should().Be(optionTickData.AskSize);
        tickDataRecord.ImpliedVolatility.Should().Be(optionTickData.ImpliedVolatility);
        tickDataRecord.Delta.Should().Be(optionTickData.Delta);
        tickDataRecord.Gamma.Should().Be(optionTickData.Gamma);
        tickDataRecord.Vega.Should().Be(optionTickData.Vega);
        tickDataRecord.Theta.Should().Be(optionTickData.Theta);
        tickDataRecord.Rho.Should().Be(optionTickData.Rho);
    }
}
