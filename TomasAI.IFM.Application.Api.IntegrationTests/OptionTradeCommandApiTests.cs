using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using System;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class OptionTradeCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task Snapshot_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var response = await api.SnapshotAsync(1, 1);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Delete_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var response = await api.DeleteAsync(1, 1);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task PlaceOrder_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var tradeOrder = new TradeOrderReadModel(
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeType: TradeType.LongCall,
            tradeSubType: TradeSubType.Primary,
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            tradeOrderState: TradeOrderState.OrderPlaced,
            underlyingContractId: "SYM20251010",
            underlyingAssetType: AssetType.Equity,
            orderDescription: "Sample order",
            orderAction: OrderAction.Buy,
            orderActionType: OrderActionType.Open,
            orderQuantity: 10,
            orderFilled: 0,
            orderType: OrderType.Limit,
            orderPrice: 100m,
            orderAmount: 1000m,
            commission: 10m,
            totalAmount: 1010m,
            tradePnl: 0m,
            tradeFillType: TradeFillType.Manual,
            createdOn: DateTime.Now,
            createdBy: "user",
            updatedOn: DateTime.Now,
            updatedBy: "user"
        );
        var optionTrade = new OptionTradeReadModel(1, 1, "strategy", DateOnly.FromDateTime(DateTime.Now), DateOnly.FromDateTime(DateTime.Now.AddDays(30)), TradeType.LongCall, TradeState.NewTrade, TradeAction.Buy, "CONTRACT1", AssetType.Equity, true, false, DateTime.Now, "user", DateTime.Now, "user");
        var response = await api.PlaceOrderAsync(tradeOrder, optionTrade);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task OpenOptionTrade_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var tradeOrder = new TradeOrderReadModel(
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeType: TradeType.LongCall,
            tradeSubType: TradeSubType.Primary,
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            tradeOrderState: TradeOrderState.OrderPlaced,
            underlyingContractId: "SYM20251010",
            underlyingAssetType: AssetType.Equity,
            orderDescription: "Sample order",
            orderAction: OrderAction.Buy,
            orderActionType: OrderActionType.Open,
            orderQuantity: 10,
            orderFilled: 0,
            orderType: OrderType.Limit,
            orderPrice: 100m,
            orderAmount: 1000m,
            commission: 10m,
            totalAmount: 1010m,
            tradePnl: 0m,
            tradeFillType: TradeFillType.Manual,
            createdOn: DateTime.Now,
            createdBy: "user",
            updatedOn: DateTime.Now,    
            updatedBy: "user"
        );
        var response = await api.OpenOptionTradeAsync(tradeOrder);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CloseOptionTrade_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var tradeOrder = new TradeOrderReadModel(
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeType: TradeType.LongCall,
            tradeSubType: TradeSubType.Primary,
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            tradeOrderState: TradeOrderState.OrderPlaced,
            underlyingContractId: "SYM20251010",
            underlyingAssetType: AssetType.Equity,
            orderDescription: "Sample order",
            orderAction: OrderAction.Buy,
            orderActionType: OrderActionType.Open,
            orderQuantity: 10,
            orderFilled: 0,
            orderType: OrderType.Limit,
            orderPrice: 100m,
            orderAmount: 1000m,
            commission: 10m,
            totalAmount: 1010m,
            tradePnl: 0m,
            tradeFillType: TradeFillType.Manual,
            createdOn: DateTime.Now,
            createdBy: "user",
            updatedOn: DateTime.Now,
            updatedBy: "user"
        );
        var response = await api.CloseOptionTradeAsync(tradeOrder);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertOptionTradeSpreadData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var spreadData = new OptionTradeSpreadsDataModel(1, 1, DateOnly.FromDateTime(DateTime.Now), TradeType.LongCall, 1, 1, 1, 1, 1, DateTime.Now, "user");
        var response = await api.InsertOptionTradeSpreadDataAsync(spreadData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task InsertOptionTradeSpreadBarData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var spreadBarData = new OptionTradeSpreadBarsDataModel(1, 1, DateOnly.FromDateTime(DateTime.Now), TradeType.LongCall, DateTime.Now, 1, 1, 1, 1);
        var response = await api.InsertOptionTradeSpreadBarDataAsync(spreadBarData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteOptionTradeSpreadBarData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var optionTradeId = new OptionTradeEntityId(1, 1);
        var response = await api.DeleteOptionTradeSpreadBarDataAsync(optionTradeId, TradeType.LongCall, DateOnly.FromDateTime(DateTime.Now));
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeOptionLegData_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var optionLegData = OptionTradeLegDataReadModel.Default(1, 1, TradeType.LongCall, DateOnly.FromDateTime(DateTime.Now), 10, TradeStatus.Open);
        var optionLeg = new OptionTradeLegReadModel(
            orderId: 1,
            tradeId: 1,
            contractId: "SYM20251010C3456",
            quantity: 10,
            strikePrice: 100m,
            optionLegType: OptionType.Call,
            optionLegAction: OptionLegAction.Long,
            createdOn: DateTime.Now,
            createdBy: "user",
            updatedOn: DateTime.Now,
            updatedBy: "user"
        );
        optionLegData = optionLegData.SetOptionLeg(optionLeg);
        var response = await api.ChangeOptionLegDataAsync(1, 1, TradeType.LongCall, DateOnly.FromDateTime(DateTime.Now), TradeStatus.Open, 100, 0.01, optionLegData);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeDistributionStatistics_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var putSpread = new SpreadDistributionReadModel(1, 1, DateOnly.FromDateTime(DateTime.Now), TradeType.LongPut, TradeStatus.Open, 10, 100, 0.1, 10, 1, 0.2, 0.3, 0.5, DateTime.Now);
        var callSpread = new SpreadDistributionReadModel(2, 2, DateOnly.FromDateTime(DateTime.Now), TradeType.LongCall, TradeStatus.Open, 10, 100, 0.1, 10, 1, 0.2, 0.3, 0.5, DateTime.Now);
        var response = await api.ChangeDistributionStatisticsAsync(1, 1, TradeType.LongCall, DateOnly.FromDateTime(DateTime.Now), TradeStatus.Open, putSpread, callSpread);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ProcessEndOfDay_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var response = await api.ProcessEndOfDayAsync(1, 1, 1, TradeType.LongCall, DateOnly.FromDateTime(DateTime.Now), TradeStatus.Open, 1, 1, 1, 1, 1, "ref");
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task UpdateTradeLimitDailyProfitTarget_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var response = await api.UpdateTradeLimitDailyProfitTargetAsync(1, 1, 1, 1);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteOptionTrades_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var api = new OptionTradeCommandApi(commandServiceApi);
        var response = await api.DeleteAsync(1);
        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
