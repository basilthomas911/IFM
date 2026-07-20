using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.IntegrationTests;

public class FundCommandApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();

    [Fact]
    public async Task CreateFund_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fund = new FundReadModel(
            fundId: 1,
            name: "Test Fund",
            description: "Test Description",
            balance: 1000m,
            isProduction: false,
            createdOn: DateTime.Now,
            createdBy: "TestUser"
        );
        var response = await fundApi.CreateFundAsync(fund);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddOrderToFund_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrder = new FundOrderReadModel(
            fundId: 1,
            orderId: 1,
            orderDate: DateTime.Now,
            orderStatus: Domain.Fund.Shared.OrderStatus.Open,
            baseContractId: "CONTRACT1",
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            reference: "Test Reference",
            createdOn: DateTime.Now,
            createdBy: "TestUser",
            updatedOn: DateTime.Now,
            updatedBy: "TestUser"
        );
        var response = await fundApi.AddOrderToFundAsync(fundOrder);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveOrderFromFund_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrderId = new FundOrderId(1, 1);
        var response = await fundApi.RemoveOrderFromFundAsync(fundOrderId);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddTradeToFundOrder_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrderTrade = new FundOrderTradeReadModel(
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            tradeType: TomasAI.IFM.Shared.Trade.TradeType.LongCall,
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            tradeState: TradeState.NewTrade,
            tradeAction: TomasAI.IFM.Shared.Trade.TradeAction.Buy,
            reference: "Test Reference",
            primaryTrade: true,
            baseContractSymbol: "CONTRACT1",
            createdOn: DateTime.Now,
            createdBy: "TestUser",
            updatedOn: DateTime.Now,
            updatedBy: "TestUser"
        );
        var response = await fundApi.AddTradeToFundOrderAsync(fundOrderTrade);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task RemoveTradeFromFundOrder_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrderTradeId = new FundOrderTradeId(1, 1, 1);
        var response = await fundApi.RemoveTradeFromFundOrderAsync(fundOrderTradeId);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CloseFundOrder_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrderId = new FundOrderId(1, 1);
        var response = await fundApi.CloseFundOrderAsync(fundOrderId);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeFundOrderTradeState_WithCorrelationId_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrderTradeId = new FundOrderTradeId(1, 1, 1);
        var response = await fundApi.ChangeFundOrderTradeStateAsync(fundOrderTradeId, TradeState.NewTrade, Guid.NewGuid());

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ChangeFundOrderTradeState_WithSetCommandId_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrderTradeId = new FundOrderTradeId(1, 1, 1);
        var response = await fundApi.ChangeFundOrderTradeStateAsync(fundOrderTradeId, TradeState.NewTrade);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateFundTransaction_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundTransaction = new FundTransactionReadModel(
            transactionId: 1,
            transactionDate: DateTime.Now,
            transactionType: FundTransactionType.OpeningTrade,
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            tradeType: TomasAI.IFM.Shared.Trade.TradeType.LongCall,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeStatus: TradeStatus.Open,
            description: "Test Transaction",
            amount: 1000m,
            balance: 1000m
        );
        var response = await fundApi.CreateFundTransactionAsync(fundTransaction);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateFundTransactions_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundTransactions = new[]
        {
            new FundTransactionReadModel(
                transactionId: 1,
                transactionDate: DateTime.Now,
                transactionType: FundTransactionType.OpeningTrade,
                fundId: 1,
                orderId: 1,
                tradeId: 1,
                tradeType: TomasAI.IFM.Shared.Trade.TradeType.LongCall,
                valueDate: DateOnly.FromDateTime(DateTime.Now),
                tradeStatus: TradeStatus.Open,
                description: "Test Transaction",
                amount: 1000m,
                balance: 1000m
            )
        };
        var fundTransactionsId = new FundTransactionEntityId(1,1);
        var response = await fundApi.CreateFundTransactionsAsync(fundTransactionsId, fundTransactions, Guid.NewGuid());

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GenerateFundMaxProfit_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundOrder = new FundOrderReadModel(
            fundId: 1,
            orderId: 1,
            orderDate: DateTime.Now,
            orderStatus: Domain.Fund.Shared.OrderStatus.Open,
            baseContractId: "CONTRACT1",
            tradeDate: DateOnly.FromDateTime(DateTime.Now),
            maturityDate: DateOnly.FromDateTime(DateTime.Now.AddDays(30)),
            reference: "Test Reference",
            createdOn: DateTime.Now,
            createdBy: "TestUser",
            updatedOn: DateTime.Now,
            updatedBy: "TestUser"
        );
        var response = await fundApi.GenerateFundMaxProfitAsync(fundOrder, Shared.MarketDataAnalytics.TradeTimePeriodType.Daily);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ProcessEndOfDayFundTransaction_Ok()
    {
        var commandServiceApi = new CommandServiceApiClient(_httpClientFactory, _jsonSerializer, new CommandServiceApiOptions("http://localhost"));
        var fundApi = new FundCommandApi(commandServiceApi);

        var fundTransaction = new FundTransactionReadModel(
            transactionId: 1,
            transactionDate: DateTime.Now,
            transactionType: FundTransactionType.EndOfDayProcessed,
            fundId: 1,
            orderId: 1,
            tradeId: 1,
            tradeType: TomasAI.IFM.Shared.Trade.TradeType.LongCall,
            valueDate: DateOnly.FromDateTime(DateTime.Now),
            tradeStatus: TradeStatus.EndOfDay,
            description: "End of Day Transaction",
            amount: 1000m,
            balance: 1000m
        );
        var response = await fundApi.ProcessEndOfDayFundTransactionAsync(Guid.NewGuid(), fundTransaction);

        response.Success.Should().BeTrue();
        response.Value.Should().NotBe(Guid.Empty);
    }
}
