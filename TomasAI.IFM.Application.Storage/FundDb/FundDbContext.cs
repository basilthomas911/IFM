using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.SystemAdmin;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.Storage.FundDb
{
    /// <summary>
    /// fund database
    /// </summary>
    public class FundDbContext : ObjectDataRepository<FundDbContext>, IFundDbContext, IFundDbReadContext, IFundDbWriteContext
    {
        public const string FundDbConnection = "FundDbConnection";
        readonly IDbContextFactory _dbFactory;

        /// <summary>
        /// fund database constructor
        /// </summary>
        /// <param name="connectionSettings"></param>
        /// <param name="dbFactory"></param>
        public FundDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<FundDbContext> logger)
            :base(connectionSettings[FundDbConnection], logger)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <summary>
        /// initialize fund view model mappings
        /// </summary>
        /// <param name="model"></param>
        public override void OnCreateModel(DbModel<FundDbContext> model)
        {
            Fund = model.Map(e => e.Fund)
                .Parameters(e =>
                    e.Set(o => o.FundId)
                     .Set(o => o.Name)
                     .Set(o => o.Description)
                     .Set(o => o.Balance)
                     .Set(o => o.IsProduction)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                );

            FundOrder = model.Map(e => e.FundOrder)
                .Parameters(e =>
                    e.Set(o => o.FundId)
                     .Set(o => o.OrderId)
                     .Set(o => o.OrderDate)
                     .Set(o => o.OrderStatus, o => o.AsEnum<Shared.Fund.OrderStatus>())
                     .Set(o => o.BaseContractId)
                    // .Set(o => o.TradeDate)
                    // .Set(o => o.MaturityDate)
                     .Set(o => o.Reference)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                 );

            FundOrderTrade = model.Map(e => e.FundOrderTrade)
                .Parameters(e =>
                    e.Set(o => o.FundId)
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     //.Set(o => o.TradeDate)
                     //.Set(o => o.MaturityDate)
                     .Set(o => o.TradeState, o => o.AsEnum<TradeState>())
                     .Set(o => o.TradeAction, o => o.AsEnum<TradeAction>())
                     .Set(o => o.Reference)
                     .Set(o => o.PrimaryTrade)
                     .Set(o => o.BaseContractSymbol)
                     .Set(o => o.CreatedOn)
                     .Set(o => o.CreatedBy)
                     .Set(o => o.UpdatedOn)
                     .Set(o => o.UpdatedBy)
                 );

            FundTransaction = model.Map(e => e.FundTransaction)
                .Parameters(e =>
                    e.Set(o => o.TransactionDate)
                     .Set(o => o.TransactionType, o => o.AsEnum<FundTransactionType>())
                     .Set(o => o.FundId)
                     .Set(o => o.OrderId)
                     .Set(o => o.TradeId)
                     .Set(o => o.TradeType, o => o.AsEnum<TradeType>())
                     .Set(o => o.ValueDate)
                     .Set(o => o.TradeStatus, o => o.AsEnum<TradeStatus>())
                     .Set(o => o.Description)
                     .Set(o => o.Amount)
                     .Set(o => o.Balance)
                 );

            FundPnl = model.Map(e => e.FundPnl)
                .Parameters(e =>
                     e.Set(o => o.FundId)
                      .Set(o => o.ValueDate)
                      .Set(o => o.OrderId)
                      .Set(o => o.TradeId)
                      .Set(o => o.Pnl)
                 );

            FundOrderAmount = model.Map(e => e.FundOrderAmount)
                .Parameters(e =>
                     e.Set(o => o.OrderId)
                      .Set(o => o.Amount)
                 );

            FundDailyBalance = model.Map(e => e.FundDailyBalance)
               .Parameters(e =>
                    e.Set(o => o.ValueDate)
                     .Set(o => o.Balance)
                );

            FundDrawdownBalances = model.Map(e => e.FundDrawdownBalances)
               .Parameters(e =>
                    e.Set(o => o.FundId)
                     .Set(o => o.StartBalance)
                     .Set(o => o.EndBalance)
                );
        }

        /// <summary>
        /// return db reader/writer properties
        /// </summary>
        public IFundDbReadContext DbReader => this;
        public IFundDbWriteContext DbWriter => this;

        /// <summary>
        /// object mapping properties
        /// </summary>
        public DbMap<FundReadModel> Fund { get; private set; }
        public DbMap<FundOrderReadModel> FundOrder { get; private set; }
        public DbMap<FundOrderTradeReadModel> FundOrderTrade { get; private set; }
        public DbMap<FundTransactionReadModel> FundTransaction { get; private set; }
        public DbMap<FundPnlReadModel> FundPnl { get; private set; }
        public DbMap<FundOrderAmountReadModel> FundOrderAmount { get; private set; }
        public DbMap<FundDailyBalanceReadModel> FundDailyBalance { get; private set; }
        public DbMap<FundDrawdownBalancesReadModel> FundDrawdownBalances { get; private set; }

        public enum StoredProcedure
        {
            spBackupDatabase,
            spDeleteFundOrder,
            spDeleteFundOrder2,
            spDeleteFundOrderTrade,
            spDeleteFundOrderTrade2,
            spGetFund,
            spGetFunds,
            spGetFundCommission,
            spGetFundOrders,
            spGetFundOrderTrades,
            spGetFundBalance,
            spGetFundBalanceByTradeStatus,
            spGetFundStartingBalance,
            spGetFundEndingBalance,
            spGetFundIdFromOrderId,
            spGetFundTransactions,
            spGetFundPnl,
            spGetFundLossOrders,
            spGetFundProfitOrders,
            spGetFundDailyBalances,
            spGetFundDrawdownBalances,
            spInsertFund,
            spInsertFundOrder,
            spInsertFundOrderTrade,
            spInsertFundTransaction,
            spUpdateFundOrderTradeState,
            spUpdateFundOrderStatus,
            spUpdateFundBalance
        }

        /// <summary>
        /// delete fund order
        /// </summary>
        /// <param name="e"></param>
        /// <param name="removedOn"></param>
        /// <param name="removedBy"></param>
        /// <returns></returns>
        public async Task DeleteFundOrderAsync(FundOrderId e, DateTime removedOn, string removedBy)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spDeleteFundOrder2)
                   .SetParameters(new {
                       fundId = e.FundId,
                       orderId = e.OrderId,
                       removedOn,
                       removedBy })
                   .ExecuteCommandAsync();
        }

        /// <summary>
        /// delete fund order trade
        /// </summary>
        /// <param name="e">order id</param>
        /// <returns></returns>
        public async Task DeleteFundOrderTradeAsync(FundOrderTradeId e)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spDeleteFundOrderTrade2)
                   .SetParameters(new {
                       fundId = e.FundId,
                       orderId = e.OrderId,
                       tradeId = e.TradeId })
                   .ExecuteCommandAsync();
        }

        /// <summary>
        /// return single fund by id
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        public async Task<FundReadModel> GetFundAsync(int fundId)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFund)
                   .SetParameters(new { fundId })
                   .ExecuteSingleAsync<FundReadModel>();
        }

        /// <summary>
        /// return single fund by id
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        public FundReadModel GetFund(int fundId)
            => GetFundAsync(fundId).Result;

        /// <summary>
        /// return all funds
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<FundReadModel>> GetFundsAsync()
        {
            var db = _dbFactory.FundDb;
            return await db .Use(StoredProcedure.spGetFunds)
                   .ExecuteQueryAsync<FundReadModel>();
        }

        /// <summary>
        /// return all fund orders
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<FundOrderReadModel>> GetFundOrdersAsync()
        {
            var db = _dbFactory.FundDb;
            return await db .Use(StoredProcedure.spGetFundOrders)
                .ExecuteQueryAsync<FundOrderReadModel>();
        }

        /// <summary>
        /// return all fund orders
        /// </summary>
        /// <returns></returns>
        public  IReadOnlyList<FundOrderReadModel> GetFundOrders()
            => GetFundOrdersAsync().Result; 

        /// <summary>
        /// return all fund order trades 
        /// </summary>
        /// <returns></returns>
        public async Task<IReadOnlyList<FundOrderTradeReadModel>> GetFundOrderTradesAsync()
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundOrderTrades)
                 .ExecuteQueryAsync<FundOrderTradeReadModel>();
        }

        public IReadOnlyList<FundOrderTradeReadModel> GetFundOrderTrades()
            => GetFundOrderTradesAsync().Result;

        /// <summary>
        /// return list of fund transactions for selected fund by date range
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db .Use(StoredProcedure.spGetFundTransactions)
                .SetParameters(new {
                    fundId,
                    startDate,
                    endDate = endDate.AddDays(1).AddMilliseconds(-1) })
                .ExecuteQueryAsync<FundTransactionReadModel>();
        }

        /// <summary>
        /// return fund pnl for selected fund by date range
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<IReadOnlyList<FundPnlReadModel>> GetFundPnlAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundPnl)
                .SetParameters(new {
                    fundId,
                    startDate = startDate.Date,
                    endDate = endDate.Date })
                .ExecuteQueryAsync<FundPnlReadModel>();
        }

        /// <summary>
        /// return fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        public async Task<decimal> GetFundBalanceAsync(int fundId)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundBalance)
               .SetParameters(new { fundId })
               .ExecuteScalarAsync<decimal>();
        }

        /// <summary>
        /// return starting fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<decimal> GetFundCommissionAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundCommission)
               .SetParameters(new { fundId, startDate, endDate })
               .ExecuteScalarAsync<decimal>();
        }

        /// <summary>
        /// return starting fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <returns></returns>
        public async Task<decimal> GetFundStartingBalanceAsync(int fundId, DateTime startDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundStartingBalance)
               .SetParameters(new { fundId, startDate })
               .ExecuteScalarAsync<decimal>();
        }

        /// <summary>
        /// return ending fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<decimal> GetFundEndingBalanceAsync(int fundId, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundEndingBalance)
                .SetParameters(new { fundId, endDate })
                .ExecuteScalarAsync<decimal>();
        }

        /// <summary>
        /// return opening fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateTime valueDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundBalanceByTradeStatus)
                .SetParameters(new { fundId, valueDate, tradeStatus = $"{TradeStatus.Open}" })
                .ExecuteScalarAsync<decimal>();
        }

        /// <summary>
        /// return closing fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<decimal> GetClosingFundBalanceAsync(int fundId, DateTime valueDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundBalanceByTradeStatus)
                 .SetParameters(new { fundId, valueDate, tradeStatus = $"{TradeStatus.Close}" })
                 .ExecuteScalarAsync<decimal>();
        }

        /// <summary>
        /// return fund orders with loss amounts
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public async Task<IReadOnlyList<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundLossOrders)
                .SetParameters(new { fundId, startDate = startDate.Date, endDate = endDate.Date })
                .ExecuteQueryAsync<FundOrderAmountReadModel>();
        }

        /// <summary>
        /// return fund orders with profit amounts
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        public async Task<IReadOnlyList<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundProfitOrders)
                .SetParameters(new { fundId, startDate = startDate.Date, endDate = endDate.Date })
                .ExecuteQueryAsync<FundOrderAmountReadModel>();
        }

        /// <summary>
        /// return fund daily balances
        /// </summary>
        /// <param name="fundId">selected fund</param>
        /// <param name="startDate">
        /// <paramref name="endDate"/>
        public async Task<IReadOnlyList<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundDailyBalances)
                .SetParameters(new { fundId, startDate = startDate.Date, endDate = endDate.Date })
                .ExecuteQueryAsync<FundDailyBalanceReadModel>();
        }

        /// <summary>
        /// return fund drawdown balances
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(int fundId, DateTime startDate, DateTime endDate)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundDrawdownBalances)
                .SetParameters(new { fundId, startDate = startDate.Date, endDate = endDate.Date })
                .ExecuteSingleAsync<FundDrawdownBalancesReadModel>();
        }

        /// <summary>
        /// return fund id from order id
        /// </summary>
        /// <param name="orderId"></param>
        public async Task<int> GetFundIdFromOrderIdAsync(int orderId)
        {
            var db = _dbFactory.FundDb;
            return await db.Use(StoredProcedure.spGetFundIdFromOrderId)
                .SetParameters(new { orderId })
                .ExecuteScalarAsync<int>();
        }

        /// <summary>
        /// insert fund order
        /// </summary>
        /// <param name="e">investment fund</param>
        /// <returns></returns>
        public async Task InsertFundAsync(FundReadModel e)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spInsertFund)
                   .SetParameters(new {
                       fundId = e.FundId,
                       name = e.Name,
                       description = e.Description,
                       balance = e.Balance,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert fund order
        /// </summary>
        /// <param name="e">fund order</param>
        /// <returns></returns>
        public async Task InsertFundOrderAsync(FundOrderReadModel e)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spInsertFundOrder)
                   .SetParameters(new {
                       fundId = e.FundId,
                       orderId = e.OrderId,
                       orderDate = e.OrderDate,
                       orderStatus = $"{e.OrderStatus}",
                       baseContractId = e.BaseContractId,
                       tradeDate = e.TradeDate,
                       maturityDate = e.MaturityDate,
                       reference = e.Reference ?? string.Empty,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .ExecuteCommandAsync();
        }

        /// <summary>
        /// insert fund order trade
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task InsertFundOrderTradeAsync(FundOrderTradeReadModel e)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spInsertFundOrderTrade)
                   .SetParameters(new {
                       fundId = e.FundId,
                       orderId = e.OrderId,
                       tradeId = e.TradeId,
                       tradeType = $"{e.TradeType}",
                       tradeDate = e.TradeDate,
                       maturityDate = e.MaturityDate,
                       tradeState = $"{e.TradeState}",
                       tradeAction = $"{e.TradeAction}",
                       reference = e.Reference ?? string.Empty,
                       primaryTrade = e.PrimaryTrade,
                       baseContractSymbol = e.BaseContractSymbol,
                       createdOn = e.CreatedOn,
                       createdBy = e.CreatedBy })
                   .ExecuteCommandAsync();
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
            queuedCommands.Add(db.Use(StoredProcedure.spInsertFundTransaction)
                .SetParameters(new {
                    transactionDate = e.TransactionDate,
                    transactionType = $"{e.TransactionType}",
                    fundId = e.FundId,
                    orderId = e.OrderId,
                    tradeId = e.TradeId,
                    tradeType = $"{e.TradeType}",
                    valueDate = e.ValueDate,
                    tradeStatus = $"{e.TradeStatus}",
                    description = e.Description,
                    amount = e.Amount,
                    balance = e.Balance })
                .QueueCommand());
            queuedCommands.Add(db.Use(StoredProcedure.spUpdateFundBalance)
                .SetParameters(new {
                    fundId = e.FundId,
                    balance = e.Balance })
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
            foreach(var e in fundTransactions)
            {
                var queuedCommand = db.Use(StoredProcedure.spInsertFundTransaction)
                    .SetParameters(new {
                        transactionDate = e.TransactionDate,
                        transactionType = $"{e.TransactionType}",
                        fundId = e.FundId,
                        orderId = e.OrderId,
                        tradeId = e.TradeId,
                        tradeType = $"{e.TradeType}",
                        valueDate = e.ValueDate,
                        tradeStatus = $"{e.TradeStatus}",
                        description = e.Description,
                        amount = e.Amount,
                        balance = e.Balance })
                    .QueueCommand();
                queuedCommands.Add(queuedCommand);
                queuedCommand = db.Use(StoredProcedure.spUpdateFundBalance)
                    .SetParameters(new {
                        fundId = e.FundId,
                        balance = e.Balance })
                    .QueueCommand();
                queuedCommands.Add(queuedCommand);
            }
            await db.ExecuteQueuedCommandsAsync(queuedCommands);
        }

        /// <summary>
        /// update fund order trade state
        /// </summary>
        /// <param name="e"></param>
        /// <param name="tradeState"></param>
        /// <param name="updatedOn"></param>
        /// <param name="updatedBy"></param>
        public async Task UpdateFundOrderTradeStateAsync(FundOrderTradeId e, TradeState tradeState, DateTime updatedOn, string updatedBy)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spUpdateFundOrderTradeState)
                   .SetParameters(new {
                       fundId = e.FundId,
                       orderId = e.OrderId,
                       tradeId = e.TradeId,
                       tradeState = $"{tradeState}",
                       updatedOn,
                       updatedBy })
                   .ExecuteCommandAsync();
        }

        /// <summary>
        /// update fund order status
        /// </summary>
        /// <param name="e"></param>
        /// <param name="orderStatus"></param>
        /// <returns></returns>
        public async Task UpdateFundOrderStatusAsync(FundOrderId e, Shared.Fund.OrderStatus orderStatus)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spUpdateFundOrderStatus)
                   .SetParameters(new {
                       fundId = e.FundId,
                       orderId = e.OrderId,
                       orderStatus = $"{orderStatus}" })
                   .ExecuteCommandAsync();
        }

        /// <summary>
        /// backup fund database
        /// </summary>
        /// <param name="backupType"></param>
        /// <param name="commandTimeout"></param>
        /// <param name="onInfoMessage"></param>
        /// <returns></returns>
        public async Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage)
        {
            var db = _dbFactory.FundDb;
            await db.Use(StoredProcedure.spBackupDatabase)
                .SetParameters(new { backupType = $"{backupType}" })
                .WithNoTransaction()
                .SetCommandTimeout(commandTimeout)
                .ExecuteCommandAsync(onInfoMessage);
        }
 
    }
}
