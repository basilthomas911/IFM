using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

public class FuturesItiSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly IActorProducer _actorProducer = factory.Services.GetRequiredService<IActorProducer>();

    async Task SeedItiSignalAsync()
    {
        var contractId = SampleData.ContractId;
        var valueDate = SampleData.ValueDate;
        var timePeriod = SampleData.TimePeriod;

        await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(contractId, valueDate, timePeriod);

        var signal = new FuturesItiSignalV2ReadModel(
            contractId: contractId,
            valueDate: valueDate,
            timePeriod: timePeriod,
            sequenceId: 0,
            intrinsicTime: SampleData.Timestamp,
            intrinsicTimeGroupId: 0,
            intrinsicTimeLength: 0,
            intrinsicPrice: SampleData.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: SampleData.FuturesPrice,
            trendExtreme: SampleData.FuturesPrice,
            trendReversal: SampleData.FuturesPrice,
            trendDelta: SampleData.PredictedDelta + ((SampleData.FuturesPrice * SampleData.Lambda) / 2),
            targetDelta: SampleData.FuturesPrice * SampleData.Lambda,
            lambda: SampleData.Lambda,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: SampleData.FuturesPrice,
            downTrendTrigger: SampleData.FuturesPrice - (SampleData.FuturesPrice * SampleData.Lambda),
            tradeState: IntrinsicTimeTradeState.Ready);

        await dbFixture.MarketDataDb.InsertFuturesItiSignalAsync(signal);
    }

    [Fact]
    public async Task GetFuturesItiSignalData_Ok()
    {
        // arrange...
        await SeedItiSignalAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesItiSignalDataAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.TrendDirectionChange.Should().NotBeNull();
        response.Value.TrendDirectionChange!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.TrendDirectionChange.ValueDate.Should().Be(SampleData.ValueDate);
    }

    [Fact]
    public async Task GetLastFuturesItiSignal_Ok()
    {
        // arrange...
        await SeedItiSignalAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesItiSignalAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value!.ContractId.Should().Be(SampleData.ContractId);
        response.Value.ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.TimePeriod.Should().Be(SampleData.TimePeriod);
        response.Value.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.UpTrend);
        response.Value.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        response.Value.TradeState.Should().Be(IntrinsicTimeTradeState.Ready);
    }

    [Fact]
    public async Task GetFuturesItiTrendDirectionChangedSignals_Ok()
    {
        // arrange...
        await SeedItiSignalAsync();

        // act...
        var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
        var response = await analyticsApi.GetFuturesItiTrendDirectionChangedSignalsAsync(SampleData.ContractId, SampleData.ValueDate, SampleData.TimePeriod);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value!.First().ContractId.Should().Be(SampleData.ContractId);
        response.Value.First().ValueDate.Should().Be(SampleData.ValueDate);
        response.Value.First().IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
    }

    [Fact]
    public async Task GetFuturesItiSignalHistory_ReturnsCompleteWeeklyWindowInOrder()
    {
        const string contractId = "ES-HISTORY-API";
        var monday = new DateOnly(2026, 9, 7);
        var wednesday = monday.AddDays(2);
        var seeded = new[]
        {
            CreateHistorySignal(contractId, wednesday, TimeFrameType.Weekly, 2, hour: 15),
            CreateHistorySignal(contractId, monday, TimeFrameType.Weekly, 1, hour: 13),
            CreateHistorySignal(contractId, wednesday, TimeFrameType.Daily, 3, hour: 14)
        };

        try
        {
            foreach (var signal in seeded)
            {
                await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(
                    signal.ContractId,
                    signal.ValueDate,
                    signal.TimePeriod);
                await dbFixture.MarketDataDb.InsertFuturesItiSignalAsync(signal);
            }

            var analyticsApi = new MarketDataAnalyticsQueryApi(_actorProducer);
            var response = await analyticsApi.GetFuturesItiSignalHistoryAsync(
                contractId,
                wednesday,
                TimeFrameType.Weekly);

            response.Success.Should().BeTrue();
            response.Value.Should().NotBeNull();
            response.Value!.Select(signal => signal.SequenceId).Should().Equal(1, 2);
            response.Value.Should().OnlyContain(signal => signal.TimePeriod == TimeFrameType.Weekly);
        }
        finally
        {
            foreach (var signal in seeded)
            {
                await dbFixture.MarketDataDb.DeleteFuturesItiSignalAsync(
                    signal.ContractId,
                    signal.ValueDate,
                    signal.TimePeriod);
            }
        }
    }

    static FuturesItiSignalV2ReadModel CreateHistorySignal(
        string contractId,
        DateOnly valueDate,
        TimeFrameType timePeriod,
        long sequenceId,
        int hour)
        => new(
            contractId,
            valueDate,
            timePeriod,
            sequenceId,
            valueDate.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc),
            0,
            0,
            5_000 + sequenceId,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeModeType.Trending,
            5_000,
            5_001,
            5_000,
            1,
            1,
            0.003,
            5,
            10,
            5_010,
            4_990,
            IntrinsicTimeTradeState.Ready,
            timeFrameStartValueDate: valueDate);
}

