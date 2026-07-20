using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Application.Api.Client;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.RestApi;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.IntegrationTests;

public class FundQueryApiTests(WebApplicationFactory<Program> factory, FundDatabaseFixture dbFixture)
    : IClassFixture<WebApplicationFactory<Program>>, IClassFixture<FundDatabaseFixture>
{
    readonly HttpClientTestFactory _httpClientFactory = new(factory);
    readonly IJsonSerializer _jsonSerializer = new NewtonSoftJsonSerializer();
    readonly ILogger<NatsActorEventListener> _logger = Substitute.For<ILogger<NatsActorEventListener>>();

    [Fact]
    public async Task GetClosingFundBalanceQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var fundTx = SampleData.FundTransaction with { TradeStatus = TradeStatus.Close, ValueDate = valueDate };
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_transaction where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundTransactionAsync(fundTx);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetClosingFundBalanceAsync(fund.FundId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().Be(fundTx.Balance);
    }

    [Fact]
    public async Task GetOpeningFundBalanceQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var valueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var fundTx = SampleData.FundTransaction with { TradeStatus = TradeStatus.Open, ValueDate =  valueDate };
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_transaction where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundTransactionAsync(fundTx);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetOpeningFundBalanceAsync(fund.FundId, valueDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().Be(fundTx.Balance);
    }

    [Fact]
    public async Task GetFundBalanceQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundTx = SampleData.FundTransaction;
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_transaction where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundTransactionAsync(fundTx);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundBalanceAsync(fund.FundId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().Be(fundTx.Balance);
    }

    [Fact]
    public async Task GetFundsQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.InsertFundAsync(fund);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundsAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(f => f.FundId == fund.FundId);
    }

    [Fact]
    public async Task GetFundOrdersQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_order where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundOrderAsync(fundOrder);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundOrdersAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(o => o.FundId == fund.FundId && o.OrderId == fundOrder.OrderId);
    }

    [Fact]
    public async Task GetFundOrderTradesQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundOrder = SampleData.FundOrder;
        var fundOrderTrade = SampleData.FundOrderTrade;
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_order where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.Use($"delete from fund_order_trade where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundOrderAsync(fundOrder);
        await dbFixture.FundDb.InsertFundOrderTradeAsync(fundOrderTrade);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundOrderTradesAsync();

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Should().NotBeEmpty();
        response.Value.Should().Contain(t => t.FundId == fund.FundId && t.OrderId == fundOrder.OrderId && t.TradeId == fundOrderTrade.TradeId);
    }

    [Fact]
    public async Task GetFundPnlReportQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundTx = SampleData.FundTransaction;
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_transaction where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundTransactionAsync(fundTx);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundPnlReportAsync(fund.FundId, startDate, endDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.WinLossRatio.Should().Be(0);
    }

    [Fact]
    public async Task GetFundIdFromOrderIdQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundOrder = SampleData.FundOrder;
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_order where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundOrderAsync(fundOrder);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundIdFromOrderIdAsync(fundOrder.OrderId);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.Value.Should().Be(fund.FundId);
    }

    [Fact]
    public async Task GetFundWinLossRatioQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundTx = SampleData.FundTransaction;
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_transaction where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundTransactionAsync(fundTx);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundWinLossRatioAsync(fund.FundId, startDate, endDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFundDrawdownBalancesQuery_Ok()
    {
        // arrange...
        var fund = SampleData.NewFund;
        var fundTx = SampleData.FundTransaction;
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await dbFixture.FundDb.DeleteFundAsync(fund.FundId);
        await dbFixture.FundDb.Use($"delete from fund_transaction where fundid = {fund.FundId}").ExecuteCommandAsync();
        await dbFixture.FundDb.InsertFundAsync(fund);
        await dbFixture.FundDb.InsertFundTransactionAsync(fundTx);

        // act...
        _httpClientFactory.CreateClient();
        var queryServiceApi = new QueryServiceApiClient(_httpClientFactory, _jsonSerializer, new QueryServiceApiOptions("http://localhost"));
        var fundApi = new FundQueryApi(queryServiceApi);
        var response = await fundApi.GetFundDrawdownBalancesAsync(fund.FundId, startDate, endDate);

        // assert...
        response.Should().NotBeNull();
        response.Success.Should().BeTrue();
        response.Value.Should().NotBeNull();
        response.Value.FundId.Should().Be(fund.FundId);
        response.Value.StartBalance.Should().Be(fundTx.Balance);
        response.Value.EndBalance.Should().Be(fundTx.Balance);
    }
}
