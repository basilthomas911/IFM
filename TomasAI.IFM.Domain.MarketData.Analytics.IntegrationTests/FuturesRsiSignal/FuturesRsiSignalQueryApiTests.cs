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
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesRsiSignal;

public class FuturesRsiSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    static FuturesRsiSignalReadModel CreateRsiSignal(
        TimeFrameType signalType,
        TimeOnly timestamp,
        decimal price,
        double rsi = 55.0,
        double rsiSlope = 0.1,
        int windowSize = 60)
        => new(
            contractId: SampleData.ContractId,
            valueDate: SampleData.ValueDate,
            timePeriod: signalType,
            periodLength: windowSize,
            timestamp: timestamp,
            price: price,
            priceChange: 0m,
            priceGain: 0m,
            priceLoss: 0m,
            averagePriceGain: 0m,
            averagePriceLoss: 0m,
            rs: 0.0,
            rsi: rsi,
            rsiAverage: 0.0,
            rsiSlope: rsiSlope);

    async Task SeedRsiSignalsAsync()
    {
        var baseTime = TimeOnly.FromDateTime(SampleData.Timestamp);

        // Default signal 1
        await dbFixture.MarketDataDb.InsertFuturesRsiSignalAsync(
            CreateRsiSignal(
                SampleData.TimePeriod,
                baseTime,
                (decimal)SampleData.FuturesPrice,
                rsi: 55.0,
                windowSize: SampleData.PeriodLength));

        // Default signal 2
        await dbFixture.MarketDataDb.InsertFuturesRsiSignalAsync(
            CreateRsiSignal(
                SampleData.TimePeriod,
                baseTime.AddMinutes(1),
                (decimal)SampleData.FuturesPrice + 10m,
                rsi: 58.0,
                windowSize: SampleData.PeriodLength));

        // Daily signal
        await dbFixture.MarketDataDb.InsertFuturesRsiSignalAsync(
            CreateRsiSignal(TimeFrameType.Daily, baseTime, (decimal)SampleData.FuturesPrice, rsi: 52.0, windowSize: 14));
    }

    [Fact]
    public async Task GetFuturesRsiSignal_Ok()
    {
        // arrange...
        await SeedRsiSignalsAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod, SampleData.PeriodLength);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TimePeriod.Should().Be(SampleData.TimePeriod);
    }

    [Fact]
    public async Task GetFuturesRsiSignal_BySignalType_Ok()
    {
        // arrange...
        await SeedRsiSignalsAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesRsiSignalAsync(SampleData.ContractId, SampleData.ValueDate, TimeFrameType.Daily, 14);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TimePeriod.Should().Be(TimeFrameType.Daily);
    }

    [Fact]
    public async Task GetFuturesTrendDirectionFromRSISignal_Ok()
    {
        // arrange...
        await SeedRsiSignalsAsync();

        var timestamp = SampleData.Timestamp;
        var startTime = timestamp.AddMinutes(-5);
        var endTime = timestamp.AddMinutes(5);
        var lookbackInterval = 5;

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesTrendDirectionFromRSISignalAsync(
            SampleData.ContractId,
            SampleData.ValueDate,
            SampleData.TimePeriod,
            SampleData.PeriodLength,
            timestamp,
            lookbackInterval,
            startTime,
            endTime);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue(response.ErrorMessage);
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
    }
}
