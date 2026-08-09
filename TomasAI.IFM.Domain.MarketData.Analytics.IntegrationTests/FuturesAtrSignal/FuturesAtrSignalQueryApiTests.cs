using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesAtrSignal;

public class FuturesAtrSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    async Task SeedAtrSignalAsync()
    {
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        await dbFixture.MarketDataDb.DeleteFuturesAtrSignalAsync(contractId, valueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        await dbFixture.MarketDataDb.InsertFuturesAtrSignalAsync(
            SampleData.CreateAtrSignalViewModel(FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium));
    }

    [Fact]
    public async Task GetFuturesAtrSignal_Ok()
    {
        // arrange...
        await SeedAtrSignalAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesAtrSignalAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

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
