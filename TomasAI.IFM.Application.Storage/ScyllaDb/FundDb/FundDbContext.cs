using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

/// <summary>
/// Represents the database context for managing fund-related data and operations.
/// </summary>
/// <remarks>The <see cref="FundDbContext"/> class provides methods for performing CRUD operations on funds, fund
/// orders, fund transactions, and related entities. It also includes functionality for retrieving financial data such
/// as balances, profit/loss, and daily balances. This class is designed to interact with a database using the provided
/// connection settings and factory.</remarks>
/// <param name="connectionSettings">The database connection settings used to configure the context.</param>
/// <param name="dbFactory">The factory for creating database connections and executing commands.</param>
/// <param name="sequenceIdGenerator">The generator for creating unique sequence IDs for database entities.</param>
/// <param name="logger">The logger used for logging database operations and errors.</param>
public class FundDbContext : ObjectDataRepository<FundDbContext>, IFundDbContext
{
    readonly IDbContextFactory _dbFactory;
    readonly ISequenceIdGenerator _sequenceIdGenerator;
    public const string FundDbConnection = "FundDbConnection";

    /*
    // Parameterless constructor for unit testing only
    public FundDbContext()
        : base(null, null) 
    {
    }
    */

    // Parameterized constructor
    public FundDbContext(
        IDbConnectionSettings connectionSettings,
        IDbContextFactory dbFactory,
        ISequenceIdGenerator sequenceIdGenerator,
        ILogger<DbProvider> logger)
        : base(connectionSettings[FundDbConnection], logger)
    {
        _dbFactory = IsArgumentNull.Set(dbFactory);
        _sequenceIdGenerator = IsArgumentNull.Set(sequenceIdGenerator);
    }

    /// <summary>
    /// Gets the database context.
    /// </summary>
    public override FundDbContext Database => this;

