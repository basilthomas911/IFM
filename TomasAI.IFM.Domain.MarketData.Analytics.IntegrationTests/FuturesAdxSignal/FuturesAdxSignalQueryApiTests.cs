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

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesAdxSignal;

public class FuturesAdxSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    async Task SeedAdxSignalAsync()
    {
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        await dbFixture.MarketDataDb.DeleteFuturesAdxSignalAsync(contractId, valueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        await dbFixture.MarketDataDb.InsertFuturesAdxSignalAsync(
            SampleData.CreateAdxSignalViewModel(FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium));
    }

    [Fact]
    public async Task GetFuturesAdxSignal_Ok()
    {
        // arrange...
        await SeedAdxSignalAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesAdxSignalAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.ADX.Should().Be(FuturesTrendDirectionType.UpTrending);
        response.Value.ADXStrength.Should().Be(FuturesTrendDirectionStrengthType.Medium);
    }
}
