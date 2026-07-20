using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using Xunit;
using System;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

/// <summary>
/// Integration tests for TradePlacementCommandApi covering all ITradePlacementCommandApi methods.
/// </summary>
public class TradePlacementCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    /// <summary>
    /// Verify SignalTradePlacementAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task SignalTradePlacement_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlacementCommandApi(commandServiceApi);

        var futuresSignal = new TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels.FuturesTradeSignalV2ReadModel(
            contractId: "ES20251010",
            valueDate: new DateOnly(2025, 10, 10),
            timePeriod: TomasAI.IFM.Shared.MarketDataAnalytics.TradeTimePeriodType.FifteenSeconds,
            sequenceId: 1,
            timestamp: new TimeOnly(10, 0),
            mean: 0,
            stdDev: 0,
            futuresPrice: 4200,
            priceChangePercent: 0,
            fundRiskPercent: 0,
            rsi: 50,
            rsiSlope: 0,
            trendType: TomasAI.IFM.Shared.MarketDataAnalytics.FuturesTrendType.RangeBound,
            trendStrength: TomasAI.IFM.Shared.MarketDataAnalytics.FuturesTrendStrengthType.Low,
            tradeSignal: TomasAI.IFM.Shared.MarketDataAnalytics.TradeSignalType.None,
            tdi: TomasAI.IFM.Shared.MarketDataAnalytics.FuturesTrendDirectionType.Init,
            tdiStrength: TomasAI.IFM.Shared.MarketDataAnalytics.FuturesTrendDirectionStrengthType.Low,
            mdi: 0,
            mdiTrend: TomasAI.IFM.Shared.MarketDataAnalytics.FuturesMDITrendType.RangeBound,
            mdiUpTrendLimit: 0,
            mdiDownTrendLimit: 0,
            upTrendingTrigger: 0,
            downTrendingTrigger: 0,
            entryTrigger: 0,
            exitTrigger: 0,
            trendDelta: 0,
            trendExtreme: 0,
            trendReversal: 0,
            fiftyDMA: 0m,
            twoHundredDMA: 0m,
            tradeExecuteState: TomasAI.IFM.Shared.MarketDataAnalytics.TradeExecuteState.No
        );

        var response = await api.SignalTradePlacementAsync(futuresSignal);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify StartTradePlacementAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task StartTradePlacement_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlacementCommandApi(commandServiceApi);

        var id = new TomasAI.IFM.Shared.Trade.TradePlacementId("ES20251010", new DateOnly(2025, 10, 10));
        var response = await api.StartTradePlacementAsync(id);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Verify StopTradePlacementAsync returns success and a non-empty command id.
    /// </summary>
    [Fact]
    public async Task StopTradePlacement_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlacementCommandApi(commandServiceApi);

        var id = new TomasAI.IFM.Shared.Trade.TradePlacementId("ES20251010", new DateOnly(2025, 10, 10));
        var response = await api.StopTradePlacementAsync(id);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
