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

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesMacdSignal;

public class FuturesMacdSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    async Task SeedMacdSignalAsync()
    {
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;

        await dbFixture.MarketDataDb.InsertFuturesMacdSignalAsync(
            SampleData.CreateMacdSignalViewModel(FuturesTrendDirectionType.UpTrending, FuturesTrendDirectionStrengthType.Medium));
    }

    [Fact]
    public async Task GetFuturesMacdSignal_Ok()
    {
        // arrange...
        await SeedMacdSignalAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesMacdSignalAsync(
            SampleData.ContractId,
            SampleData.ValueDate,
            SampleData.TimePeriod,
            FuturesMacdConfiguration.ConventionalSignalEmaPeriod,
            FuturesMacdConfiguration.ConventionalFastEmaPeriod,
            FuturesMacdConfiguration.ConventionalSlowEmaPeriod);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.SignalEmaPeriod.Should().Be(9);
        response.Value.FastEmaPeriod.Should().Be(12);
        response.Value.SlowEmaPeriod.Should().Be(26);
        response.Value.MACD.Should().Be(FuturesTrendDirectionType.UpTrending);
        response.Value.MACDStrength.Should().Be(FuturesTrendDirectionStrengthType.Medium);
    }

    [Fact]
    public async Task MacdStorage_SeparatesSignalsByAllThreeEmaPeriods()
    {
        var standard = SampleData.CreateMacdSignalViewModel() with { MacdLine = 1.5 };
        var custom = standard with
        {
            SignalEmaPeriod = 7,
            FastEmaPeriod = 10,
            SlowEmaPeriod = 30,
            MacdLine = 2.5
        };

        await dbFixture.MarketDataDb.InsertFuturesMacdSignalAsync(standard);
        await dbFixture.MarketDataDb.InsertFuturesMacdSignalAsync(custom);

        var standardResult = await dbFixture.MarketDataDb.GetLastFuturesMacdSignalAsync(
            standard.ContractId,
            standard.ValueDate,
            standard.TimePeriod,
            standard.SignalEmaPeriod,
            standard.FastEmaPeriod,
            standard.SlowEmaPeriod);
        var customResult = await dbFixture.MarketDataDb.GetLastFuturesMacdSignalAsync(
            custom.ContractId,
            custom.ValueDate,
            custom.TimePeriod,
            custom.SignalEmaPeriod,
            custom.FastEmaPeriod,
            custom.SlowEmaPeriod);

        standardResult.Should().NotBeNull();
        customResult.Should().NotBeNull();
        standardResult!.MacdLine.Should().Be(1.5);
        customResult!.MacdLine.Should().Be(2.5);
        standardResult.EntityId.Format().Should().NotBe(customResult.EntityId.Format());
    }
}
