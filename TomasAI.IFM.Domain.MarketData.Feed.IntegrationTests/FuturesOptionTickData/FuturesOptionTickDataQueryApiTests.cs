using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesOptionTickData;

public class FuturesOptionTickDataQueryApiTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>, IClassFixture<MarketDataFeedFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetLastFuturesOptionTickData_Ok()
    {
        // arrange...
        var optionTickData = SampleData.ShortOptionTickData;
        await dbFixture.MarketDataDb.DeleteFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);
        await dbFixture.MarketDataDb.InsertFuturesOptionTickDataAsync(optionTickData);

        // act...
        var marketDataFeedApi = new MarketDataFeedQueryApi(_actorProducer);
        var response = await marketDataFeedApi.GetLastFuturesOptionTickDataAsync(optionTickData.ContractId, optionTickData.ValueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
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
