using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesItiSignal;

public class FuturesItiSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

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
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
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
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
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
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
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
}