    /// <summary>
    /// object mapping properties
    /// </summary>
    static FundReadModel MapToFund<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            name: e.GetString(1),
            description: e.GetString(2),
            balance: e.GetDecimal(3),
            isProduction: e.GetBool(4),
            createdOn: e.GetDateTime(5).ToUniversalTime(),
            createdBy: e.GetString(6)
        );

    static FundOrderReadModel MapToFundOrder<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            orderId: e.GetInt(1),
            orderDate: e.GetDateTime(2),
            orderStatus: e.GetEnum<Domain.Fund.Shared.OrderStatus>(3),
            baseContractId: e.GetString(4),
            tradeDate: e.GetDateOnly(5),
            maturityDate: e.GetDateOnly(6),
            reference: e.GetString(7),
            createdOn: e.GetDateTime(8),
            createdBy: e.GetString(9),
            updatedOn: e.GetDateTime(10),
            updatedBy: e.GetString(11)
        );

    static FundOrderTradeReadModel MapToFundOrderTrade<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            orderId: e.GetInt(1),
            tradeId: e.GetInt(2),
            tradeType: e.GetEnum<TradeType>(3),
            tradeDate: e.GetDateOnly(4),
            maturityDate: e.GetDateOnly(5),
            tradeState: e.GetEnum<TradeState>(6),
            tradeAction: e.GetEnum<TradeAction>(7),
            reference: e.GetString(8),
            primaryTrade: e.GetBool(9),
            baseContractSymbol: e.GetString(10),
            createdOn: e.GetDateTime(11),
            createdBy: e.GetString(12),
            updatedOn: e.GetDateTime(13),
            updatedBy: e.GetString(14)
        );

    static FundTransactionReadModel MapToFundTransaction<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            transactionId: e.GetLong(0),
            transactionDate: e.GetDateTime(1),
            transactionType: e.GetEnum<FundTransactionType>(2),
            fundId: e.GetInt(3),
            orderId: e.GetInt(4),
            tradeId: e.GetInt(5),
            tradeType: e.GetEnum<TradeType>(6),
            valueDate: e.GetDateOnly(7),
            tradeStatus: e.GetEnum<TradeStatus>(8),
            description: e.GetString(9),
            amount: e.GetDecimal(10),
            balance: e.GetDecimal(11)
        );

    static FundPnlReadModel MapToFundPnl<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            valueDate: e.GetDateOnly(1),
            orderId: e.GetInt(2),
            tradeId: e.GetInt(3),
            tradeType: e.GetEnum<TradeType>(4),
            pnl: e.GetDecimal(5)
        );

    static decimal MapToFundBalance<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDecimal(0);

    static decimal MapToFundTradeCommission<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDecimal(0);

    static DateTime MapToFundTransactionDate<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetDateTime(0);

    static long MapToFundTransactionId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetLong(0);

    static FundOrderAmountReadModel MapToFundOrderAmount<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => new (
            fundId: e.GetInt(0),
            valueDate: e.GetDateOnly(1),
            orderId: e.GetInt(2),
            amount: e.GetDecimal(3)
        );

    static FundDailyBalanceReadModel MapToFundDailyBalance<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
         => new (
            fundId: e.GetInt(0),
            valueDate: e.GetDateOnly(1),
            balance: e.GetDecimal(2)
        );

    static int MapToFundId<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
        => e.GetInt(0);

    /// <summary>
    /// delete fund, fund order and fund order trades by tradeId
    /// </summary>
    /// <param name="fundId"></param>
    /// <returns></returns>
    public async Task DeleteFundAsync(int fundId)
        => await _dbFactory.FundDb
                .Use(FundDbCql.DeleteFund)
                .SetParameters(new DeleteFund(fundId))
                .ExecuteCommandAsync();

    /// <summary>
    /// delete fund order
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task DeleteFundOrderAsync(int fundId, int orderId)
        => await _dbFactory.FundDb
               .Use(FundDbCql.DeleteFundOrder)
               .SetParameters(new DeleteFundOrder(fundId, orderId))
               .ExecuteCommandAsync();

    /// <summary>
    /// delete fund order trade
    /// </summary>
    /// <param name="fundId">fund order trade id</param>
    /// <param name="orderId">fund order id</param>
    /// <param name="tradeId">fund order trade id</param>
    /// <returns></returns>
    public async Task DeleteFundOrderTradeAsync(int fundId, int orderId, int tradeId)
        => await _dbFactory.FundDb.Use(FundDbCql.DeleteFundOrderTrade)
               .SetParameters(new DeleteFundOrderTrade(fundId, orderId, tradeId))
               .ExecuteCommandAsync();

    /// <summary>
    /// delete fund trancaction
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="transactionType"></param>
    /// <param name="transactionDate"></param>
    /// <returns></returns>
    public async Task DeleteFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, 
        TradeType tradeType, FundTransactionType transactionType, DateTime transactionDate)
        => await _dbFactory.FundDb
                .Use(FundDbCql.DeleteFundTransaction)
                .SetParameters(new DeleteFundTransaction(fundId, valueDate, orderId, tradeId, tradeType.ToStringFast(), transactionType.ToStringFast(), transactionDate))
                .ExecuteCommandAsync();

    /// <summary>
    /// return single fund by id
    /// </summary>
    /// <param name="fundId"></param>
    /// <returns></returns>
    public async Task<FundReadModel?> GetFundAsync(int fundId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundByFundId)
            .SetParameters(new GetFundByFundId(fundId))
            .ExecuteSingleAsync(MapToFund);

    /// <summary>
    /// return all funds
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundReadModel>> GetFundsAsync()
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFunds)
            .ExecuteQueryAsync(MapToFund!);

    /// <summary>
    /// return fund order by fund id and order id
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>  
    /// <returns></returns>
    public async Task<FundOrderReadModel?> GetFundOrderAsync(int fundId, int orderId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrder)
            .SetParameters(new GetFundOrder(fundId, orderId))
            .ExecuteSingleAsync(MapToFundOrder);

    /// <summary>
    /// return all fund orders
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundOrderReadModel>> GetFundOrdersAsync()
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrders)
            .ExecuteQueryAsync(MapToFundOrder);

    /// <summary>
    /// return all fund orders
    /// </summary>
    /// <returns></returns>
    public ICollection<FundOrderReadModel> GetFundOrders()
        => GetFundOrdersAsync().Result;

    /// <summary>
    /// return all fund order trades 
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundOrderTradeReadModel>> GetFundOrderTradesAsync()
        => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundOrderTrades)
                .ExecuteQueryAsync(MapToFundOrderTrade);

    /// <summary>
    /// return fund order trade by fund id, order id and trade id
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    /// 
    public async Task<FundOrderTradeReadModel?> GetFundOrderTradeAsync(int fundId, int orderId, int tradeId)
    => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundOrderTrade)
            .SetParameters(new GetFundOrderTrade(fundId, orderId, tradeId))
            .ExecuteSingleAsync(MapToFundOrderTrade!);

    /// <summary>
    /// return all fund order trades
    /// </summary>
    /// <returns></returns>
    public ICollection<FundOrderTradeReadModel> GetFundOrderTrades()
        => GetFundOrderTradesAsync().Result;

    /// <summary>
    /// return fund transaction 
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="transactionType"></param>
    /// <param name="transactionDate"></param>
    /// <returns></returns>
    public async Task<FundTransactionReadModel?> GetFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, TradeType tradeType,
        FundTransactionType transactionType, DateTime transactionDate)
        => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundTransaction)
                .SetParameters(new GetFundTransaction(fundId, valueDate, orderId, tradeId, tradeType.ToStringFast(), transactionType.ToStringFast(), transactionDate))
                .ExecuteSingleAsync(MapToFundTransaction!);

    /// <summary>
    /// return list of fund transactions for selected fund by date range
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.FundDb
            .Use("""
               SELECT 
                    transactionId AS "TransactionId",
                   transactionDate AS "TransactionDate", 
                   transactionType AS "TransactionType", 
                   fundId AS "FundId", 
                   orderId AS "OrderId", 
                   tradeId AS "TradeId", 
                   tradeType AS "TradeType", 
                   valueDate AS "ValueDate", 
                   tradeStatus AS "TradeStatus", 
                   description AS "Description", 
                   amount AS "Amount", 
                   balance AS "Balance"
                FROM fund_transaction
                WHERE fundId = :fundId 
                AND valueDate >= :startDate 
                AND valueDate <= :endDate;
              """)
            .SetParameters(new { fundId, startDate, endDate })
            .ExecuteQueryAsync(MapToFundTransaction!);

    /// <summary>
    /// return all  fund transactions
    /// </summary>
    /// <returns></returns>
    public async Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync()
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundTransactionsAll)
            .ExecuteQueryAsync(MapToFundTransaction!);

    /// <summary>
    /// return fund pnl for selected fund by date range
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ICollection<FundPnlReadModel>> GetFundPnlAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundPnl)
            .SetParameters(new GetFundPnl(fundId, startDate, endDate))
            .ExecuteQueryAsync(MapToFundPnl!);

    /// <summary>
    /// return fund balance
    /// </param>
    /// <returns></returns>
    public async Task<decimal> GetFundBalanceAsync(int fundId)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundBalance)
            .SetParameters(new GetFundBalance(fundId))
            .ExecuteScalarAsync(MapToFundBalance!);

    /// <summary>
    /// return fund trade commission
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetFundTradeCommissionAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundTradeCommission)
                .SetParameters(new GetFundTradeCommission(fundId, startDate, endDate))
                .ExecuteScalarAsync(MapToFundTradeCommission!);

    /// <summary>
    /// return starting fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="startDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetFundStartingBalanceAsync(int fundId, DateOnly startDate)
    {
        var transactionId = await _dbFactory.FundDb
                .Use(FundDbCql.GetFundMinTransactionId)
               .SetParameters(new GetFundMinTransactionId(fundId, startDate))
               .ExecuteScalarAsync(MapToFundTransactionId!);

        return transactionId != 0
            ? await _dbFactory.FundDb
                .Use(FundDbCql.GetFundBalanceByTransactionId)
               .SetParameters(new GetFundBalanceByTransactionId(fundId, transactionId))
               .ExecuteScalarAsync(MapToFundBalance!)
             : 0m;
    }

    /// <summary>
    /// return ending fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetFundEndingBalanceAsync(int fundId, DateOnly endDate)
    {
        var transactionId = await _dbFactory.FundDb
            .Use(FundDbCql.GetFundMaxTransactionId)
           .SetParameters(new GetFundMaxTransactionId(fundId, endDate))
           .ExecuteScalarAsync(MapToFundTransactionId!);

        return transactionId != 0
            ?  await _dbFactory.FundDb
                .Use(FundDbCql.GetFundBalanceByTransactionId)
               .SetParameters(new GetFundBalanceByTransactionId(fundId, transactionId))
               .ExecuteScalarAsync(MapToFundBalance!)
            : 0m;
    }

    /// <summary>
    /// return opening fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate)
    {
        var transactionDate = await _dbFactory.FundDb
           .Use(FundDbCql.GetFundMinTransactionDateByTradeStatus)
          .SetParameters(new GetFundTransactionDateByTradeStatus(fundId, valueDate, TradeStatus.Open.ToStringFast()))
          .ExecuteScalarAsync(MapToFundTransactionDate!);

        return transactionDate != DateTime.MinValue
            ? await _dbFactory.FundDb
                 .Use(FundDbCql.GetFundBalanceByTransactionDate)
                .SetParameters(new GetFundBalanceByTransactionDate(fundId, transactionDate))
                .ExecuteScalarAsync(MapToFundBalance!)
            : 0m;
    }

    /// <summary>
    /// return closing fund balance
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<decimal> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate)
    {
        var transactionDate = await _dbFactory.FundDb
           .Use(FundDbCql.GetFundMaxTransactionDateByTradeStatus)
          .SetParameters(new GetFundTransactionDateByTradeStatus(fundId, valueDate, TradeStatus.Close.ToStringFast()))
          .ExecuteScalarAsync(MapToFundTransactionDate!);

        return await _dbFactory.FundDb
                 .Use(FundDbCql.GetFundBalanceByTransactionDate)
                .SetParameters(new GetFundBalanceByTransactionDate(fundId, transactionDate))
                .ExecuteScalarAsync(MapToFundBalance!);
    }

    /// <summary>
    /// return fund orders with loss amounts
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundLossOrders)
                .SetParameters(new GetFundLossOrders(fundId, startDate, endDate))
                .ExecuteQueryAsync(MapToFundOrderAmount!);

    /// <summary>
    /// return fund orders with profit amounts
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    public async Task<ICollection<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate)
        => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundProfitOrders)
            .SetParameters(new GetFundProfitOrders(fundId, startDate, endDate))
            .ExecuteQueryAsync(MapToFundOrderAmount!);

    /// <summary>
    /// return fund daily balances
    /// </summary>
    /// <param name="fundId">selected fund</param>
    /// <param name="startDate">
    /// <paramref name="endDate"/>
    public async Task<ICollection<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate)
          => await _dbFactory.FundDb
            .Use(FundDbCql.GetFundDailyBalance)
            .SetParameters(new GetFundDailyBalance(fundId, startDate, endDate))
            .ExecuteQueryAsync(MapToFundDailyBalance!);

    /// <summary>
    /// return fund drawdown balances
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate)
    {
        var startingBalance = await GetFundStartingBalanceAsync(fundId, startDate);
        var endingBalance = await GetFundEndingBalanceAsync(fundId, endDate);
        return new FundDrawdownBalancesReadModel(fundId, startingBalance, endingBalance);
    }

    /// <summary>
    /// return fund id from order id
    /// </summary>
    /// <param name="orderId"></param>
    public async Task<int> GetFundIdFromOrderIdAsync(int orderId)
            => await _dbFactory.FundDb
                .Use(FundDbCql.GetFundIdFromOrderId)
                .SetParameters(new GetFundIdFromOrderId(orderId))
                .ExecuteScalarAsync(MapToFundId!);

    /// <summary>
    /// insert fund order
    /// </summary>
    /// <param name="e">investment fund</param>
    /// <returns></returns>
    public async Task InsertFundAsync(FundReadModel e)
        => await _dbFactory.FundDb
            .Use(FundDbCql.InsertFund)
            .SetParameters(new InsertFund(e.FundId, e.Name, e.Description, e.Balance, e.IsProduction, e.CreatedOn, e.CreatedBy))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of funds into the database asynchronously.
    /// </summary>
    /// <remarks>This method uses the underlying database connection to execute the insertion command. Ensure
    /// that the provided <paramref name="funds"/> collection is not null or empty, and that each <see
    /// cref="FundReadModel"/> contains valid data to avoid potential errors during execution.</remarks>
    /// <param name="funds">A collection of <see cref="FundReadModel"/> objects representing the funds to be inserted. Each fund must have
    /// valid values for its properties, such as <c>FundId</c>, <c>Name</c>, and <c>Balance>.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the funds have been successfully
    /// inserted.</returns>
    public async Task InsertFundsAsync(ICollection<FundReadModel> funds)
        => await _dbFactory.FundDb
            .Use(FundDbCql.InsertFund)
            .SetParameters(funds.Select(e => new InsertFund(e.FundId, e.Name, e.Description, e.Balance, e.IsProduction, e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of funds into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of funds and inserts them into the database. 
    /// The operation is performed asynchronously, and the method returns the total count of rows inserted.</remarks>
    /// <param name="funds">A collection of <see cref="FundReadModel"/> objects representing the funds to be inserted. Each fund must have
    /// valid properties such as <c>FundId</c>, <c>Name</c>, and <c>Balance</c>.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the number of rows
    /// successfully inserted into the database.</returns>
    public async Task<long> InsertFundsAsync(IEnumerable<FundReadModel> funds)
    {
        var rowCount = 0l;
        await _dbFactory.FundDb
            .Use(FundDbCql.InsertFund)
            .SetParameters(GetFunds().Select(e => new InsertFund(e.FundId, e.Name, e.Description, e.Balance, e.IsProduction, e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FundReadModel> GetFunds()
        {
            rowCount = 0l;
            foreach (var e in funds)
            {
                rowCount++;
                yield return e;
            }
        }

    }

    /// <summary>
    /// insert fund order
    /// </summary>
    /// <param name="e">fund order</param>
    /// <returns></returns>
    public async Task InsertFundOrderAsync(FundOrderReadModel e)
            => await _dbFactory.FundDb
                .Use(FundDbCql.InsertFundOrder)
               .SetParameters(new InsertFundOrder(e.FundId, e.OrderId, e.OrderDate, e.OrderStatus.ToStringFast(), e.BaseContractId, e.TradeDate, e.MaturityDate, e.Reference ?? string.Empty, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy))
               .ExecuteCommandAsync();

    /// <summary>
    /// insert fund orders
    /// </summary>
    /// <param name="fundOrders">fund order</param>
    /// <returns></returns>
    public async Task InsertFundOrdersAsync(ICollection<FundOrderReadModel> fundOrders)
            => await _dbFactory.FundDb
                .Use(FundDbCql.InsertFundOrder)
               .SetParameters(fundOrders.Select(e => new InsertFundOrder(e.FundId, e.OrderId, e.OrderDate, e.OrderStatus.ToStringFast(), e.BaseContractId, e.TradeDate, e.MaturityDate, e.Reference ?? string.Empty, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy)))
               .ExecuteCommandAsync();

    /// <summary>
    /// insert fund orders
    /// </summary>
    /// <param name="fundOrders">fund order</param>
    /// <returns></returns>
    public async Task<long> InsertFundOrdersAsync(IEnumerable<FundOrderReadModel> fundOrders)
    { 
        var rowCount = 0l;
        await _dbFactory.FundDb
                .Use(FundDbCql.InsertFundOrder)
               .SetParameters(GetFundOrders().Select(e => new InsertFundOrder(e.FundId, e.OrderId, e.OrderDate, e.OrderStatus.ToStringFast(), e.BaseContractId, e.TradeDate, e.MaturityDate, e.Reference ?? string.Empty, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy)))
               .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FundOrderReadModel> GetFundOrders()
        {
            rowCount = 0l;
            foreach (var e in fundOrders)
            {
                rowCount++;
                yield return e;
            }
        }

    }

    /// <summary>
    /// insert fund order trade
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public async Task InsertFundOrderTradeAsync(FundOrderTradeReadModel e)
            => await _dbFactory.FundDb
                    .Use(FundDbCql.InsertFundOrderTrade)
                   .SetParameters(new InsertFundOrderTrade(e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.MaturityDate, e.TradeState.ToStringFast(), e.TradeAction.ToStringFast(), e.Reference ?? string.Empty, e.PrimaryTrade, e.BaseContractSymbol, e.CreatedOn, e.CreatedBy))
                   .ExecuteCommandAsync();

    /// <summary>
    /// insert fund order trades
    /// </summary>
    /// <param name="fundOrderTrades"></param>
    /// <returns></returns>
    public async Task InsertFundOrderTradesAsync(ICollection<FundOrderTradeReadModel> fundOrderTrades)
            => await _dbFactory.FundDb
                    .Use(FundDbCql.InsertFundOrderTrade)
                   .SetParameters(fundOrderTrades.Select(e => new InsertFundOrderTrade(e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.MaturityDate, e.TradeState.ToStringFast(), e.TradeAction.ToStringFast(), e.Reference ?? string.Empty, e.PrimaryTrade, e.BaseContractSymbol, e.CreatedOn, e.CreatedBy)))
                   .ExecuteCommandAsync();

    /// <summary>
    /// Inserts a collection of fund order trade records into the database asynchronously.
    /// </summary>
    /// <remarks>This method processes the provided collection of fund order trades and inserts them into the
    /// database using the configured database factory and query. Ensure that the <paramref name="fundOrderTrades"/>
    /// collection is not null and contains valid data for each trade, as invalid or incomplete data may result in an
    /// error.</remarks>
    /// <param name="fundOrderTrades">A collection of <see cref="FundOrderTradeReadModel"/> objects representing the fund order trades to be inserted.
    /// Each object must contain valid data for the associated fund, order, and trade details.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when all fund order trade records have
    /// been successfully inserted into the database.</returns>
    public async Task<long> InsertFundOrderTradesAsync(IEnumerable<FundOrderTradeReadModel> fundOrderTrades)
    {
        var rowCount = 0l;
        await _dbFactory.FundDb
           .Use(FundDbCql.InsertFundOrderTrade)
           .SetParameters(GetFundOrderTrades().Select(e => new InsertFundOrderTrade(e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.MaturityDate, e.TradeState.ToStringFast(), e.TradeAction.ToStringFast(), e.Reference ?? string.Empty, e.PrimaryTrade, e.BaseContractSymbol, e.CreatedOn, e.CreatedBy)))
           .ExecuteCommandAsync();
        return rowCount;

        IEnumerable<FundOrderTradeReadModel> GetFundOrderTrades()
        {
            rowCount = 0l;
            foreach (var e in fundOrderTrades)
            {
                rowCount++;
                yield return e;
            }
        }
    }


    /// <summary>
    /// insert fund transction
    /// </summary>
    /// <param name="e">fund transaction</param>
    /// <returns></returns>
    public async Task InsertFundTransactionAsync(FundTransactionReadModel e)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.FundDb;
        var transactionId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FundTransaction_TransactionId);
        queuedCommands.Add(
        db.Use(FundDbCql.InsertFundTransaction)
            .SetParameters(new InsertFundTransaction(
                transactionId, 
                e.TransactionDate, 
                e.TransactionType.ToStringFast(), 
                e.FundId, 
                e.OrderId, 
                e.TradeId, 
                e.TradeType.ToStringFast(), 
                e.ValueDate, 
                e.TradeStatus.ToStringFast(), 
                e.Description, 
                e.Amount, 
                e.Balance))
            .QueueCommand());

        queuedCommands.Add(
        db.Use(FundDbCql.UpdateFundBalance)
            .SetParameters(new UpdateFundBalance(e.FundId, e.Balance))
            .QueueCommand());
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// insert fund transctions
    /// </summary>
    /// <param name="fundTransactions">fund transaction</param>
    /// <returns></returns>
    public async Task InsertFundTransactionsAsync(ICollection<FundTransactionReadModel> fundTransactions)
    {
        var queuedCommands = new List<object>();
        var db = _dbFactory.FundDb;
        foreach (var e in fundTransactions)
        {
            var transactionId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FundTransaction_TransactionId);
            queuedCommands.Add(
            db.Use(FundDbCql.InsertFundTransaction)
                .SetParameters(new InsertFundTransaction(
                    transactionId, 
                    e.TransactionDate, 
                    e.TransactionType.ToStringFast(), 
                    e.FundId, 
                    e.OrderId, 
                    e.TradeId, 
                    e.TradeType.ToStringFast(), 
                    e.ValueDate, 
                    e.TradeStatus.ToStringFast(), 
                    e.Description, 
                    e.Amount, 
                    e.Balance))
                .QueueCommand());

            queuedCommands.Add(
            db.Use(FundDbCql.UpdateFundBalance)
                .SetParameters(new UpdateFundBalance(e.FundId, e.Balance))
                .QueueCommand());
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
    }

    /// <summary>
    /// Inserts a collection of fund transactions into the database and updates the corresponding fund balances.
    /// </summary>
    /// <remarks>This method generates unique transaction IDs for each fund transaction and ensures that both
    /// the transaction records  and the associated fund balances are updated atomically. The method uses a queued
    /// command execution model to batch  database operations for efficiency.</remarks>
    /// <param name="fundTransactions">A collection of <see cref="FundTransactionReadModel"/> objects representing the fund transactions to be
    /// inserted. Each transaction must include details such as transaction date, type, fund ID, and balance.</param>
    /// <returns>A <see cref="Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
    /// database commands queued and executed.</returns>
    public async Task<long> InsertFundTransactionsAsync(IEnumerable<FundTransactionReadModel> fundTransactions)
    {
        var rowCount = 0l;
        var queuedCommands = new List<object>();
        var db = _dbFactory.FundDb;
        foreach (var e in fundTransactions)
        {
            var transactionId = await _sequenceIdGenerator.GetSequenceIdAsync(SequenceName.FundTransaction_TransactionId);
            queuedCommands.Add(
            db.Use(FundDbCql.InsertFundTransaction)
                .SetParameters(new InsertFundTransaction(transactionId, e.TransactionDate, e.TransactionType.ToStringFast(), e.FundId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.TradeStatus.ToStringFast(), e.Description, e.Amount, e.Balance))
                .QueueCommand());

            queuedCommands.Add(
            db.Use(FundDbCql.UpdateFundBalance)
                .SetParameters(new UpdateFundBalance(e.FundId, e.Balance))
                .QueueCommand());
            rowCount++;
        }
        await db.ExecuteQueuedCommandsAsync(queuedCommands);
        return rowCount;
    }

    /// <summary>
    /// update fund order trade state
    /// </summary>
    /// <param name="fundId"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>  
    /// <param name="tradeState"></param>
    /// <param name="updatedOn"></param>
    /// <param name="updatedBy"></param>
    public async Task UpdateFundOrderTradeStateAsync(int fundId, int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy)
        => await _dbFactory.FundDb
                .Use(FundDbCql.UpdateFundOrderTradeState)
               .SetParameters(new UpdateFundOrderTradeState(fundId, orderId, tradeId, tradeState.ToStringFast(), updatedOn, updatedBy))
               .ExecuteCommandAsync();

    /// <summary>
    /// update fund order status
    /// </summary>
    /// <param name="e"></param>
    /// <param name="orderStatus"></param>
    /// <returns></returns>
    public async Task UpdateFundOrderStatusAsync(int fundId, int orderId, Domain.Fund.Shared.OrderStatus orderStatus)
        => await _dbFactory.FundDb
            .Use(FundDbCql.UpdateFundOrderStatus)
            .SetParameters(new UpdateFundOrderStatus(fundId, orderId, orderStatus.ToStringFast()))
            .ExecuteCommandAsync();

    /// <summary>
    /// backup fund database
    /// </summary>
    /// <param name="backupType"></param>
    /// <param name="commandTimeout"></param>
    /// <param name="onInfoMessage"></param>
    /// <returns></returns>
    public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
            => throw new NotImplementedException();
    /*
=> await _dbFactory.FundDb
        .Use(StoredProcedure.spBackupDatabase)
        .SetParameters(new { backupType = $"{backupType}" })
        .WithNoTransaction()
        .SetCommandTimeout(commandTimeout)
        .ExecuteCommandAsync(onInfoMessage);

}
    */
}
