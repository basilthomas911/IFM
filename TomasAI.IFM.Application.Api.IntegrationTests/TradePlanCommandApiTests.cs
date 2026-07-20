using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class TradePlanCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task UpdateTradePlan_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlanCommandApi(commandServiceApi);
        var tradePlan = new TradePlanReadModel(
            sequenceId: 1,
            orderId: 1,
            tradeId: 1,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            actionDate: DateTime.Now,
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            tradeType: TradeType.LongIronCondor,
            actionType: ActionType.HoldTradePosition,
            actionSubType: ActionSubType.None,
            actionState: ActionState.Normal,
            actionReason: "test",
            tradePnl: 0,
            forwardLossRatio: 0,
            lossProbability: 0,
            mScore: 0,
            maxProfit: 0,
            maxLoss: 0,
            minProfitTarget: 0,
            dailyProfitTarget: 0,
            assetPrice: 0,
            assetStdDev: 0,
            assetMean: 0,
            assetPriceChange: 0,
            marketTrend: MarketDirectionType.NeutralUp,
            marketVolatility: MarketVolatilityType.Normal,
            marketDirection: PriceDirectionType.Flat,
            vixVolatility: PriceVolatilityType.Unknown,
            tradeRisk: TradeRiskType.Low,
            fiftyDayMA: 0,
            fiveDayXMA: 0,
            putOTMProbability: 0,
            callOTMProbability: 0,
            shortPutGamma: 0,
            shortCallGamma: 0,
            gammaRisk: GammaRiskType.None,
            netPrice: 0,
            forwardPrice: 0,
            forwardDelta: 0,
            stopLossLimit: 0,
            trendType: FuturesTrendType.RangeBound,
            trendStrength: FuturesTrendStrengthType.None,
            rsi: 0,
            rsiSlope: 0,
            tdi: FuturesTrendDirectionType.Init,
            tdiStrength: FuturesTrendDirectionStrengthType.Low,
            createdOn: DateTime.Now,
            createdBy: "user"
        );
        var response = await api.UpdateTradePlanAsync(tradePlan);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task UpdateIronCondorTradePlan_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlanCommandApi(commandServiceApi);
        var valueDate = DateOnly.FromDateTime(DateTime.Now);
        var optionTrade = new OptionTradeReadModel(1, 1, "strategy", valueDate, valueDate.AddDays(30), TradeType.LongIronCondor, TradeState.NewTrade, TradeAction.Buy, "CONTRACT1", AssetType.Equity, true, false, DateTime.Now, "user", DateTime.Now, "user");
        var optionTrades = new OptionTradeCollection(new[] { optionTrade });
        var futuresEodData = new FuturesEodDataV2ReadModel(
            contractId: "CONTRACT1",
            valueDate: valueDate,
            symbol: "SYM",
            openPrice: 100,
            highPrice: 110,
            lowPrice: 90,
            closePrice: 105,
            volume: 1000
        );
        double mScore = 1.0;
        decimal fundBalance = 1000m;
        var response = await api.UpdateIronCondorTradePlanAsync(valueDate, optionTrades, futuresEodData, mScore, fundBalance);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task UpdateTradePlanForwardLossLimit_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlanCommandApi(commandServiceApi);
        var forwardLossLimit = new TradePlanForwardLossLimitReadModel(
            orderId: 1,
            tradeId: 1,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeType: TradeType.LongIronCondor,
            limitType: ForwardLossLimitType.LimitWarning
        );
        var response = await api.UpdateTradePlanForwardLossLimitAsync(forwardLossLimit);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ClearTradePlanForwardLossLimit_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new TradePlanCommandApi(commandServiceApi);
        var entityId = new TradePlanForwardLossLimitEntityId(
            OrderId: 1,
            TradeId: 1,
            ValueDate: DateOnly.FromDateTime(DateTime.Now),
            TradeType: TradeType.LongIronCondor
        );
        var response = await api.ClearTradePlanForwardLossLimitAsync(entityId);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
