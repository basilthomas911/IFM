using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Command.State;
using TomasAI.IFM.Application.Storage.IntegrationTests.FundDb;

namespace TomasAI.IFM.Application.Storage.LoadTests.SqlServer;

public class FundDbLoadTests(FundDatabaseFixture testFixture) : IClassFixture<FundDatabaseFixture>
{
    readonly FundDatabaseFixture _testFixture = testFixture;

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
