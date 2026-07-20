using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Command.State;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.FundDb;

public class FundDatabaseFixture : IDisposable
{
    public Storage.ScyllaDb.FundDb.FundDbContext FundDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }

    public IDbContextFactory DbFactory { get; private set; }

    public FundDatabaseFixture()
    {
        SetSeqIdDatabase();
        SetFundDatabase();
    }

    void SetFundDatabase()
    {
        var dbConn = new DbConnectionSettings()
                         .Add("FundDbConnection", "Contact Points=localhost;Port=9042;Username=ifmapp;Password=monkey35907;Default Keyspace=fund_test_db", "System.Data.ScyllaDb");

        var diContainer = new Dictionary<Type, Storage.ScyllaDb.FundDb.FundDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<Storage.ScyllaDb.FundDb.FundDbContext>), new Storage.ScyllaDb.FundDb.FundDbContext(dbConn, DbFactory, SequenceIdGenerator, logger));
        FundDb = DbFactory.FundDb as Storage.ScyllaDb.FundDb.FundDbContext;

    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Username=postgres;Password=monkey35907;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, dbFactory, logger));
        SeqIdDatabase = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);
    }

    public void Dispose()
    {
    }
}

public class FundDbTests(FundDatabaseFixture testFixture) 
    : IClassFixture<FundDatabaseFixture>
{
     readonly FundDatabaseFixture _testFixture = testFixture;

    /*
    [Fact]
    [Trait("get a fund", "FundDb")]
    public async Task GetFundAsyncOk()
    {
        var dbFactory = _testFixture.DbFactory;
        await dbFactory.DeleteFundAsync(SampleData.Fund.FundId);
        await dbFactory.InsertFundAsync(SampleData.Fund);
        var fund = await dbFactory.GetFundAsync(SampleData.Fund.FundId);
        fund.Should().NotBeNull();
        fund.FundId.Should().Be(SampleData.Fund.FundId);
        fund.Name.Should().Be(SampleData.Fund.Name);
        fund.Description.Should().Be(SampleData.Fund.Description);
        fund.Balance.Should().Be(SampleData.Fund.Balance);
        fund.IsProduction.Should().Be(SampleData.Fund.IsProduction);
        DateOnly.FromDateTime(fund.CreatedOn).Should().Be(DateOnly.FromDateTime(SampleData.Fund.CreatedOn));
        fund.CreatedBy.Should().Be(SampleData.Fund.CreatedBy);
    }
    */

    [Fact]
    [Trait("get all funds", "FundDb")]
    public async Task GetFundsAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        var funds = await db.GetFundsAsync();
        funds.Should().NotBeNull();
        funds .Count.Should().BeGreaterOrEqualTo(1);
        var fund = funds.Where(e => e.FundId == SampleData.Fund.FundId).SingleOrDefault();
        fund.Should().NotBeNull();
        fund.FundId.Should().Be(SampleData.Fund.FundId);
        fund.Name.Should().Be(SampleData.Fund.Name);
        fund.Description.Should().Be(SampleData.Fund.Description);
        fund.Balance.Should().Be(SampleData.Fund.Balance);
        fund.IsProduction.Should().Be(SampleData.Fund.IsProduction);
        DateOnly.FromDateTime(fund.CreatedOn).Should().Be(DateOnly.FromDateTime(SampleData.Fund.CreatedOn));
        fund.CreatedBy.Should().Be(SampleData.Fund.CreatedBy);
    }


    [Fact]
    [Trait("insert a fund", "FundDb")]
    public async Task InsertFundAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        var fund = await db.GetFundAsync(SampleData.Fund.FundId);
        fund.Should().NotBeNull();
        fund.FundId.Should().Be(SampleData.Fund.FundId);
    }

    [Fact]
    [Trait("delete a fund", "FundDb")]
    public async Task DeleteFundAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        await db.DeleteFundAsync(SampleData.Fund.FundId);
        var fund = await db.GetFundAsync(SampleData.Fund.FundId);
        fund.Should().BeNull();
    }

    [Fact]
    [Trait("insert a fund order", "FundDb")]
    public async Task InsertFundOrderAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund_order where FundId = {SampleData.FundOrder.FundId}").ExecuteCommandAsync();
        await db.InsertFundOrderAsync(SampleData.FundOrder);
        var fundOrders = await db.GetFundOrdersAsync();
        fundOrders.Count.Should().BeGreaterOrEqualTo(1);    
        fundOrders.Where(e => e.FundId == SampleData.FundOrder.FundId && e.OrderId == SampleData.FundOrder.OrderId ).SingleOrDefault().Should().NotBeNull();
    }

    [Fact]
    [Trait("delete a fund order", "FundDb")]
    public async Task DeleteFundOrderAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund_order where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundOrderAsync(SampleData.FundOrder);
        var fundOrderId = FundOrderId.Create(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
         await db.DeleteFundOrderAsync(fundOrderId.FundId, fundOrderId.OrderId);
        var fundOrders = await db.GetFundOrdersAsync();
        fundOrders.Where(e => e.FundId == SampleData.FundOrder.FundId && e.OrderId == SampleData.FundOrder.OrderId).SingleOrDefault().Should().BeNull();
        fundOrders.Count.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    [Trait("insert a fund order trade", "FundDb")]
    public async Task InsertFundOrderTradeAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund_order_trade where FundId = {SampleData.FundOrderTrade.FundId} and OrderId = {SampleData.FundOrderTrade.OrderId}").ExecuteCommandAsync();
        await db.InsertFundOrderTradeAsync(SampleData.FundOrderTrade);
        var fundOrderTrades = await db.GetFundOrderTradesAsync();
        fundOrderTrades.Count.Should().BeGreaterOrEqualTo(1);
        fundOrderTrades.Where(e => e.FundId == SampleData.FundOrderTrade.FundId && e.OrderId == SampleData.FundOrderTrade.OrderId && e.TradeId == SampleData.FundOrderTrade.TradeId).SingleOrDefault().Should().NotBeNull();
    }

    [Fact]
    [Trait("delete a fund order trade", "FundDb")]
    public async Task DeleteFundOrderTradeAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundOrderTrade.FundId;
        var orderId = SampleData.FundOrderTrade.OrderId;
        var tradeId = 992934;
        await db.Use($"delete from fund_order_trade where FundId = {fundId} and OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();
        await db.InsertFundOrderTradeAsync(SampleData.FundOrderTrade with { TradeId = tradeId});
        await db.DeleteFundOrderTradeAsync(SampleData.FundOrderTrade.FundId, SampleData.FundOrderTrade.OrderId, tradeId);
        var fundOrderTrades = await db.GetFundOrderTradesAsync();
        fundOrderTrades.Where(e => e.FundId == fundId && e.OrderId == orderId && e.TradeId == tradeId).SingleOrDefault().Should().BeNull();
    }

    [Fact]
    [Trait("insert a fund transaction", "FundDb")]
    public async Task InsertFundTransactionAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var orderId = SampleData.FundTransaction.OrderId;
        var tradeId = SampleData.FundTransaction.TradeId;   
        var valueDate = SampleData.FundTransaction.ValueDate;
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction);
        var fundTransactions = await db.GetFundTransactionsAsync(
            SampleData.FundTransaction.FundId,
            DateOnly.FromDateTime(SampleData.FundTransaction.TransactionDate.AddDays(-1)),
            DateOnly.FromDateTime(SampleData.FundTransaction.TransactionDate.AddDays(1)));
        fundTransactions.Count.Should().BeGreaterOrEqualTo(1);
        fundTransactions.Where(e => e.FundId == SampleData.FundTransaction.FundId && e.OrderId == SampleData.FundTransaction.OrderId && e.TradeId == SampleData.FundTransaction.TradeId).SingleOrDefault().Should().NotBeNull();
    }

    [Fact]
    [Trait("return pnl for all trades in fund", "FundDb")]
    public async Task GetFundPnlAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var orderId = SampleData.FundTransaction.OrderId;
        var tradeId = SampleData.FundTransaction.TradeId;
        var valueDate = SampleData.FundTransaction.ValueDate;
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundPnl = await db.GetFundPnlAsync(
            SampleData.FundTransaction.FundId,
            SampleData.FundTransaction.ValueDate.AddDays(-1),
            SampleData.FundTransaction.ValueDate.AddDays(1));
        fundPnl.Count.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    [Trait("return current fund balance", "FundDb")]
    public async Task GetFundBalanceAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var valueDate = SampleData.FundTransaction.ValueDate;
        await db
            .Use($"delete from fund where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        var fundBalance = await db.GetFundBalanceAsync(fundId);
        fundBalance.Should().BeGreaterOrEqualTo(SampleData.Fund.Balance);
    }

    [Fact]
    [Trait("return starting fund balance", "FundDb")]
    public async Task GetFundStartingBalanceAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = SampleData.FundTransaction.ValueDate.AddDays(-1);
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000 });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000 });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000 });
        var fundBalance = await db.GetFundStartingBalanceAsync(fundId, startDate);
        fundBalance.Should().Be(1000);
    }

    [Fact]
    [Trait("return ending fund balance", "FundDb")]
    public async Task GetFundEndingBalanceAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var endDate = SampleData.FundTransaction.ValueDate.AddDays(1);
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000 });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000 });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000 });
        var fundBalacel = await db.GetFundEndingBalanceAsync(fundId, endDate);
        fundBalacel.Should().Be(3000);
    }

    [Fact]
    [Trait("return fund loss orders", "FundDb")]
    public async Task GetFundLossOrdersAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = SampleData.FundTransaction.ValueDate.AddDays(-1);
        var endDate = SampleData.FundTransaction.ValueDate.AddDays(1);
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000 , Amount = -300, TransactionType = FundTransactionType.RealizedTradePnl});
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000, Amount = -400, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000, Amount = -500, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundLossOrders = await db.GetFundLossOrdersAsync(fundId, startDate, endDate);
        fundLossOrders.Count.Should().Be(1);
        var fundLossOrder = fundLossOrders.First();
        fundLossOrder.Amount.Should().Be(-1200);
    }

    [Fact]
    [Trait("return fund profit orders", "FundDb")]
    public async Task GetFundProfitOrdersAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = SampleData.FundTransaction.ValueDate.AddDays(-1);
        var endDate = SampleData.FundTransaction.ValueDate.AddDays(1);
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000, Amount = 300, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000, Amount = -400, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000, Amount = 500, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundProfitOrders = await db.GetFundProfitOrdersAsync(fundId, startDate, endDate);
        fundProfitOrders.Count.Should().Be(1);
        var fundProfitOrder = fundProfitOrders.First();
        fundProfitOrder.Amount.Should().Be(800);    
    }

    [Fact]
    [Trait("return fund drawdown balances", "FundDb")]
    public async Task GetFundDrawdownBalancesAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = new DateOnly(2025, 01, 2);
        var endDate = new DateOnly(2025, 01, 28);
        await db
            .Use($"delete from fund_transaction where fundId = :fundId")
            .SetParameters(new { fundId })
            .ExecuteCommandAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000, ValueDate = new DateOnly(2025,01,2), Amount = 300, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000, ValueDate = new DateOnly(2025, 01, 15), Amount = -400, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000, ValueDate = new DateOnly(2025, 01, 28), Amount = 500, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundDrawdownBalances = await db.GetFundDrawdownBalancesAsync(fundId, startDate, endDate);
        fundDrawdownBalances.Should().NotBeNull();
        fundDrawdownBalances.FundId.Should().Be(fundId);
        fundDrawdownBalances.StartBalance.Should().Be(1000);
        fundDrawdownBalances.EndBalance.Should().Be(3000);
    }

    [Fact]
    [Trait("return fund daily balances", "FundDb")]
    public async Task UpdatedFundOrderStatusAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.Use($"delete from fund_order where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        var oldStatus = SampleData.FundOrder.OrderStatus;
        await db.InsertFundOrderAsync(SampleData.FundOrder);
        await db.UpdateFundOrderStatusAsync(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId,  Domain.Fund.Shared.OrderStatus.Closed);
        var fundOrders = await db.GetFundOrdersAsync();
        var fundOrder = fundOrders.Where(e => e.FundId == SampleData.FundOrder.FundId && e.OrderId == SampleData.FundOrder.OrderId).SingleOrDefault();
        fundOrder.OrderStatus.Should().NotBe(oldStatus);
        fundOrder.OrderStatus.Should().Be(Domain.Fund.Shared.OrderStatus.Closed);
    }

    [Fact]
    [Trait("update fund order trade state", "FundDb")]
    public async Task UpdateFundOrderTradeStateAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundOrderTrade.FundId;
        var orderId = SampleData.FundOrderTrade.OrderId;
        var tradeId = SampleData.FundOrderTrade.TradeId;
        await db.Use($"delete from fund_order_trade where FundId = {fundId} and OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();
        await db.InsertFundOrderTradeAsync(SampleData.FundOrderTrade);
        var oldState = SampleData.FundOrderTrade.TradeState;
        await db.UpdateFundOrderTradeStateAsync(fundId, orderId, tradeId, Shared.Trade.TradeState.OrderCompleted, DateTime.Now, "basilt");
        var fundOrderTrades = await db.GetFundOrderTradesAsync();
        var fundOrderTrade = fundOrderTrades.Where(e => e.FundId == fundId && e.OrderId == orderId && e.TradeId == tradeId).SingleOrDefault();
        fundOrderTrade.TradeState.Should().NotBe(oldState);
        fundOrderTrade.TradeState.Should().Be(Shared.Trade.TradeState.OrderCompleted);
    }

    [Fact]
    [Trait("read fund from CSV file and insert into database", "FundDb")]
    public async Task GetFundsFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = _testFixture.DbFactory.FundDb;
        var dbFund = db as IFundDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\fund.csv"))
           .ReadAsync(MapToFund, async funds => {
               await db.Use($"truncate fund").ExecuteCommandAsync();
               rowCount = await dbFund.InsertFundsAsync(funds);
           });
        var resultSet = await dbFund.GetFundsAsync();
        Assert.NotNull(resultSet);
        Assert.Equal(rowCount, resultSet.Count);

        static FundReadModel MapToFund(string e, int o)
            => new(
                fundId: e.GetInt(ref o),
                name: e.GetString(ref o),
                description: e.GetString(ref o),
                balance: e.GetDecimal(ref o),
                isProduction: e.GetBool(ref o),
                createdOn: e.GetDateTime(ref o).ToUniversalTime(),
                createdBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read fund orders from CSV file and insert into database", "FundDb")]
    public async Task GetFundOrdersFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = _testFixture.DbFactory.FundDb;
        var dbFund = db as IFundDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\fund_order.csv"))
           .ReadAsync(MapToFundOrder, async fundOrderTrades => {
               await db.Use($"truncate fund_order").ExecuteCommandAsync();
               rowCount = await dbFund.InsertFundOrdersAsync(fundOrderTrades);
           });
        var resultSet = await dbFund.GetFundOrdersAsync();
        Assert.NotNull(resultSet);
        Assert.Equal(rowCount, resultSet.Count);

       static FundOrderReadModel MapToFundOrder(string e, int o)
           => new(
                fundId: e.GetInt(ref o),
                orderId: e.GetInt(ref o),
                orderDate: e.GetDateTime(ref o).ToUniversalTime(),
                orderStatus: e.GetEnum<Domain.Fund.Shared.OrderStatus>(ref o),
                baseContractId: e.GetString(ref o),
                tradeDate: e.GetDateOnly(ref o),
                maturityDate: e.GetDateOnly(ref o),
                reference: e.GetString(ref o),
                createdOn: e.GetDateTime(ref o).ToUniversalTime(),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o).ToUniversalTime(),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read fund order trades from CSV file and insert into database", "FundDb")]
    public async Task GetFundOrderTradesFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = _testFixture.DbFactory.FundDb;
        var dbFund = db as IFundDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\fund_order_trade.csv"))
           .ReadAsync(MapToFundOrderTrade, async fundOrderTrades => {
                await db.Use($"truncate fund_order_trade").ExecuteCommandAsync();
                rowCount = await dbFund.InsertFundOrderTradesAsync(fundOrderTrades);
           });
        var resultSet = await dbFund.GetFundOrderTradesAsync();
        Assert.NotNull(resultSet);
        Assert.Equal(rowCount, resultSet.Count);

        static FundOrderTradeReadModel MapToFundOrderTrade(string e, int o)
            =>new(
                fundId: e.GetInt(ref o),
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                tradeType: e.GetEnum<TradeType>(ref o),
                tradeDate: e.GetDateOnly(ref o),
                maturityDate: e.GetDateOnly(ref o),
                tradeState: e.GetEnum<TradeState>(ref o),
                tradeAction: e.GetEnum<TradeAction>(ref o),
                reference: e.GetString(ref o),
                primaryTrade: e.GetBool(ref o),
                baseContractSymbol: e.GetString(ref o),
                createdOn: e.GetDateTime(ref o),
                createdBy: e.GetString(ref o),
                updatedOn: e.GetDateTime(ref o),
                updatedBy: e.GetString(ref o)
            );
    }

    [Fact]
    [Trait("read fund transactions from CSV file and insert into database", "FundDb")]
    public async Task GetFundTransactionsFromCsvFileOk()
    {
        var rowCount = 0l;
        var db = _testFixture.DbFactory.FundDb;
        var dbFund = db as IFundDbContext;
        await db.Use(new Uri("C:\\TomasAI\\data\\SqlServer\\fund_transaction.csv"))
           .ReadAsync(MapToFundTransaction, async fundTx => {
               await db.Use($"truncate fund_transaction").ExecuteCommandAsync();
               rowCount = await dbFund.InsertFundTransactionsAsync(fundTx);
           });
        var resultSet = await dbFund.GetFundTransactionsAsync();
        Assert.NotNull(resultSet);
        Assert.Equal(rowCount, resultSet.Count);
       
        static FundTransactionReadModel MapToFundTransaction(string e, int o)
            => new(
                transactionId: e.GetInt(ref o),
                fundId: e.GetInt(ref o),
                orderId: e.GetInt(ref o),
                tradeId: e.GetInt(ref o),
                transactionType: e.GetEnum<FundTransactionType>(ref o),
                transactionDate: e.GetDateTime(ref o).ToUniversalTime(),
                tradeType: e.GetEnum<TradeType>(ref o),
                valueDate: e.GetDateOnly(ref o),
                tradeStatus: e.GetEnum<TradeStatus>(ref o),
                description: e.GetString(ref o),
                amount: e.GetDecimal(ref o),
                balance: e.GetDecimal(ref o)
            );
    }
}
