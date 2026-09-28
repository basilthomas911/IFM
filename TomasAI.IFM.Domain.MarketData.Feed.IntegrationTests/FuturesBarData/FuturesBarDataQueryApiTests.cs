using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.FuturesBarData;

public class FuturesBarDataQueryApiTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> factory, MarketDataFeedFixture dbFixture)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>, IClassFixture<MarketDataFeedFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetFuturesBarData_Ok()
    {
        // arrange...
        var futuresBarData = SampleData.FuturesBarData;
        await dbFixture.MarketDataDb.DeleteFuturesBarDataAsync(futuresBarData.Id);
        await dbFixture.MarketDataDb.InsertFuturesBarDataAsync(futuresBarData);

        // act...
        var marketDataFeedApi = new MarketDataFeedQueryApi(_actorProducer);
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
        var marketDataFeedApi = new MarketDataFeedQueryApi(_actorProducer);
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
