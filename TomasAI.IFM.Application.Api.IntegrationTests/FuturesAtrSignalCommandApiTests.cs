using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesAtrSignalCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GenerateFuturesAtrSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var atrSignalId = new FuturesAtrSignalId(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TradeTimePeriodType.FifteenSeconds,
            14,
            TimeOnly.FromDateTime(DateTime.Now));
        var itiSignals = new[] {
            new FuturesItiSignalV2ReadModel(
                contractId: "CONTRACT1",
                valueDate: DateOnly.FromDateTime(DateTime.Now),
                timePeriod: TradeTimePeriodType.FifteenSeconds,
                sequenceId: 1,
                intrinsicTime: DateTime.Now,
                intrinsicTimeGroupId: 1,
                intrinsicTimeLength: 1,
                intrinsicPrice: 100,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
                trendPrice: 100,
                trendExtreme: 101,
                trendReversal: 99,
                lambda: 0.5,
                tradingDays: 0,
                threshold: 0,
                targetDelta: 1,
                trendDelta: 1,
                upTrendTrigger: 1,
                downTrendTrigger: 1,
                tradeState: IntrinsicTimeTradeState.Ready)
        };
        var response = await api.GenerateFuturesAtrSignalAsync(atrSignalId, itiSignals);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateFuturesAtrSignalFromIntraDayData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var atrSignalId = new FuturesAtrSignalId(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TradeTimePeriodType.FifteenSeconds,
            14,
            TimeOnly.FromDateTime(DateTime.Now));
        var intraDayData = new[] {
            new FuturesIntraDayDataReadModel()
        };
        var response = await api.GenerateFuturesAtrSignalFromIntraDayDataAsync(atrSignalId, intraDayData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
