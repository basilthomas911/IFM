using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests.FuturesTradeSignal;

public class FuturesTradeSignalQueryApiTests(WebApplicationFactory<Program> factory, MarketDataAnalyticsFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<MarketDataAnalyticsFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GetFuturesTradeSignal_Ok()
    {
        // arrange...
        var contractId = $"{SampleData.ContractId}-Q1";
        var valueDate = new DateOnly(2099, 1, 2);
        await dbFixture.MarketDataDb.InsertFuturesTradeSignalAsync(CreateTradeSignalViewModel(contractId, valueDate, 1));

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesTradeSignalAsync(contractId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.ContractId.Should().Be(contractId);
        response.Value.ValueDate.Should().Be(valueDate);
    }

    [Fact]
    public async Task GetLastFuturesTradeSignal_Ok()
    {
        // arrange...
        var contractId = $"{SampleData.ContractId}-Q2";
        var valueDate = new DateOnly(2099, 1, 3);
        await dbFixture.MarketDataDb.InsertFuturesTradeSignalAsync(CreateTradeSignalViewModel(contractId, valueDate, 2));

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetLastFuturesTradeSignalAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.ContractId.Should().Be(contractId);
        response.Value.ValueDate.Should().Be(valueDate);
    }

    [Fact]
    public async Task GetFuturesTradeSignalIds_Ok()
    {
        // arrange...
        var valueDate = new DateOnly(2099, 1, 4);
        var contractId = $"{SampleData.ContractId}-Q3";
        await dbFixture.MarketDataDb.InsertFuturesTradeSignalAsync(CreateTradeSignalViewModel(contractId, valueDate, 3));

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var analyticsApi = new MarketDataAnalyticsQueryApi(queryServiceApi);
        var response = await analyticsApi.GetFuturesTradeSignalIdsAsync(valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().Contain(id => id.ContractId == contractId && id.ValueDate == valueDate && id.TimePeriod == TradeTimePeriodType.FifteenSeconds);
    }

    static FuturesTradeSignalV2ReadModel CreateTradeSignalViewModel(string contractId, DateOnly valueDate, long sequenceId)
        => new(
            contractId,
            valueDate,
            TradeTimePeriodType.FifteenSeconds,
            sequenceId,
            TimeOnly.FromDateTime(SampleData.Timestamp.AddSeconds(sequenceId)),
            SampleData.FuturesMean,
            SampleData.FuturesStdDev,
            SampleData.FuturesPrice,
            SampleData.FuturesPercentChange,
            0.02,
            SampleData.FuturesRSI,
            SampleData.FuturesRSISlope,
            FuturesTrendType.UpTrend,
            FuturesTrendStrengthType.Medium,
            TradeSignalType.Buy,
            FuturesTrendDirectionType.UpTrending,
            FuturesTrendDirectionStrengthType.Medium,
            SampleData.FuturesMDI,
            FuturesMDITrendType.UpTrending,
            SampleData.FuturesMDI + 0.2,
            SampleData.FuturesMDI - 0.2,
            SampleData.FuturesPrice + 5,
            SampleData.FuturesPrice - 5,
            SampleData.FuturesPrice + 2,
            SampleData.FuturesPrice - 2,
            SampleData.PredictedDelta,
            SampleData.FuturesPrice + 10,
            SampleData.FuturesPrice - 10,
            SampleData.FuturesFiftyDMA,
            SampleData.FuturesTwoHundredDMA,
            TradeExecuteState.Wait);
}
