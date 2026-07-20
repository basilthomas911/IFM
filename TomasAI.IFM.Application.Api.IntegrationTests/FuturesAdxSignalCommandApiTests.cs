using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FuturesAdxSignalCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task GenerateFuturesAdxSignal_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new MarketDataAnalyticsCommandApi(commandServiceApi);
        var futuresPrice = 5500m;
        var adxSignalId = new FuturesAdxSignalId(
            "CONTRACT1",
            DateOnly.FromDateTime(DateTime.Now),
            TradeTimePeriodType.Daily, 14,
            TimeOnly.FromDateTime(DateTime.Now));
        var itiSignals = new[] {
            new FuturesItiSignalV2ReadModel(
                contractId: "CONTRACT1",
                valueDate: DateOnly.FromDateTime(DateTime.Now),
                timePeriod: TradeTimePeriodType.Weekly,
                sequenceId: 0,
                intrinsicTime: DateTime.Now,
                intrinsicTimeGroupId: 0,
                intrinsicTimeLength: 0,
                intrinsicPrice: 4500.0,
                intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
                intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
                trendPrice: 4500.0,
                trendExtreme: 4500.0,
                trendReversal: 4450.0,
                trendDelta: 50.0,
                targetDelta: 45.0,
                lambda: 0.01,
                tradingDays: 0,
                threshold: 0,
                upTrendTrigger: 4500.0,
                downTrendTrigger: 4455.0,
                tradeState: IntrinsicTimeTradeState.Ready)
        };
        var response = await api.GenerateFuturesAdxSignalAsync(adxSignalId, futuresPrice);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
