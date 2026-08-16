using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTdiSignal;

public class FuturesTdiSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    [Fact]
    public async Task GetFuturesTdiSignal_Ok()
    {
        var expectedSignal = new FuturesTdiSignalReadModel(
            SampleData.ContractId,
            new DateOnly(2099, 12, 30),
            TimeFrameType.OneMinute,
            new TimeOnly(10, 0, 0),
            FuturesTdiConfiguration.Standard,
            5500m,
            62d,
            61d,
            58d,
            55d,
            70d,
            40d,
            FuturesTrendDirectionType.UpTrending,
            FuturesTrendDirectionStrengthType.Medium,
            FuturesTdiCrossType.None,
            FuturesTdiMarketStateType.AboveMidline);

        await dbFixture.MarketDataDb.InsertFuturesTdiSignalAsync(expectedSignal);

        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesTdiSignalAsync(
            expectedSignal.ContractId,
            expectedSignal.ValueDate,
            expectedSignal.TimePeriod,
            expectedSignal.ConfigurationId);

        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(expectedSignal.ContractId);
        response.Value.ValueDate.Should().Be(expectedSignal.ValueDate);
        response.Value.TimePeriod.Should().Be(expectedSignal.TimePeriod);
        response.Value.Timestamp.Should().Be(expectedSignal.Timestamp);
        response.Value.TDI.Should().Be(expectedSignal.TDI);
        response.Value.TDIStrength.Should().Be(expectedSignal.TDIStrength);
    }
}
