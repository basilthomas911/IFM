using TomasAI.IFM.Domain.Trade.Shared.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Text.Json;
using System.Globalization;

using TomasAI.IFM.Domain.MarketData.Shared;

using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;


using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder;

using MessagePack;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

namespace TomasAI.IFM.Application.Storage.TradeDb;
/// <summary>Provides internal persistence helpers for <see cref = "TradeDbContext"/>.</summary>
public static class TradeDbContextExtensions
{
    extension(TradeDbContext context)
    {
        /// <summary>Requires a bounded route query page between one and five hundred rows.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        internal void ValidateRoutePageSize(int pageSize)
        {
            if (pageSize is < 1 or > 500)
                throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Inserts or updates an option leg in the database asynchronously.
        /// </summary>
        /// <remarks>This method first deletes any existing option leg with the same trade and contract
        /// identifiers, then inserts the new option leg data. The operation is performed as a queued command to ensure
        /// atomicity and consistency.</remarks>
        /// <param name = "e">The <see cref = "OptionTradeLegReadModel"/> containing the option leg details to be upserted.</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertOptionLegAsyncCoreBinding(OptionTradeLegReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var db = context;
            List<object> queuedCommands = [db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionLegById)}", TradeDbSql.DeleteOptionLegById)
            .SetParameters(new DeleteOptionLegById(e.OrderId, e.TradeId, e.ContractId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLeg)}", TradeDbSql.InsertOptionLeg)
            .SetParameters(new InsertOptionLeg(e.OrderId, e.TradeId, e.ContractId, e.Quantity, e.StrikePrice, e.OptionLegType.ToStringFast(), e.OptionLegAction.ToStringFast(), DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand()];
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionLegsAsync operation.</summary>
        internal async Task<long> InsertOptionLegsAsyncCoreBinding(IEnumerable<OptionTradeLegReadModel> optionLegs)
        {
            ArgumentNullException.ThrowIfNull(optionLegs);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLeg)}", TradeDbSql.InsertOptionLeg)
            .SetParameters(EnumerateOptionLegs().Select(e => new InsertOptionLeg(e.OrderId, e.TradeId, e.ContractId, e.Quantity, e.StrikePrice, e.OptionLegType.ToStringFast(), e.OptionLegAction.ToStringFast(), DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<OptionTradeLegReadModel> EnumerateOptionLegs()
            {
                foreach (var e in optionLegs)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Inserts option leg data into the database asynchronously.
        /// </summary>
        /// <remarks>This method first deletes any existing option leg data for the specified trade and value
        /// date, then inserts the new data. The operation is performed asynchronously.</remarks>
        /// <param name = "e">The <see cref = "OptionTradeLegDataReadModel"/> containing the option leg data to be inserted.</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertOptionLegDataAsyncCoreBinding(OptionTradeLegDataReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var db = context;
            List<object> queuedCommands = [db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionLegDataById)}", TradeDbSql.DeleteOptionLegDataById)
            .SetParameters(new DeleteOptionLegDataById(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.OptionLegId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLegData)}", TradeDbSql.InsertOptionLegData)
            .SetParameters(new InsertOptionLegData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.OptionLegId, e.BidPrice, e.AskPrice, e.ImpliedVolatility, e.Delta, e.Gamma, e.Theta, e.Vega, e.Rho, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand()];
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionLegDataAsync operation.</summary>
        internal async Task<long> InsertOptionLegDataAsyncCoreBinding(IEnumerable<OptionTradeLegDataReadModel> optionLegData)
        {
            ArgumentNullException.ThrowIfNull(optionLegData);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLegData)}", TradeDbSql.InsertOptionLegData)
            .SetParameters(EnumerateOptionLegData().Select(e => new InsertOptionLegData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.OptionLegId, e.BidPrice, e.AskPrice, e.ImpliedVolatility, e.Delta, e.Gamma, e.Theta, e.Vega, e.Rho, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<OptionTradeLegDataReadModel> EnumerateOptionLegData()
            {
                foreach (var e in optionLegData)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert option trade
        /// </summary>
        /// <param name = "e">futures bar data</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertOptionTradeAsyncCoreBinding(OptionTradeReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            // save base option trade...
            var queuedCommands = new List<object>();
            var db = context;
            var dbWriter = db as ITradeDbWriteContext;
            await (dbWriter!.DeleteOptionTradeAsync(e.OrderId, e.TradeId))
            .ConfigureAwait(false);
            queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTrade)}", TradeDbSql.InsertOptionTrade)
            .SetParameters(new InsertOptionTrade(e.TradeId, e.OrderId, e.TradeDate, e.MaturityDate, e.TradeType.ToStringFast(), e.TradeState.ToStringFast(), e.TradeStrategy ?? string.Empty, e.TradeAction.ToStringFast(), e.UnderlyingContractId, e.UnderlyingAssetType.ToStringFast(), e.IsPrimaryTrade, e.IsHedgeTrade, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand());
            // save each option leg info...
            if (e.OptionLegs is not null)
            {
                foreach (var ol in e.OptionLegs)
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLeg)}", TradeDbSql.InsertOptionLeg)
            .SetParameters(new InsertOptionLeg(e.OrderId, ol.TradeId, ol.ContractId, ol.Quantity, ol.StrikePrice, ol.OptionLegType.ToStringFast(), ol.OptionLegAction.ToStringFast(), DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand());
            }

            // save trade positions...
            if (e.TradePositions is not null)
            {
                foreach (var o in e.TradePositions)
                {
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePosition)}", TradeDbSql.InsertTradePosition)
            .SetParameters(new InsertTradePosition(e.OrderId, o.TradeId, o.TradeType.ToStringFast(), o.ValueDate, o.DaysToExpiry, o.TradeStatus.ToStringFast(), o.Commission, o.DeltaHedge, o.NetSpread, o.TradeValue, o.TradePnl, o.AssetPrice, o.OTMProbability, o.ForwardPrice, o.ForwardLossRatio, o.LossProbability, o.RiskFreeRate, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand());
                    // save option leg data within each trade position...
                    if (o.OptionLegData is not null)
                    {
                        foreach (var old in o.OptionLegData)
                            queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLegData)}", TradeDbSql.InsertOptionLegData)
            .SetParameters(new InsertOptionLegData(e.OrderId, old.TradeId, old.TradeType.ToStringFast(), old.ValueDate, old.DaysToExpiry, old.TradeStatus.ToStringFast(), old.OptionLegId, old.BidPrice, old.AskPrice, old.ImpliedVolatility, old.Delta, old.Gamma, old.Theta, old.Vega, old.Rho, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand());
                    }
                }
            }

            // save trade limit...
            if (e.TradeLimit is not null)
            {
                queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeLimit)}", TradeDbSql.InsertTradeLimit)
            .SetParameters(new InsertTradeLimit(e.TradeLimit.TradeId, e.TradeLimit.TradeType.ToStringFast(), e.TradeLimit.RiskMargin, e.TradeLimit.MaxProfit, e.TradeLimit.MaxLoss, e.TradeLimit.MaxReturn, e.TradeLimit.MaxLossLimit, e.TradeLimit.MinProfitLimit, e.TradeLimit.MaxProfitLimit, e.TradeLimit.MinProfitTarget, e.TradeLimit.DailyProfitTarget, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
            .QueueCommand());
                // save trade type limits...
                queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeTypeLimit)}", TradeDbSql.InsertTradeTypeLimit)
            .SetParameters(new InsertTradeTypeLimit(e.TradeLimit.TradeId, e.TradeLimit.TradeType.ToStringFast(), e.TradeLimit.MaxLossLimit, e.TradeLimit.MinProfitLimit, e.TradeLimit.MaxProfitLimit))
            .QueueCommand());
            }

            if (e.TradeTypeLimits is not null)
            {
                foreach (var ttl in e.TradeTypeLimits)
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeTypeLimit)}", TradeDbSql.InsertTradeTypeLimit)
            .SetParameters(new InsertTradeTypeLimit(ttl.TradeId, ttl.TradeType.ToStringFast(), ttl.MaxLossLimit, ttl.MinProfitLimit, ttl.MaxProfitLimit))
            .QueueCommand());
            }

            // save any trade fills...
            if (e.TradeFills is not null)
            {
                foreach (var tf in e.TradeFills)
                {
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeFill)}", TradeDbSql.InsertTradeFill)
            .SetParameters(new InsertTradeFill(tf.OrderId, tf.TradeId, tf.FillDate, tf.FillQuantity, tf.CreatedOn, tf.CreatedBy))
            .QueueCommand());
                    if (tf.TradeFillData is not null)
                        foreach (var tfd in tf.TradeFillData)
                            queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeFillData)}", TradeDbSql.InsertTradeFillData)
            .SetParameters(new InsertTradeFillData(tfd.OrderId, tfd.TradeId, tfd.ContractId, tfd.FillDate, tfd.BidPrice, tfd.AskPrice, tfd.Commission, tfd.OptionLegAction.ToStringFast(), tfd.CreatedOn, tfd.CreatedBy))
            .QueueCommand());
                }
            }

            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionTradesAsync operation.</summary>
        internal async Task<long> InsertOptionTradesAsyncCoreBinding(IEnumerable<OptionTradeReadModel> optionTrades)
        {
            ArgumentNullException.ThrowIfNull(optionTrades);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTrade)}", TradeDbSql.InsertOptionTrade)
            .SetParameters(EnumerateOptionTrades().Select(e => new InsertOptionTrade(e.TradeId, e.OrderId, e.TradeDate, e.MaturityDate, e.TradeType.ToStringFast(), e.TradeState.ToStringFast(), e.TradeStrategy ?? string.Empty, e.TradeAction.ToStringFast(), e.UnderlyingContractId, e.UnderlyingAssetType.ToStringFast(), e.IsPrimaryTrade, e.IsHedgeTrade, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<OptionTradeReadModel> EnumerateOptionTrades()
            {
                foreach (var e in optionTrades)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade position
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertTradePositionAsyncCoreBinding(TradePositionReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var db = context;
            List<object> queuedCommands = [db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePosition)}", TradeDbSql.DeleteTradePosition)
            .SetParameters(new DeleteTradePositionLowerCase(e.OrderId, e.TradeId))
            .QueueCommand(), db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionLegData)}", TradeDbSql.DeleteOptionLegData)
            .SetParameters(new DeleteOptionLegDataLowerCase(e.OrderId, e.TradeId))
            .QueueCommand()];
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
            queuedCommands.Clear();
            queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePosition)}", TradeDbSql.InsertTradePosition)
            .SetParameters(new InsertTradePosition(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.Commission, e.DeltaHedge, e.NetSpread, e.TradeValue, e.TradePnl, e.AssetPrice, e.OTMProbability, e.ForwardPrice, e.ForwardLossRatio, e.LossProbability, e.RiskFreeRate, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy))
            .QueueCommand());
            // save option leg data within trade position...
            if (e.OptionLegData is not null && e.OptionLegData.Length > 0)
                queuedCommands.AddRange(e.OptionLegData.Select(old => db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLegData)}", TradeDbSql.InsertOptionLegData)
            .SetParameters(new InsertOptionLegData(e.OrderId, old.TradeId, old.TradeType.ToStringFast(), old.ValueDate, old.DaysToExpiry, old.TradeStatus.ToStringFast(), old.OptionLegId, old.BidPrice, old.AskPrice, old.ImpliedVolatility, old.Delta, old.Gamma, old.Theta, old.Vega, old.Rho, old.CreatedOn, old.CreatedBy, old.UpdatedOn, old.UpdatedBy))
            .QueueCommand()));
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade position
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task InsertTradePositionAsyncCoreBinding(ICollection<TradePositionReadModel> tradePositions)
        {
            ArgumentNullException.ThrowIfNull(tradePositions);
            if (tradePositions.Count == 0)
                return;
            var queuedCommands = new List<object>();
            var db = context;
            foreach (var e in tradePositions)
            {
                queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePosition)}", TradeDbSql.DeleteTradePosition)
            .SetParameters(new DeleteTradePositionLowerCase(e.OrderId, e.TradeId))
            .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteOptionLegData)}", TradeDbSql.DeleteOptionLegData)
            .SetParameters(new DeleteOptionLegDataLowerCase(e.OrderId, e.TradeId))
            .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePosition)}", TradeDbSql.InsertTradePosition)
            .SetParameters(new InsertTradePosition(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.Commission, e.DeltaHedge, e.NetSpread, e.TradeValue, e.TradePnl, e.AssetPrice, e.OTMProbability, e.ForwardPrice, e.ForwardLossRatio, e.LossProbability, e.RiskFreeRate, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy))
            .QueueCommand());
                // save option leg data within trade position...
                if (e.OptionLegData is not null)
                {
                    foreach (var old in e.OptionLegData)
                        queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionLegData)}", TradeDbSql.InsertOptionLegData)
            .SetParameters(new InsertOptionLegData(e.OrderId, old.TradeId, old.TradeType.ToStringFast(), old.ValueDate, old.DaysToExpiry, old.TradeStatus.ToStringFast(), old.OptionLegId, old.BidPrice, old.AskPrice, old.ImpliedVolatility, old.Delta, old.Gamma, old.Theta, old.Vega, old.Rho, old.CreatedOn, old.CreatedBy, old.UpdatedOn, old.UpdatedBy))
            .QueueCommand());
                }
            }

            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradePositionsAsync operation.</summary>
        internal async Task<long> InsertTradePositionsAsyncCoreBinding(IEnumerable<TradePositionReadModel> tradePositions)
        {
            ArgumentNullException.ThrowIfNull(tradePositions);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePosition)}", TradeDbSql.InsertTradePosition)
            .SetParameters(EnumerateTradePositions().Select(e => new InsertTradePosition(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.Commission, e.DeltaHedge, e.NetSpread, e.TradeValue, e.TradePnl, e.AssetPrice, e.OTMProbability, e.ForwardPrice, e.ForwardLossRatio, e.LossProbability, e.RiskFreeRate, DateTime.Now, e.CreatedBy, DateTime.Now, e.UpdatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<TradePositionReadModel> EnumerateTradePositions()
            {
                foreach (var e in tradePositions)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeFillAsync operation.</summary>
        internal async Task InsertTradeFillAsyncCoreBinding(ICollection<TradeFillReadModel> tradeFills)
        {
            ArgumentNullException.ThrowIfNull(tradeFills);
            // save any trade fills...
            if (tradeFills.Count > 0)
            {
                var db = context;
                foreach (var tf in tradeFills)
                {
                    var queuedCommands = new List<object>();
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeFill)}", TradeDbSql.DeleteTradeFill)
            .SetParameters(new DeleteTradeFill(tf.OrderId, tf.TradeId))
            .QueueCommand());
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradeFillData)}", TradeDbSql.DeleteTradeFillData)
            .SetParameters(new DeleteTradeFillData(tf.OrderId, tf.TradeId))
            .QueueCommand());
                    queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeFill)}", TradeDbSql.InsertTradeFill)
            .SetParameters(new InsertTradeFill(tf.OrderId, tf.TradeId, tf.FillDate, tf.FillQuantity, tf.CreatedOn, tf.CreatedBy))
            .QueueCommand());
                    if (tf.TradeFillData?.Length > 0)
                        foreach (var tfd in tf.TradeFillData!)
                            queuedCommands.Add(db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeFillData)}", TradeDbSql.InsertTradeFillData)
            .SetParameters(new InsertTradeFillData(tfd.OrderId, tfd.TradeId, tfd.ContractId, tfd.FillDate, tfd.BidPrice, tfd.AskPrice, tfd.Commission, tfd.OptionLegAction.ToStringFast(), tfd.CreatedOn, tfd.CreatedBy))
            .QueueCommand());
                    await (db.ExecuteQueuedCommandsAsync(queuedCommands, true))
            .ConfigureAwait(false);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeFillsAsync operation.</summary>
        internal async Task<long> InsertTradeFillsAsyncCoreBinding(IEnumerable<TradeFillReadModel> tradeFills)
        {
            ArgumentNullException.ThrowIfNull(tradeFills);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeFill)}", TradeDbSql.InsertTradeFill)
            .SetParameters(EnumerateTradeFills().Select(e => new InsertTradeFill(e.OrderId, e.TradeId, e.FillDate, e.FillQuantity, e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<TradeFillReadModel> EnumerateTradeFills()
            {
                foreach (var e in tradeFills)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert option tradde spread data
        /// </summary>
        /// <param name = "e">option trade spread data</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertOptionTradeSpreadDataAsyncCoreBinding(OptionTradeSpreadsDataModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var sequenceId = e.SequenceId > 0 ? e.SequenceId : await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId))
            .ConfigureAwait(false);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTradeSpreadData)}", TradeDbSql.InsertOptionTradeSpreadData)
            .SetParameters(new InsertOptionTradeSpreadData(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), sequenceId, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread, e.CreatedOn, e.CreatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Asynchronously inserts a collection of option trade spread data into the database.
        /// </summary>
        /// <remarks>The method processes the data in batches of 1000 items to optimize database operations. Each
        /// data entry is assigned a unique sequence ID before insertion.</remarks>
        /// <param name = "optionTradeSpreadsData">A collection of <see cref = "OptionTradeSpreadsDataModel"/> objects representing the option trade spread data to
        /// be inserted.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertOptionTradeSpreadDataAsyncCoreBinding(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadsData)
        {
            ArgumentNullException.ThrowIfNull(optionTradeSpreadsData);
            List<(long SequenceId, OptionTradeSpreadsDataModel Data)> batchData = [];
            var db = context;
            for (var index = 0; index < optionTradeSpreadsData.Count; index += 1000)
            {
                batchData.Clear();
                foreach (var e in optionTradeSpreadsData.Skip(index).Take(1000))
                {
                    var id = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId))
            .ConfigureAwait(false);
                    batchData.Add((id, e));
                }

                await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTradeSpreadData)}", TradeDbSql.InsertOptionTradeSpreadData)
            .SetParameters(batchData.Select(e => new InsertOptionTradeSpreadData(e.Data.OrderId, e.Data.TradeId, e.Data.ValueDate, e.Data.TradeType.ToStringFast(), e.SequenceId, e.Data.LossLimit, e.Data.WinLimit, e.Data.ForwardSpread, e.Data.NetSpread, e.Data.CreatedOn, e.Data.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Inserts a collection of option trade spread data into the database asynchronously.
        /// </summary>
        /// <remarks>This method generates a unique sequence ID for each trade spread entry before inserting it
        /// into the database. The method ensures that all provided data is processed and inserted in a single
        /// operation.</remarks>
        /// <param name = "optionTradeSpreadsData">A collection of <see cref = "OptionTradeSpreadsDataModel"/> objects representing the option trade spread data to
        /// be inserted. Each item in the collection must contain valid trade details.</param>
        /// <returns>A <see cref = "Task{TResult}"/> representing the asynchronous operation. The result contains the total number of
        /// rows inserted.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task<long> InsertOptionTradeSpreadDataAsyncCoreBinding(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadsData)
        {
            ArgumentNullException.ThrowIfNull(optionTradeSpreadsData);
            var rowCount = 0L;
            var db = context;
            var parameters = new List<InsertOptionTradeSpreadData>();
            foreach (var e in optionTradeSpreadsData)
            {
                var sequenceId = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId))
            .ConfigureAwait(false);
                parameters.Add(new InsertOptionTradeSpreadData(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), sequenceId, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread, e.CreatedOn, e.CreatedBy));
                rowCount++;
            }

            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTradeSpreadData)}", TradeDbSql.InsertOptionTradeSpreadData)
            .SetParameters(parameters)
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert option tradde spread bar data
        /// </summary>
        /// <param name = "e">option trade spread bar data</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertOptionTradeSpreadBarDataAsyncCoreBinding(OptionTradeSpreadBarsDataModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTradeSpreadBarData)}", TradeDbSql.InsertOptionTradeSpreadBarData)
            .SetParameters(new InsertOptionTradeSpreadBarData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.BarDate, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionTradeSpreadBarDataAsync operation.</summary>
        internal async Task<long> InsertOptionTradeSpreadBarDataAsyncCoreBinding(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData)
        {
            ArgumentNullException.ThrowIfNull(optionTradeSpreadBarsData);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertOptionTradeSpreadBarData)}", TradeDbSql.InsertOptionTradeSpreadBarData)
            .SetParameters(EnumerateOptionTradeSpreadBarData().Select(e => new InsertOptionTradeSpreadBarData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.BarDate, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<OptionTradeSpreadBarsDataModel> EnumerateOptionTradeSpreadBarData()
            {
                foreach (var e in optionTradeSpreadBarsData)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade liveFeed
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertTradeLiveFeedAsyncCoreBinding(TradeLiveFeedReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeLiveFeed)}", TradeDbSql.InsertTradeLiveFeed)
            .SetParameters(new InsertTradeLiveFeed(e.OrderId, e.TradeId, e.LiveFeed))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade plan forward loss limit
        /// </summary>
        /// <param name = "e">trade plan forward loss limit</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertTradePlanForwardLossLimitAsyncCoreBinding(TradePlanForwardLossLimitReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var db = context;
            var dbReader = db as ITradeDbReadContext;
            var existingTradePlanForwardLossLimit = await (dbReader!.GetTradePlanForwardLossLimitAsync(e.OrderId, e.TradeId, e.ValueDate, e.TradeType))
            .ConfigureAwait(false);
            if (existingTradePlanForwardLossLimit is not null)
            {
                await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePlanForwardLossLimit)}", TradeDbSql.DeleteTradePlanForwardLossLimit)
            .SetParameters(new DeleteTradePlanForwardLossLimit(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            }

            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlanForwardLossLimit)}", TradeDbSql.InsertTradePlanForwardLossLimit)
            .SetParameters(new InsertTradePlanForwardLossLimit(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), e.LimitType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Inserts a trade placement signal into the database asynchronously and returns the generated sequence ID.
        /// </summary>
        /// <remarks>This method generates a unique sequence ID for the trade placement signal and stores the
        /// provided data in the database. Ensure that all required properties of the <paramref name = "e"/> parameter are
        /// populated before calling this method.</remarks>
        /// <param name = "e">The trade placement signal data to insert, encapsulated in a <see cref = "TradePlacementSignalReadModel"/> object.</param>
        /// <returns>The sequence ID generated for the inserted trade placement signal.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task<long> InsertTradePlacementSignalAsyncCoreBinding(TradePlacementSignalReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var sequenceId = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlacementSignal_SequenceId))
            .ConfigureAwait(false);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlacementSignal)}", TradeDbSql.InsertTradePlacementSignal)
            .SetParameters(new InsertTradePlacementSignal(sequenceId, e.ContractId, e.ValueDate, e.TradePlacementSignal.ToStringFast(), e.TradePrice, e.CreatedOn, e.CreatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return sequenceId;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpdateTradeLiveFeedAsync operation.</summary>
        internal async Task UpdateTradeLiveFeedAsyncCoreBinding(TradeLiveFeedReadModel tradeLiveFeed)
        {
            ArgumentNullException.ThrowIfNull(tradeLiveFeed);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpdateTradeLiveFeed)}", TradeDbSql.UpdateTradeLiveFeed)
            .SetParameters(new UpdateTradeLiveFeed(tradeLiveFeed.OrderId, tradeLiveFeed.TradeId, tradeLiveFeed.LiveFeed))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// update option leg data
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task UpdateOptionLegDataAsyncCoreBinding(OptionTradeLegDataReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpdateOptionLegData)}", TradeDbSql.UpdateOptionLegData)
            .SetParameters(new UpdateOptionLegData(e.OrderId, e.TradeId, e.ValueDate, e.OptionLegId, e.BidPrice, e.AskPrice, e.ImpliedVolatility, e.Delta, e.Gamma, e.Theta, e.Vega, e.Rho, e.UpdatedOn, e.UpdatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade limit for selected trade
        /// </summary>
        /// <param name = "e">trade limit</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertTradeLimitAsyncCoreBinding(TradeLimitReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.TradeFlowProjectMonitoringLimitsAsync(db, e, [], ct))
            .ConfigureAwait(false);
                return true;
            }))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeLimitsAsync operation.</summary>
        internal async Task<long> InsertTradeLimitsAsyncCoreBinding(IEnumerable<TradeLimitReadModel> tradeLimits)
        {
            ArgumentNullException.ThrowIfNull(tradeLimits);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeLimit)}", TradeDbSql.InsertTradeLimit)
            .SetParameters(EnumerateTradeLimits().Select(e => new InsertTradeLimit(e.TradeId, e.TradeType.ToStringFast(), e.RiskMargin, e.MaxProfit, e.MaxLoss, e.MaxReturn, e.MaxLossLimit, e.MinProfitLimit, e.MaxProfitLimit, e.MinProfitTarget, e.DailyProfitTarget, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<TradeLimitReadModel> EnumerateTradeLimits()
            {
                foreach (var e in tradeLimits)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade limit for selected trade
        /// </summary>
        /// <param name = "e">trade limit</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertTradeTypeLimitAsyncCoreBinding(TradeTypeLimitReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.TradeFlowProjectSpreadLimitsAsync(db, [e], ct))
            .ConfigureAwait(false);
                return true;
            }))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeTypeLimitsAsync operation.</summary>
        internal async Task<long> InsertTradeTypeLimitsAsyncCoreBinding(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits)
        {
            ArgumentNullException.ThrowIfNull(tradeTypeLimits);
            var rowCount = 0L;
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradeTypeLimit)}", TradeDbSql.InsertTradeTypeLimit)
            .SetParameters(EnumerateTradeTypeLimits().Select(e => new InsertTradeTypeLimit(e.TradeId, e.TradeType.ToStringFast(), e.MaxLossLimit, e.MinProfitLimit, e.MaxProfitLimit)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            return rowCount;
            IEnumerable<TradeTypeLimitReadModel> EnumerateTradeTypeLimits()
            {
                foreach (var e in tradeTypeLimits)
                {
                    rowCount++;
                    yield return e;
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// insert trade plan
        /// </summary>
        /// <param name = "e">trade plan</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task InsertTradePlanAsyncCoreBinding(TradePlanReadModel e)
        {
            ArgumentNullException.ThrowIfNull(e);
            var sequenceId = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId))
            .ConfigureAwait(false);
            var db = context;
            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlan)}", TradeDbSql.InsertTradePlan)
            .SetParameters(new InsertTradePlan(sequenceId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.ValueDate, e.MaturityDate, e.ActionDate, e.ActionType.ToStringFast(), e.ActionSubType.ToStringFast(), e.ActionState.ToStringFast(), e.ActionReason, e.TradePnl, e.ForwardLossRatio, e.LossProbability, e.MScore, e.MaxProfit, e.MaxLoss, e.MinProfitTarget, e.DailyProfitTarget, e.AssetPrice, e.AssetStdDev, e.AssetMean, e.AssetPriceChange, e.MarketTrend.ToStringFast(), e.MarketVolatility.ToStringFast(), e.MarketDirection.ToStringFast(), e.VixVolatility.ToStringFast(), e.TradeRisk.ToStringFast(), e.FiftyDayMA, e.FiveDayXMA, e.PutOTMProbability, e.CallOTMProbability, e.ShortPutGamma, e.ShortCallGamma, e.GammaRisk.ToStringFast(), e.NetPrice, e.ForwardPrice, e.ForwardDelta, e.StopLossLimit, e.TrendType.ToStringFast(), e.TrendStrength.ToStringFast(), e.RSI, e.RSISlope, e.TDI.ToStringFast(), e.TDIStrength.ToStringFast(), e.CreatedOn, e.CreatedBy))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlanForwardLossRatio)}", TradeDbSql.InsertTradePlanForwardLossRatio)
            .SetParameters(new InsertTradePlanForwardLossRatio(1, e.ValueDate, e.ForwardLossRatio, sequenceId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// Inserts a collection of trade plans into the database asynchronously.
        /// </summary>
        /// <remarks>This method processes the provided trade plans in batches of 1,000 to optimize database
        /// operations. Each trade plan is assigned a unique sequence ID before being inserted into the database. The method
        /// performs two database operations for each batch: 1. Inserts the trade plans into the main trade plan table. 2.
        /// Inserts forward loss ratio data into a separate table for analytics purposes.  The method is designed to handle
        /// large collections of trade plans efficiently. Ensure that the input collection is not null and contains valid
        /// trade plan data to avoid runtime exceptions.</remarks>
        /// <param name = "tradePlans">A collection of <see cref = "TradePlanByIdReadModel"/> objects representing the trade plans to be inserted. Each
        /// trade plan must contain valid data for insertion, including required fields such as order ID, trade ID, and
        /// trade type.</param>
        /// <returns>A task that represents the asynchronous operation. The task completes when all trade plans have been
        /// successfully inserted.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task InsertTradePlansAsyncCoreBinding(ICollection<TradePlanByIdReadModel> tradePlans)
        {
            ArgumentNullException.ThrowIfNull(tradePlans);
            var db = context;
            for (var index = 0; index < tradePlans.Count; index += 2000)
            {
                var tradePlanPage = new List<TradePlanByIdReadModel>(2000);
                var tradePlanQry = tradePlans.Skip(index).Take(2000);
                foreach (var e in tradePlanQry)
                {
                    var sequenceId = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.TradePlan_SequenceId))
            .ConfigureAwait(false);
                    tradePlanPage.Add(e with { Id = sequenceId });
                }

                await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlan)}", TradeDbSql.InsertTradePlan)
            .SetParameters(tradePlanPage.Select(e => new InsertTradePlan(e.Id, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.ValueDate, e.MaturityDate, e.ActionDate, e.ActionType.ToStringFast(), e.ActionSubType.ToStringFast(), e.ActionState.ToStringFast(), e.ActionReason, e.TradePnl, e.ForwardLossRatio, e.LossProbability, e.MScore, e.MaxProfit, e.MaxLoss, e.MinProfitTarget, e.DailyProfitTarget, e.AssetPrice, e.AssetStdDev, e.AssetMean, e.AssetPriceChange, e.MarketTrend.ToStringFast(), e.MarketVolatility.ToStringFast(), e.MarketDirection.ToStringFast(), e.VixVolatility.ToStringFast(), e.TradeRisk.ToStringFast(), e.FiftyDayMA, e.FiveDayXMA, e.PutOTMProbability, e.CallOTMProbability, e.ShortPutGamma, e.ShortCallGamma, e.GammaRisk.ToStringFast(), e.NetPrice, e.ForwardPrice, e.ForwardDelta, e.StopLossLimit, e.TrendType.ToStringFast(), e.TrendStrength.ToStringFast(), e.RSI, e.RSISlope, e.TDI.ToStringFast(), e.TDIStrength.ToStringFast(), e.CreatedOn, e.CreatedBy)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
                await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertTradePlanForwardLossRatio)}", TradeDbSql.InsertTradePlanForwardLossRatio)
            .SetParameters(tradePlanPage.Select(e => new InsertTradePlanForwardLossRatio(1, e.ValueDate, e.ForwardLossRatio, e.Id)))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>
        /// delete selected trade position state
        /// </summary>
        /// <param name = "e"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task DeleteTradePositionStateAsyncCoreBinding(OptionTradeEntityId e)
        {
            ArgumentNullException.ThrowIfNull(e);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePositionState)}", TradeDbSql.DeleteTradePositionState)
            .SetParameters(new DeleteTradePositionState(e.OrderId, e.TradeId))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the DeleteTradePlanForwardLossLimitAsync operation.</summary>
        internal async Task DeleteTradePlanForwardLossLimitAsyncCoreBinding(TradePlanForwardLossLimitEntityId id)
        {
            ArgumentNullException.ThrowIfNull(id);
            await (context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.DeleteTradePlanForwardLossLimit)}", TradeDbSql.DeleteTradePlanForwardLossLimit)
            .SetParameters(new DeleteTradePlanForwardLossLimit(id.OrderId, id.TradeId, id.ValueDate, id.TradeType.ToStringFast()))
            .ExecuteCommandAsync())
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertIntrinsicTimeStrategyWorkflowAsync operation.</summary>
        internal async Task UpsertIntrinsicTimeStrategyWorkflowAsyncCoreBinding(IntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpsertIntrinsicTimeStrategyWorkflow)}", TradeDbSql.UpsertIntrinsicTimeStrategyWorkflow)
            .SetParameters(new UpsertIntrinsicTimeStrategyWorkflow(workflow.WorkflowId.Value, workflow.WorkflowEntityId, workflow.WorkflowDefinitionId, workflow.WorkflowDefinitionVersion, workflow.ContractId, workflow.TimeFrameStartValueDate, workflow.TimePeriod.ToString(), workflow.TriggerEventId, workflow.CorrelationId, workflow.Status.ToString(), workflow.Outcome.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.LastEventId, workflow.StateSchemaVersion, workflow.StatePayload.ToArray(), workflow.StopReasonCode, workflow.StartedAtUtc, workflow.TerminalAtUtc, workflow.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertActiveIntrinsicTimeStrategyWorkflowAsync operation.</summary>
        internal async Task UpsertActiveIntrinsicTimeStrategyWorkflowAsyncCoreBinding(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpsertActiveIntrinsicTimeStrategyWorkflow)}", TradeDbSql.UpsertActiveIntrinsicTimeStrategyWorkflow)
            .SetParameters(new UpsertActiveIntrinsicTimeStrategyWorkflow(workflow.WorkflowEntityId, workflow.WorkflowId.Value, workflow.ContractId, workflow.TimeFrameStartValueDate, workflow.TimePeriod.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.LastEventId, workflow.StateSchemaVersion, workflow.StatePayload.ToArray(), workflow.StartedAtUtc, workflow.UpdatedAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync operation.</summary>
        internal async Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsyncCoreBinding(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(attempt);
            attempt.RequireNotNull(nameof(attempt));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)}", TradeDbSql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)
            .SetParameters(new InsertIntrinsicTimeStrategyWorkflowStartAttempt(attempt.WorkflowEntityId, attempt.RequestedAtUtc, attempt.RequestedWorkflowId.Value, attempt.Decision.ToString(), attempt.ActiveWorkflowId?.Value, attempt.StartCommandId, attempt.TriggerEventId, attempt.ActiveStage.ToString(), attempt.ReasonCode, attempt.SourceEventId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertIntrinsicTimeStrategyWorkflowTimelineAsync operation.</summary>
        internal async Task InsertIntrinsicTimeStrategyWorkflowTimelineAsyncCoreBinding(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(timelineEvent);
            timelineEvent.RequireNotNull(nameof(timelineEvent));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.InsertIntrinsicTimeStrategyWorkflowTimeline)}", TradeDbSql.InsertIntrinsicTimeStrategyWorkflowTimeline)
            .SetParameters(new InsertIntrinsicTimeStrategyWorkflowTimeline(timelineEvent.WorkflowId.Value, timelineEvent.EventId, timelineEvent.WorkflowEntityId, timelineEvent.WorkflowRevision, timelineEvent.Stage.ToString(), timelineEvent.EventName, timelineEvent.EventSchemaVersion, timelineEvent.EventPayload.ToArray(), timelineEvent.OccurredAtUtc))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertIntrinsicTimeStrategyWorkflowByEntityAsync operation.</summary>
        internal async Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsyncCoreBinding(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpsertIntrinsicTimeStrategyWorkflowByEntity)}", TradeDbSql.UpsertIntrinsicTimeStrategyWorkflowByEntity)
            .SetParameters(TradeDbContext.MapToByEntityParameters(workflow))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync operation.</summary>
        internal async Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsyncCoreBinding(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)}", TradeDbSql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)
            .SetParameters(new UpsertIntrinsicTimeStrategyWorkflowByStatusDay(workflow.Status.ToString(), DateOnly.FromDateTime(workflow.StartedAtUtc), workflow.StartedAtUtc, workflow.WorkflowId.Value, workflow.WorkflowEntityId, workflow.Outcome.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.TerminalAtUtc, workflow.StopReasonCode))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertMarketConditionAsync operation.</summary>
        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task UpsertMarketConditionAsyncCoreBinding(MarketConditionReadModel result, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.RequireNotNull(nameof(result));
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpsertMarketCondition)}", TradeDbSql.UpsertMarketCondition)
            .SetParameters(new UpsertMarketCondition(result.WorkflowId.Value, result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.FundId, result.InstrumentRoot, result.TargetHorizon.ToString(), result.ParameterSetId, result.ParameterSetVersion, result.ParameterPayloadSha256, result.SnapshotId, result.SnapshotSha256, result.Tradeability.ToString(), result.ConditionType.ToString(), result.Direction.ToString(), result.Phase.ToString(), result.Strength, result.Confidence, result.PrimaryReasonCode, result.ResultPayload.ToArray(), result.ResultPayloadSha256, result.EvaluatedAtUtc, result.ValidUntilUtc, result.MarketDataAsOfUtc, result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(), result.LiquidityQuality.ToString(), result.DataQuality.ToString(), result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(), result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(), result.ReasonsPayload.ToArray(), result.SummaryText))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
            await context.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.UpsertMarketConditionByFund)}", TradeDbSql.UpsertMarketConditionByFund)
            .SetParameters(new UpsertMarketConditionByFund(result.FundId, result.InstrumentRoot, result.TargetHorizon.ToString(), result.EvaluatedAtUtc, result.WorkflowId.Value, result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.ParameterSetId, result.ParameterSetVersion, result.ParameterPayloadSha256, result.SnapshotId, result.SnapshotSha256, result.Tradeability.ToString(), result.ConditionType.ToString(), result.Direction.ToString(), result.Phase.ToString(), result.Strength, result.Confidence, result.PrimaryReasonCode, result.ResultPayload.ToArray(), result.ResultPayloadSha256, result.ValidUntilUtc, result.MarketDataAsOfUtc, result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(), result.LiquidityQuality.ToString(), result.DataQuality.ToString(), result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(), result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(), result.ReasonsPayload.ToArray(), result.SummaryText))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertMarketConditionAssessmentAsync operation.</summary>
        internal async Task UpsertMarketConditionAssessmentAsyncCoreBinding(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.PipelineProjectAsync(db, completed, ct))
            .ConfigureAwait(false);
                return true;
            }, cancellationToken))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetMarketConditionAssessmentHistoryAsync operation.</summary>
        internal Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsyncCoreBinding(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            profile.RequireAssessmentHistoryScope(root, horizon, beforeUtc, pageSize);
            return context.NativeTransaction().ExecuteOnceAsync<ICollection<MarketConditionAssessmentCompletedEvent>>(async (db, ct) => (await (db.QueryAsync(TradeDbSql.HelperStatement6, [profile, root, horizon.ToString(), beforeUtc, pageSize], r => (r.GetFieldValue<byte[]>(0)).VerifyAssessment(r.GetString(1)), ct))
            .ConfigureAwait(false)).ToArray(), cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertOrderCompositionAsync operation.</summary>
        internal async Task UpsertOrderCompositionAsyncCoreBinding(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.PipelineProjectAsync(db, completed, ct))
            .ConfigureAwait(false);
                return true;
            }, token))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertRegimeDiscoveryAsync operation.</summary>
        internal async Task UpsertRegimeDiscoveryAsyncCoreBinding(RegimeDiscoveryReadModel result, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.RequireNotNull(nameof(result));
            await context.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.RegimeUpsertAsync(db, result, ct), cancellationToken)
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertRiskHistoryAsync operation.</summary>
        internal async Task<RiskHistoryProjectionResult> UpsertRiskHistoryAsyncCoreBinding(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var row = RiskHistoryIdentity.Row(snapshot);
            if (row is null)
                return new(RiskHistoryProjectionDisposition.NotApplicable);
            token.ThrowIfCancellationRequested();
            // The exact invocation row and its scoped summary are one rebuildable projection unit.
            // Once the unit starts, finish every idempotent Scylla statement even if the recovery
            // worker is asked to stop; otherwise shutdown can leave an exact row without its history
            // row and surface cancellation from the second statement as an unhandled worker error.
            var projectionToken = CancellationToken.None;
            if (snapshot.WorkflowId != snapshot.State.WorkflowId || snapshot.WorkflowRevision != row.Revision || snapshot.EntityId != snapshot.State.EntityId)
                throw new InvalidDataException("Risk history source identity mismatch.");
            var canonical = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(MessagePackBinarySerializer.Shared.Serialize(snapshot with { EventId = 0 }));
            var payload = MessagePackBinarySerializer.Shared.Serialize(canonical);
            var hash = RiskContracts.Hash(canonical);
            var inserted = await (context.Use("RiskHistory.Insert", TradeDbSql.HelperStatement7)
            .SetParameters(new RiskValues([row.WorkflowId, row.InvocationId, row.Revision, payload, hash]))
            .ExecuteSingleAsync(r => r.GetBool(0), projectionToken))
            .ConfigureAwait(false);
            if (!inserted)
            {
                var old = await (context.Use("RiskHistory.Verify", TradeDbSql.HelperStatement8)
            .SetParameters(new RiskValues([row.WorkflowId, row.InvocationId, row.Revision]))
            .ExecuteSingleAsync(r => r.GetString(0), projectionToken))
            .ConfigureAwait(false);
                if (!string.Equals(old, hash, StringComparison.Ordinal))
                    return new(RiskHistoryProjectionDisposition.Conflict, row.WorkflowId, row.InvocationId, row.Revision, old, hash);
            }

            var disposition = inserted ? RiskHistoryProjectionDisposition.Projected : RiskHistoryProjectionDisposition.AlreadyProjected;
            var summary = MessagePackBinarySerializer.Shared.Serialize(row);
            var added = await (context.Use("RiskHistory.SummaryInsert", TradeDbSql.HelperStatement9)
            .SetParameters(new RiskValues([row.PortfolioId, row.FundId, row.ValueDate, row.EvaluatedAtUtc, row.InvocationId, row.Revision, summary]))
            .ExecuteSingleAsync(r => r.GetBool(0), projectionToken))
            .ConfigureAwait(false);
            if (!added)
                await (context.Use("RiskHistory.SummaryAdvance", TradeDbSql.HelperStatement10)
            .SetParameters(new RiskValues([row.Revision, summary, row.PortfolioId, row.FundId, row.ValueDate, row.EvaluatedAtUtc, row.InvocationId, row.Revision]))
            .ExecuteSingleAsync(r => r.GetBool(0), projectionToken))
            .ConfigureAwait(false);
            return new(disposition, row.WorkflowId, row.InvocationId, row.Revision, hash, hash);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertTradeOrderAsync operation.</summary>
        internal Task UpsertTradeOrderAsyncCoreBinding(TradeOrderDefinition order, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(order);
            order.RequireNotNull(nameof(order));
            throw new InvalidOperationException("PostgreSQL order projections require the accepted source timestamp.");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertOrderExecutionAsync operation.</summary>
        internal async Task UpsertOrderExecutionAsyncCoreBinding(OrderExecutionDefinition execution, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            execution.RequireNotNull(nameof(execution));
            execution.TradeOrderId.Require();
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.TradeFlowProjectExecutionAsync(db, execution, ct))
            .ConfigureAwait(false);
                return true;
            }, token))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertEstablishedTradeAsync operation.</summary>
        internal async Task UpsertEstablishedTradeAsyncCoreBinding(EstablishedTradeDefinition trade, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(trade);
            trade.RequireNotNull(nameof(trade));
            trade.Id.Require();
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.TradeFlowProjectTradeAsync(db, trade, ct))
            .ConfigureAwait(false);
                return true;
            }, token))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertStrategyPositionAsync operation.</summary>
        internal async Task UpsertStrategyPositionAsyncCoreBinding(StrategyPositionSnapshot position, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(position);
            position.RequireNotNull(nameof(position));
            position.RequireValidIdentity();
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.TradeFlowProjectPositionAsync(db, position, ct))
            .ConfigureAwait(false);
                return true;
            }, token))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ReplaceOpenPositionRoutesAsync operation.</summary>
        internal async Task ReplaceOpenPositionRoutesAsyncCoreBinding(StrategyPositionSnapshot position, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(position);
            position.RequireNotNull(nameof(position));
            position.RequireRoutableLegs();
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.TradeFlowProjectPositionAsync(db, position, ct))
            .ConfigureAwait(false);
                return true;
            }, token))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetOpenPositionRoutesAsync operation.</summary>
        internal async Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsyncCoreBinding(string contractId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(contractId);
            contractId.RequireContractId();
            return (await (context.NativeTransaction().ExecuteOnceAsync((db, ct) => TradeDbContextExtensions.TradeReadRoutesAsync(db, contractId, ct), token))
            .ConfigureAwait(false)).ToArray();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertTradeSelectionAsync operation.</summary>
        internal async Task UpsertTradeSelectionAsyncCoreBinding(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
            {
                await (TradeDbContextExtensions.PipelineProjectAsync(db, completed, ct))
            .ConfigureAwait(false);
                return true;
            }, cancellationToken))
            .ConfigureAwait(false);
            return;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the NativeTransaction persistence helper.</summary>
        internal EventSourceDb.PostgresEventTransaction NativeTransaction() => new(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection, context.ConnectionString, "System.Data.Postgres"));
        /// <summary>Hydrates an option trade with its persisted positions, legs, limits, and fills.</summary>
        internal async Task<OptionTradeReadModel> FillOptionTradeAsync(OptionTradeReadModel optionTrade)
        {
            var entityId = optionTrade.EntityId;
            var db = context;
            var tradePositionsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositions)}", TradeDbSql.GetTradePositions)
            .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
            .ExecuteQueryAsync(TradeDbContext.MapToTradePosition!);
            var optionLegsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsByOrderAndTrade)}", TradeDbSql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
            .ExecuteQueryAsync(TradeDbContext.MapToOptionLeg!);
            var tradeLimitTask = context.GetTradeLimitAsync(optionTrade.TradeId);
            var tradeTypeLimitsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeTypeLimits)}", TradeDbSql.GetTradeTypeLimits)
            .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
            .ExecuteQueryAsync(TradeDbContext.MapToTradeTypeLimit);
            var tradeFillsTask = context.GetTradeFillsAsync(optionTrade.OrderId, optionTrade.TradeId);
            var tradePositions = await (tradePositionsTask)
            .ConfigureAwait(false);
            var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
            var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
            await (Parallel.ForEachAsync(Enumerable.Range(0, valueDates.Length), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (dateIndex, _) => legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(entityId.OrderId, entityId.TradeId, valueDates[dateIndex]))
            .ExecuteQueryAsync(TradeDbContext.MapToOptionLegData!))
            .ConfigureAwait(false)))
            .ConfigureAwait(false);
            await (Task.WhenAll(optionLegsTask, tradeLimitTask, tradeTypeLimitsTask, tradeFillsTask))
            .ConfigureAwait(false);
            var optionLegs = await (optionLegsTask)
            .ConfigureAwait(false);
            var optionLegById = optionLegs.ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
            var legDataByPosition = TradeDbContext.MapToLegDataByPosition(legDataByDate, optionLegById, requireOptionLeg: true);
            foreach (var position in tradePositions)
            {
                if (legDataByPosition.TryGetValue(TradeDbContext.MapToPositionKey(position), out var legData))
                    position.AddOptionLegData(legData);
            }

            return optionTrade.AddOptionLegs(optionLegs).AddTradePosition(tradePositions).SetTradeLimit((await (tradeLimitTask)
            .ConfigureAwait(false))!).AddTradeTypeLimits(await (tradeTypeLimitsTask)
            .ConfigureAwait(false)).AddTradeFills(await (tradeFillsTask)
            .ConfigureAwait(false));
        }

        /// <summary>Hydrates an option trade with its persisted graph while observing cancellation.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task<OptionTradeReadModel> FillOptionTradeAsync(OptionTradeReadModel optionTrade, CancellationToken cancellationToken)
        {
            var entityId = optionTrade.EntityId;
            var db = context;
            var tradePositionsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradePositions)}", TradeDbSql.GetTradePositions)
            .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
            .ExecuteQueryAsync(TradeDbContext.MapToTradePosition!, cancellationToken);
            var optionLegsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegsByOrderAndTrade)}", TradeDbSql.GetOptionLegsByOrderAndTrade)
            .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
            .ExecuteQueryAsync(TradeDbContext.MapToOptionLeg!, cancellationToken);
            var tradeLimitTask = context.GetTradeLimitAsync(optionTrade.TradeId, cancellationToken);
            var tradeTypeLimitsTask = db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetTradeTypeLimits)}", TradeDbSql.GetTradeTypeLimits)
            .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
            .ExecuteQueryAsync(TradeDbContext.MapToTradeTypeLimit, cancellationToken);
            var tradeFillsTask = context.GetTradeFillsAsync(optionTrade.OrderId, optionTrade.TradeId, cancellationToken);
            var tradePositions = await (tradePositionsTask)
            .ConfigureAwait(false);
            var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
            var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
            await (Parallel.ForEachAsync(Enumerable.Range(0, valueDates.Length), new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (dateIndex, token) => legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbSql)}.{nameof(TradeDbSql.GetOptionLegData)}", TradeDbSql.GetOptionLegData)
            .SetParameters(new GetOptionLegData(entityId.OrderId, entityId.TradeId, valueDates[dateIndex]))
            .ExecuteQueryAsync(TradeDbContext.MapToOptionLegData!, token))
            .ConfigureAwait(false)))
            .ConfigureAwait(false);
            await (Task.WhenAll(optionLegsTask, tradeLimitTask, tradeTypeLimitsTask, tradeFillsTask))
            .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var optionLegs = await (optionLegsTask)
            .ConfigureAwait(false);
            var optionLegById = optionLegs.ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
            var legDataByPosition = TradeDbContext.MapToLegDataByPosition(legDataByDate, optionLegById, requireOptionLeg: true);
            foreach (var position in tradePositions)
            {
                if (legDataByPosition.TryGetValue(TradeDbContext.MapToPositionKey(position), out var legData))
                    position.AddOptionLegData(legData);
            }

            return optionTrade.AddOptionLegs(optionLegs).AddTradePosition(tradePositions).SetTradeLimit((await (tradeLimitTask)
            .ConfigureAwait(false))!).AddTradeTypeLimits(await (tradeTypeLimitsTask)
            .ConfigureAwait(false)).AddTradeFills(await (tradeFillsTask)
            .ConfigureAwait(false));
        }
    }

    extension(int pageSize)
    {
        /// <summary>Validates a requested TradeDb query page size.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal int RequirePageSize()
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
            return pageSize;
        }
    }

    extension(DateOnly endDate)
    {
        /// <summary>Requires an inclusive date range whose end does not precede its start.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireNotBefore(DateOnly startDate)
        {
            if (endDate < startDate)
                throw new ArgumentOutOfRangeException(nameof(endDate), endDate, "End date must not precede start date.");
        }
    }

    extension(int fundId)
    {
        /// <summary>Validates the market-condition history partition and requested page size.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireMarketConditionHistoryScope(string instrumentRoot, int pageSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fundId);
            if (string.IsNullOrWhiteSpace(instrumentRoot))
                throw new ArgumentException("An instrument root is required.", nameof(instrumentRoot));
            if (pageSize is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
    }

    extension(string profile)
    {
        /// <summary>Validates the market-condition assessment history partition and page bound.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireAssessmentHistoryScope(string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(profile) || !string.Equals(root, "ES", StringComparison.Ordinal) || !MarketConditionAssessmentParameterSet.IsHorizon(horizon) || pageSize is < 1 or > 100 || !MarketConditionAssessmentContracts.Utc(beforeUtc))
                throw new ArgumentException("Invalid assessment history partition or bound.", nameof(profile));
        }

        /// <summary>Requires a non-empty routed contract identifier.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireContractId()
        {
            if (string.IsNullOrWhiteSpace(profile))
                throw new ArgumentException("ContractId is required.", "contractId");
        }
    }

    extension(MarketConditionAssessmentCompletedEvent completed)
    {
        /// <summary>Requires the assessment snapshot hashes to match the projected result.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireAssessmentProjectionIdentity(MarketConditionAssessmentResult result)
        {
            if (!string.Equals(completed.Snapshot.ComputeHash(), result.SnapshotSha256, StringComparison.Ordinal) || !string.Equals(completed.Snapshot.PayloadSha256, result.SnapshotSha256, StringComparison.Ordinal))
                throw new ArgumentException("Assessment projection snapshot hash mismatch.", nameof(result));
        }
    }

    extension(StrategyWorkflowId workflowId)
    {
        /// <summary>Requires exact workflow and invocation identities.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireInvocation(Guid invocationId, string message)
        {
            if (workflowId.Value == Guid.Empty || invocationId == Guid.Empty)
                throw new ArgumentException(message, nameof(workflowId));
        }
    }

    extension(Guid identifier)
    {
        /// <summary>Requires exact workflow and invocation identifiers.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireInvocation(Guid invocationId, string message)
        {
            if (identifier == Guid.Empty || invocationId == Guid.Empty)
                throw new ArgumentException(message, nameof(identifier));
        }

        /// <summary>Requires a non-empty identifier.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void Require(string message, string parameterName)
        {
            if (identifier == Guid.Empty)
                throw new ArgumentException(message, parameterName);
        }

        /// <summary>Validates a position-history identity, UTC range, and page size.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireUtcHistoryScope(DateTime fromUtc, DateTime toUtc, int pageSize)
        {
            if (identifier == Guid.Empty || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("Valid PositionId, UTC range, and page size 1..1000 are required.", nameof(identifier));
        }
    }

    extension(int portfolioId)
    {
        /// <summary>Validates a portfolio/fund/date history partition and page size.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireHistoryScope(int fundId, DateOnly valueDate, int pageSize, int maximumPageSize, string message)
        {
            if (portfolioId <= 0 || fundId <= 0 || valueDate == default || pageSize < 1 || pageSize > maximumPageSize)
                throw new ArgumentException(message, nameof(portfolioId));
        }

        /// <summary>Validates a portfolio/fund UTC history range and page size.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireUtcHistoryScope(int fundId, DateTime fromUtc, DateTime toUtc, int pageSize)
        {
            if (portfolioId <= 0 || fundId <= 0 || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("Valid ownership, UTC range, and page size 1..1000 are required.", nameof(portfolioId));
        }
    }

    extension(StrategyPositionSnapshot position)
    {
        /// <summary>Requires a strategy position with a valid identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireValidIdentity()
        {
            if (!position.Id.IsValid)
                throw new ArgumentException("Valid position identity is required.", nameof(position));
        }

        /// <summary>Requires every routed position leg to identify its contract.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireRoutableLegs()
        {
            if (position.Legs.Any(static leg => string.IsNullOrWhiteSpace(leg.ContractId)))
                throw new ArgumentException("Every routed position leg requires ContractId.", nameof(position));
        }
    }

    extension(StrategyPositionId id)
    {
        /// <summary>Requires a valid strategy-position identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void Require()
        {
            if (!id.IsValid)
                throw new ArgumentException("Valid position identity is required.", nameof(id));
        }
    }

    extension(TradeEntityId id)
    {
        /// <summary>Requires a valid portfolio, fund, order, and trade identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void Require()
        {
            if (!id.IsValid)
                throw new ArgumentException("Valid Portfolio, Fund, Order, and Trade identity is required.", nameof(id));
        }
    }

    extension(TradeOrderId id)
    {
        /// <summary>Requires a valid portfolio, fund, and order identity.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void Require()
        {
            if (!id.IsValid)
                throw new ArgumentException("Valid Portfolio, Fund, and Order identity is required.", nameof(id));
        }
    }

    extension<T>(T? value)
        where T : class
    {
        /// <summary>Requires a non-null TradeDb contract argument and returns it.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "The validated member belongs to this public input argument; explicitly report its root parameter rather than exposing a member expression as the parameter name.")]
        internal T RequireNotNull(string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);
            return value;
        }
    }

    extension(byte[] bytes)
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the VerifyAssessment persistence helper.</summary>
        internal MarketConditionAssessmentCompletedEvent VerifyAssessment(string hash)
        {
            if (!string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)), hash, StringComparison.Ordinal))
                throw new InvalidOperationException("Stored assessment payload hash mismatch.");
            var completed = MessagePackSerializer.Deserialize<MarketConditionAssessmentCompletedEvent>(bytes);
            var result = MarketConditionAssessmentContracts.ReadResult(completed.Result);
            if (completed.WorkflowId != result.WorkflowId || !string.Equals(completed.Snapshot.PayloadSha256, result.SnapshotSha256, StringComparison.Ordinal) || !string.Equals(completed.Snapshot.ComputeHash(), result.SnapshotSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("Stored assessment snapshot or workflow identity mismatch.");
            return completed;
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Projects lifecycle state after its source append, retaining immutable revision terms.</summary>
        public async Task<int> TradeFlowProjectOrderLifecycleAsync(TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(order);
            var identity = new object?[] { order.Id.PortfolioId, order.Id.FundId, order.Id.OrderId, order.Revision };
            var existing = await db.ScalarAsync(
                "SELECT payload FROM trade.trade_order WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND revision=$4 FOR UPDATE;",
                identity, token).ConfigureAwait(false);
            if (existing is not byte[] payload)
                return await db.TradeFlowProjectOrderAsync(order, acceptedAtUtc, token).ConfigureAwait(false);
            var stored = MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>(payload);
            // Status and execution binding belong to lifecycle state, not the accepted terms.
            static byte[] Terms(TradeOrderDefinition value) => MessagePackBinarySerializer.Shared.Serialize(value with
            {
                Status = TradeOrderStatus.Draft, BoundExecutionAttemptId = null,
                BoundExecutionChannel = null, ExecutionBoundAtUtc = null
            });
            if (!Terms(stored).AsSpan().SequenceEqual(Terms(order)))
                throw new InvalidDataException("Accepted order revision conflicts with different source facts.");
            return await db.ExecuteAsync(
                "UPDATE trade.trade_order SET status=$5,payload=$6,updatedatutc=$7 WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND revision=$4;",
                [.. identity, order.Status.ToString(), MessagePackBinarySerializer.Shared.Serialize(order), acceptedAtUtc], token).ConfigureAwait(false);
        }

        /// <summary>Stores the accepted immutable order revision, retaining its operational value date.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task<int> TradeFlowProjectOrderAsync(TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(order);
            var stored = await (db.ScalarAsync(TradeDbSql.HelperStatement11, [order.Id.PortfolioId, order.Id.FundId, order.Id.OrderId, order.Revision, order.Status.ToString(), order.ValueDate, acceptedAtUtc, order.DefinitionHash, MessagePackBinarySerializer.Shared.Serialize(order)], token))
            .ConfigureAwait(false);
            if (stored is null)
                throw new InvalidDataException("Accepted order revision conflicts with different source facts.");
            return 1;
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Stores the accepted execution and all recorded fills in the same transaction.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task TradeFlowProjectExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(execution);
            var id = execution.TradeOrderId;
            await (db.ExecuteAsync(TradeDbSql.HelperStatement12, [id.PortfolioId, id.FundId, id.OrderId, execution.ExecutionAttemptId, execution.Status.ToString(), execution.StartedAtUtc, execution.CompletedAtUtc, MessagePackBinarySerializer.Shared.Serialize(execution)], token))
            .ConfigureAwait(false);
            foreach (var fill in execution.Fills)
                await (db.ExecuteAsync(TradeDbSql.HelperStatement13, [fill.ExecutionAttemptId, fill.ExecutionFillId, fill.ComponentId, fill.TradeLegId, fill.ContractId, fill.FilledAtUtc, MessagePackBinarySerializer.Shared.Serialize(fill)], token))
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Stores the established trade and its dated evidence history together.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task TradeFlowProjectTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(trade);
            if (trade.MonitoringLimits is { } limits)
            {
                if (limits.PortfolioApprovalId == Guid.Empty || limits.OrderRevision < 1 || limits.TradeLimit.TradeId != trade.Id.TradeId
                    || limits.SpreadLimits.Any(limit => limit.TradeId != trade.Id.TradeId))
                    throw new InvalidDataException("Trade monitoring limits do not match their approved trade.");
                await db.TradeFlowProjectMonitoringLimitsAsync(limits.TradeLimit, limits.SpreadLimits, token, initializeOnly: true).ConfigureAwait(false);
            }
            var id = trade.Id;
            var payload = MessagePackBinarySerializer.Shared.Serialize(trade);
            var historyApplied = await db.ScalarAsync(TradeDbSql.HelperStatement14, [id.PortfolioId, id.FundId, trade.StrategyKind.ToString(), trade.EstablishedAtUtc, id.OrderId, id.TradeId, trade.EvidenceRevision, payload], token)
            .ConfigureAwait(false);
            if (historyApplied is null)
                throw new InvalidDataException("Accepted trade history revision conflicts with different source facts.");
            var currentApplied = await db.ScalarAsync(TradeDbSql.HelperStatement15, [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, trade.AssetFamily.ToString(), trade.StrategyKind.ToString(), trade.EstablishedAtUtc, trade.EvidenceRevision, payload], token)
            .ConfigureAwait(false);
            if (currentApplied is null)
            {
                var currentRevision = await db.ScalarAsync(TradeDbSql.HelperStatement16, [id.PortfolioId, id.FundId, id.OrderId, id.TradeId], token)
            .ConfigureAwait(false);
                if (Convert.ToInt32(currentRevision, System.Globalization.CultureInfo.InvariantCulture) == trade.EvidenceRevision)
                    throw new InvalidDataException("Accepted current trade revision conflicts with different source facts.");
            }
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Atomically updates current state, the stable daily history row and both open-leg routing indexes.
        /// Older position sequences cannot overwrite current state or restore routes for an already closed position.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task TradeFlowProjectPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(position);
            var id = position.Id.Trade;
            var payload = MessagePackBinarySerializer.Shared.Serialize(position);
            var applied = await (db.ScalarAsync(TradeDbSql.HelperStatement17, [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.Id.PositionId, position.StrategyKind.ToString(), position.PositionSequence, position.RouteGeneration, position.IsOpen, position.AsOfUtc, payload], token))
            .ConfigureAwait(false);
            if (applied is null)
            {
                var current = await (db.ScalarAsync(TradeDbSql.HelperStatement18, [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.Id.PositionId], token))
            .ConfigureAwait(false);
                if (Convert.ToInt64(current, System.Globalization.CultureInfo.InvariantCulture) == position.PositionSequence)
                    throw new InvalidDataException("Accepted position sequence conflicts with different source facts.");
                return;
            }

            await (db.ExecuteAsync(TradeDbSql.HelperStatement19, [position.Id.PositionId, position.HistoryAsOfUtc == default ? position.AsOfUtc : position.HistoryAsOfUtc, position.HistoryPositionSequence == 0 ? position.PositionSequence : position.HistoryPositionSequence, position.Phase.ToString(), payload, position.PositionSequence], token))
            .ConfigureAwait(false);
            await (db.ExecuteAsync(TradeDbSql.HelperStatement20, [position.Id.PositionId], token))
            .ConfigureAwait(false);
            await (db.ExecuteAsync(TradeDbSql.HelperStatement21, [position.Id.PositionId], token))
            .ConfigureAwait(false);
            if (!position.IsOpen)
                return;
            foreach (var leg in position.Legs)
            {
                await (db.ExecuteAsync(TradeDbSql.HelperStatement22, [leg.ContractId, id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.Id.PositionId, leg.TradeLegId, position.StrategyKind.ToString(), position.RouteGeneration], token))
            .ConfigureAwait(false);
                await (db.ExecuteAsync(TradeDbSql.HelperStatement23, [position.Id.PositionId, leg.TradeLegId, leg.ContractId, id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.StrategyKind.ToString(), position.RouteGeneration], token))
            .ConfigureAwait(false);
            }
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        public async Task TradeFlowProjectMonitoringLimitsAsync(TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit, IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads, CancellationToken token = default, bool initializeOnly = false)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(limit);
            await (db.ExecuteAsync(initializeOnly ? TradeDbSql.InsertInitialMonitoringLimit : TradeDbSql.HelperStatement24, [limit.TradeId, limit.TradeType.ToString(), limit.RiskMargin, limit.MaxProfit, limit.MaxLoss, limit.MaxReturn, limit.MaxLossLimit, limit.MinProfitLimit, limit.MaxProfitLimit, limit.MinProfitTarget, limit.DailyProfitTarget, limit.CreatedOn, limit.CreatedBy, limit.UpdatedOn, limit.UpdatedBy], token))
            .ConfigureAwait(false);
            if (initializeOnly)
            {
                foreach (var spread in spreads)
                    await db.ExecuteAsync(TradeDbSql.InsertInitialSpreadLimit,
                        [spread.TradeId, spread.TradeType.ToString(), spread.MaxLossLimit, spread.MinProfitLimit, spread.MaxProfitLimit], token).ConfigureAwait(false);
            }
            else
                await TradeDbContextExtensions.TradeFlowProjectSpreadLimitsAsync(db, spreads, token).ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Stores the exact accepted spread limit values without recalculating the whole-trade limit.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task TradeFlowProjectSpreadLimitsAsync(IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(spreads);
            foreach (var spread in spreads)
                await (db.ExecuteAsync(TradeDbSql.HelperStatement25, [spread.TradeId, spread.TradeType.ToString(), spread.MaxLossLimit, spread.MinProfitLimit, spread.MaxProfitLimit], token))
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads every dated position row through ordered pages without a fixed history row cap.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task<QueryPage<StrategyPositionSnapshot>> TradeReadPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? cursor = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (positionId == Guid.Empty || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc || fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("A position, ordered UTC range and page size 1..1000 are required.", nameof(positionId));
            TradeReadPositionCursor? last = null;
            if (cursor is not null)
            {
                try
                {
                    last = JsonSerializer.Deserialize<TradeReadPositionCursor>(cursor);
                }
                catch (JsonException error)
                {
                    throw new ArgumentException("Invalid position history cursor.", nameof(cursor), error);
                }

                if (last is null || last.PositionId != positionId || last.FromUtc != fromUtc || last.ToUtc != toUtc || last.LastAtUtc.Kind != DateTimeKind.Utc || last.LastAtUtc < fromUtc || last.LastAtUtc > toUtc || last.LastSequence < 0)
                    throw new ArgumentException("Position history cursor does not match this query.", nameof(cursor));
            }

            var sql = TradeDbSql.PositionHistory(last is not null);
            object? [] values;
            if (last is null)
            {
                values = [positionId, fromUtc, toUtc, pageSize + 1];
            }
            else
            {
                values = [positionId, fromUtc, toUtc, last.LastAtUtc, last.LastSequence, pageSize + 1];
            }

            var rows = await (db.QueryAsync(sql, values, TradeDbContext.MapToNativePositionHistory, token))
            .ConfigureAwait(false);
            var items = rows.Take(pageSize).Select(x => x.Position).ToArray();
            var next = rows.Count > pageSize ? JsonSerializer.SerializeToUtf8Bytes(new TradeReadPositionCursor(positionId, fromUtc, toUtc, rows[pageSize - 1].At, rows[pageSize - 1].Sequence)) : null;
            return new(items, next);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads the last committed position snapshot.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task<StrategyPositionSnapshot?> TradeReadPositionAsync(StrategyPositionId id, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            var trade = id.Trade;
            var payload = await (db.ScalarAsync(TradeDbSql.HelperStatement27, [trade.PortfolioId, trade.FundId, trade.OrderId, trade.TradeId, id.PositionId], token))
            .ConfigureAwait(false);
            return payload is byte[] bytes ? MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(bytes) : null;
        }
    }


    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads the current committed routes for one contract or the complete recovery snapshot.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public Task<IReadOnlyList<OpenPositionRouteReadModel>> TradeReadRoutesAsync(string? contractId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            return contractId is null ? db.QueryAsync(TradeDbSql.Routes(false), [], TradeDbContext.MapToNativeRoute, token) : db.QueryAsync(TradeDbSql.Routes(true), [contractId], TradeDbContext.MapToNativeRoute, token);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads bounded route recovery pages without Scylla paging-state tokens.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task<QueryPage<OpenPositionRouteReadModel>> TradeReadRoutePageAsync(int size, byte[]? cursor = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            if (size is < 1 or > 500)
                throw new ArgumentOutOfRangeException(nameof(size));
            TradeReadRouteCursor? last = null;
            if (cursor is not null)
            {
                try
                {
                    last = JsonSerializer.Deserialize<TradeReadRouteCursor>(cursor);
                }
                catch (JsonException error)
                {
                    throw new ArgumentException("Invalid route recovery cursor.", nameof(cursor), error);
                }

                if (last is null || last.PositionId == Guid.Empty || last.LegId == Guid.Empty)
                    throw new ArgumentException("Invalid route recovery cursor.", nameof(cursor));
            }

            var sql = TradeDbSql.RoutePage(last is not null);
            object? [] values;
            if (last is null)
            {
                values = [size + 1];
            }
            else
            {
                values = [last.PositionId, last.LegId, size + 1];
            }

            var rows = await (db.QueryAsync(sql, values, TradeDbContext.MapToNativeRoute, token))
            .ConfigureAwait(false);
            var next = rows.Count > size ? JsonSerializer.SerializeToUtf8Bytes(new TradeReadRouteCursor(rows[size - 1].Route.StrategyPositionId, rows[size - 1].Route.TradeLegId)) : null;
            return new(rows.Take(size).ToArray(), next);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads the persisted execution facts without contacting the broker.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task<OrderExecutionDefinition?> TradeReadExecutionAsync(TradeOrderId id, Guid attempt, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            var value = await (db.ScalarAsync(TradeDbSql.HelperStatement31, [id.PortfolioId, id.FundId, id.OrderId, attempt], token))
            .ConfigureAwait(false);
            return value is byte[] bytes ? MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(bytes) : null;
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads the established trade's last accepted evidence revision.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task<EstablishedTradeDefinition?> TradeReadTradeAsync(TradeEntityId id, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            var value = await (db.ScalarAsync(TradeDbSql.HelperStatement32, [id.PortfolioId, id.FundId, id.OrderId, id.TradeId], token))
            .ConfigureAwait(false);
            return value is byte[] bytes ? MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(bytes) : null;
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads persisted whole-trade limits, preserving the existing optional same-type limit override.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public Task<IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel>> TradeReadLimitsAsync(int? tradeId = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            var sql = TradeDbSql.Limits(tradeId.HasValue);
            return db.QueryAsync(sql, tradeId.HasValue ? [tradeId.Value] : [], TradeDbContext.MapToNativeTradeLimit, token);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Reads the saved spread limit rows without requesting live market data.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public Task<IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel>> TradeReadSpreadLimitsAsync(int? tradeId = null, TradeType? type = null, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(db);
            var sql = TradeDbSql.SpreadLimits(tradeId.HasValue, type.HasValue);
            object? [] values = [];
            if (tradeId.HasValue)
            {
                values = [tradeId.Value];
            }

            if (type.HasValue)
            {
                if (!tradeId.HasValue)
                    throw new ArgumentException("A trade identifier is required with a spread type.", nameof(type));
                values = [tradeId.Value, type.Value.ToString()];
            }

            return db.QueryAsync(sql, values, TradeDbContext.MapToNativeSpreadLimit, token);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Stores the exact market assessment and its profile history atomically.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task PipelineProjectAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            ArgumentNullException.ThrowIfNull(db);
            var r = TradeProjectionFacts.Assessment(completed);
            var bytes = MessagePackSerializer.Serialize(completed, cancellationToken: token);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var saved = await (db.ScalarAsync(TradeDbSql.HelperStatement35, [r.WorkflowId.Value, bytes, hash], token))
            .ConfigureAwait(false);
            if (saved is null)
                throw new InvalidDataException("Conflicting accepted market assessment.");
            await (db.ExecuteAsync(TradeDbSql.HelperStatement36, [r.MarketProfileId, r.InstrumentRoot, r.TargetHorizon.ToString(), r.EvaluatedAtUtc, r.WorkflowId.Value, bytes, hash], token))
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Stores the immutable composer invocation and its exact fund/value-date history atomically.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task PipelineProjectAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            ArgumentNullException.ThrowIfNull(db);
            var r = TradeProjectionFacts.Composition(completed);
            var bytes = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
            var saved = await (db.ScalarAsync(TradeDbSql.HelperStatement37, [r.WorkflowId.Value, r.InvocationId, completed.Result.PayloadSha256, r.InputSha256, bytes], token))
            .ConfigureAwait(false);
            if (saved is null)
                throw new InvalidDataException("Conflicting accepted composition invocation.");
            await (db.ExecuteAsync(TradeDbSql.HelperStatement38, [r.DecisionContext.PortfolioId, r.DecisionContext.FundId, r.DecisionContext.ValueDate, r.EvaluatedAtUtc, r.InvocationId, r.WorkflowId.Value, completed.Id, (short)r.TargetHorizon, (short)r.Outcome, r.Reasons[0], r.ResultId, completed.Result.PayloadSha256], token))
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        /// <summary>Stores the complete selector decision and history, without rerunning selection or dispatching an order.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        public async Task PipelineProjectAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            ArgumentNullException.ThrowIfNull(db);
            var r = TradeProjectionFacts.Selection(completed);
            var selected = r.SelectedCandidate;
            var binding = r.DecisionContext.SelectionBinding;
            var bytes = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
            var saved = await (db.ScalarAsync(TradeDbSql.HelperStatement39, [r.WorkflowId.Value, r.InvocationId, 1L, completed.Id, r.PortfolioId, r.FundId, (short)r.DecisionHorizon, (short)2, (short)r.Outcome, r.ProducedAtUtc, r.PrimaryReasonCode, selected?.DeploymentKey.Id, selected?.DeploymentKey.Version, selected?.StrategyKey.Id, selected?.StrategyKey.Version, selected?.StructureKey.Id, selected?.StructureKey.Version, selected?.VariantKey.Id, selected?.VariantKey.Version, TradeSelectionContracts.WireHash(binding.Candidates.Select(x => x.CandidateHash).ToArray()), binding.PayloadSha256, binding.CommonPolicy.Id, binding.CommonPolicy.Version, binding.CommonPolicy.PayloadSha256, r.ResultId, completed.Result.PayloadSha256, MessagePackBinarySerializer.Shared.Serialize(r), bytes], token))
            .ConfigureAwait(false);
            if (saved is null)
                throw new InvalidDataException("Conflicting accepted selection invocation.");
            await (db.ExecuteAsync(TradeDbSql.HelperStatement40, [r.PortfolioId, r.FundId, DateOnly.FromDateTime(r.ProducedAtUtc), r.ProducedAtUtc, r.WorkflowId.Value, r.InvocationId, completed.Id, (short)r.DecisionHorizon, (short)r.Outcome, r.PrimaryReasonCode, r.ResultId, completed.Result.PayloadSha256], token))
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task<OrderCompositionFunctionCompletedEvent?> PipelineReadCompositionAsync(StrategyWorkflowId workflow, Guid invocation, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var rows = await transaction.QueryAsync(TradeDbSql.HelperStatement41, [workflow.Value, invocation], TradeDbContext.MapToCompositionEvidence, token)
            .ConfigureAwait(false);
            if (rows.Count == 0)
                return null;
            var row = rows[0];
            var source = MessagePackBinarySerializer.Shared.Deserialize<OrderCompositionFunctionCompletedEvent>(row.Payload);
            var result = OrderCompositionContracts.ReadResult(source.Result);
            if (result.WorkflowId != workflow || result.InvocationId != invocation || !string.Equals(source.Result.PayloadSha256, row.Result, StringComparison.Ordinal) || !string.Equals(result.InputSha256, row.Input, StringComparison.Ordinal))
                throw new InvalidDataException("Stored composition evidence mismatch.");
            return source;
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task<TradeSelectionFunctionCompletedEvent?> PipelineReadSelectionAsync(StrategyWorkflowId workflow, Guid invocation, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var rows = await transaction.QueryAsync(TradeDbSql.HelperStatement42, [workflow.Value, invocation], TradeDbContext.MapToSelectionEvidence, token)
            .ConfigureAwait(false);
            if (rows.Count == 0)
                return null;
            var row = rows[0];
            var source = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(row.Payload);
            var result = TradeSelectionContracts.ReadResult(source.Result);
            if (result.WorkflowId != workflow || result.InvocationId != invocation || !string.Equals(source.Result.PayloadSha256, row.Result, StringComparison.Ordinal))
                throw new InvalidDataException("Stored selector evidence identity/hash mismatch.");
            return source;
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task<QueryPage<OrderCompositionHistoryRow>> PipelineReadCompositionHistoryAsync(int portfolio, int fund, DateOnly date, int pageSize, byte[]? pagingState, CancellationToken token = default)
        {
            var page = await TradeDbContextExtensions.PipelineReadReadHistoryAsync(transaction, true, portfolio, fund, date, pageSize, pagingState, token)
            .ConfigureAwait(false);
            return new(page.Items.Select(r => new OrderCompositionHistoryRow(portfolio, fund, date, r.AtUtc, r.Workflow, r.Invocation, r.Event, r.Horizon, r.Outcome, r.Reason, r.Result, r.Hash)).ToArray(), page.PagingState);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task<QueryPage<TradeSelectionHistoryRow>> PipelineReadSelectionHistoryAsync(int portfolio, int fund, DateOnly date, int pageSize, byte[]? pagingState, CancellationToken token = default)
        {
            var page = await TradeDbContextExtensions.PipelineReadReadHistoryAsync(transaction, false, portfolio, fund, date, pageSize, pagingState, token)
            .ConfigureAwait(false);
            return new(page.Items.Select(r => new TradeSelectionHistoryRow(portfolio, fund, date, r.AtUtc, r.Workflow, r.Invocation, r.Event, r.Horizon, r.Outcome, r.Reason, r.Result, r.Hash)).ToArray(), page.PagingState);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        internal async Task<QueryPage<PipelineReadHistoryRow>> PipelineReadReadHistoryAsync(bool composition, int portfolio, int fund, DateOnly date, int pageSize, byte[]? pagingState, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(portfolio);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fund);
            if (date == default)
                throw new ArgumentException("A history value date is required.", nameof(date));
            if (pageSize < 1 || pageSize > (composition ? 100 : 200))
                throw new ArgumentOutOfRangeException(nameof(pageSize), "A bounded history page size is required.");
            var scope = string.Create(CultureInfo.InvariantCulture, $"{composition}:{portfolio}:{fund}:{date.DayNumber}");
            PipelineReadCursor? cursor = null;
            if (pagingState is not null)
            {
                if (pagingState.Length > 2048)
                    throw new ArgumentException("Invalid history cursor.", nameof(pagingState));
                try
                {
                    cursor = JsonSerializer.Deserialize<PipelineReadCursor>(pagingState);
                }
                catch (JsonException error)
                {
                    throw new ArgumentException("Invalid history cursor.", nameof(pagingState), error);
                }

                if (cursor is null || !string.Equals(cursor.Scope, scope, StringComparison.Ordinal) || cursor.AtUtc.Kind != DateTimeKind.Utc || cursor.Workflow == Guid.Empty || cursor.Invocation == Guid.Empty)
                    throw new ArgumentException("History cursor does not belong to this scope.", nameof(pagingState));
            }

            var sql = TradeDbSql.PipelineHistory(composition, cursor is not null);
            object? [] values = cursor is null ? [portfolio, fund, date, pageSize + 1] : [portfolio, fund, date, cursor.AtUtc, cursor.Workflow, cursor.Invocation, pageSize + 1];
            var rows = await transaction.QueryAsync(sql, values, TradeDbContext.MapToPipelineHistory, token)
            .ConfigureAwait(false);
            var items = rows.Take(pageSize).ToArray();
            byte[]? next = null;
            if (rows.Count > pageSize)
            {
                var last = items[^1];
                next = JsonSerializer.SerializeToUtf8Bytes(new PipelineReadCursor(scope, last.AtUtc, last.Workflow, last.Invocation));
            }

            return new(items, next);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task RegimeProjectAsync(RegimeDiscoveryPipelineCompletedEvent source, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(source);
            var result = TradeProjectionFacts.Regime(source);
            var payload = MessagePackBinarySerializer.Shared.Serialize(source with { EventId = 0 });
            var accepted = await transaction.ScalarAsync(TradeDbSql.HelperStatement43, [source.WorkflowId.Value, source.CommandId, source.Id, payload], token)
            .ConfigureAwait(false);
            if (accepted is null)
                throw new InvalidDataException("Conflicting accepted Regime Discovery invocation.");
            var bytes = string.Equals(source.Result.ContentType, StrategyStageResultEnvelope.TypedRegimeContentType, StringComparison.Ordinal) ? MessagePackSerializer.Serialize(result, cancellationToken: token) : source.Result.Payload.ToArray();
            await TradeDbContextExtensions.RegimeUpsertAsync(transaction, new RegimeDiscoveryReadModel { WorkflowId = source.WorkflowId, WorkflowEntityId = source.EntityId.Format(), InputWorkflowRevision = source.InputWorkflowRevision, CommandId = source.CommandId, SourceEventId = source.Id, SourceEventSequence = source.EventId, Status = "Completed", ParameterPayloadSha256 = source.ParameterPayloadSha256, SignalSnapshotId = source.SignalSnapshotId, ResultPayload = bytes, ResultPayloadSha256 = StrategyStageResultEnvelope.ComputePayloadSha256(bytes), ReasonsPayload = MessagePackSerializer.Serialize(result.Reasons, cancellationToken: token), SchemaVersion = source.Result.SchemaVersion, TerminalAtUtc = source.CompletedAtUtc, UpdatedAtUtc = source.CompletedAtUtc }, token)
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public Task<int> RegimeUpsertAsync(RegimeDiscoveryReadModel value, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(value);
            return transaction.ExecuteAsync(TradeDbSql.HelperStatement44, [value.WorkflowId.Value, value.WorkflowEntityId, value.InputWorkflowRevision, value.CommandId, value.SourceEventId, value.SourceEventSequence, value.Status, value.ParameterPayloadSha256, value.SignalSnapshotId, value.ResultPayload.ToArray(), value.ResultPayloadSha256, value.FailureCode, value.FailureMessage, value.ReasonsPayload.ToArray(), value.SchemaVersion, value.TerminalAtUtc, value.UpdatedAtUtc], token);
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task<RegimeDiscoveryReadModel?> RegimeReadAsync(StrategyWorkflowId workflowId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            var rows = await transaction.QueryAsync(TradeDbSql.HelperStatement45, [workflowId.Value], TradeDbContext.MapToNativeRegime, token)
            .ConfigureAwait(false);
            return rows.Count == 0 ? null : rows[0];
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        public async Task WorkflowProjectAsync(WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(source);
            var state = TradeProjectionFacts.Workflow(source);
            var payload = MessagePackSerializer.Serialize(state, cancellationToken: token);
            await TradeDbContextExtensions.WorkflowWriteDetailAsync(transaction, source, payload, token)
            .ConfigureAwait(false);
            await TradeDbContextExtensions.WorkflowWriteHistoryAsync(transaction, source, token)
            .ConfigureAwait(false);
            await TradeDbContextExtensions.WorkflowWriteTimelineAsync(transaction, source, token)
            .ConfigureAwait(false);
            await TradeDbContextExtensions.WorkflowWriteActiveAsync(transaction, source, payload, token)
            .ConfigureAwait(false);
            await TradeDbContextExtensions.WorkflowWriteRiskAsync(transaction, source, token)
            .ConfigureAwait(false);
        }
    }

    extension(WorkflowStrategyStateUpdatedEvent source)
    {
        public ActiveIntrinsicTimeStrategyWorkflowReadModel? WorkflowActive() => TradeProjectionFacts.Active(source);
    }

    extension(IEnlistedPostgresTransaction db)
    {
        internal Task<int> WorkflowWriteDetailAsync(WorkflowStrategyStateUpdatedEvent source, byte[] payload, CancellationToken token)
        {
            var state = source.State;
            var entity = state.EntityId;
            var iti = entity.ItiSignalEntityId;
            return TradeDbContextExtensions.WorkflowUpsertAsync(db, "intrinsic_time_strategy_workflow", "workflowId,workflowEntityId,workflowDefinitionId,workflowDefinitionVersion,contractId,timeFrameStartValueDate,timePeriod,triggerEventId,correlationId,status,outcome,currentStage,workflowRevision,lastEventId,stateSchemaVersion,statePayload,stopReasonCode,startedAtUtc,terminalAtUtc,updatedAtUtc", "workflowId", [state.WorkflowId.Value, entity.Format(), entity.WorkflowDefinitionId, state.WorkflowDefinitionVersion, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod.ToString(), state.TriggerEventId, state.CorrelationId, TradeProjectionFacts.Status(state).ToString(), TradeProjectionFacts.Outcome(state).ToString(), state.CurrentStage.ToString(), state.WorkflowRevision, source.EventId, 2, payload, state.StopReasonCode, state.StartedAtUtc, state.TerminalAtUtc, state.UpdatedAtUtc], token, "lastEventId");
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        internal async Task WorkflowWriteHistoryAsync(WorkflowStrategyStateUpdatedEvent source, CancellationToken token)
        {
            var state = source.State;
            var status = TradeProjectionFacts.Status(state).ToString();
            var outcome = TradeProjectionFacts.Outcome(state).ToString();
            await TradeDbContextExtensions.WorkflowUpsertAsync(db, "intrinsic_time_strategy_workflow_by_entity", "workflowEntityId,startedAtUtc,workflowId,status,outcome,currentStage,workflowRevision,terminalAtUtc,stopReasonCode", "workflowEntityId,startedAtUtc,workflowId", [state.EntityId.Format(), state.StartedAtUtc, state.WorkflowId.Value, status, outcome, state.CurrentStage.ToString(), state.WorkflowRevision, state.TerminalAtUtc, state.StopReasonCode], token, "workflowRevision")
            .ConfigureAwait(false);
            await TradeDbContextExtensions.WorkflowUpsertAsync(db, "intrinsic_time_strategy_workflow_by_status_day", "status,startedDate,startedAtUtc,workflowId,workflowEntityId,outcome,currentStage,workflowRevision,terminalAtUtc,stopReasonCode", "status,startedDate,startedAtUtc,workflowId", [status, DateOnly.FromDateTime(state.StartedAtUtc), state.StartedAtUtc, state.WorkflowId.Value, state.EntityId.Format(), outcome, state.CurrentStage.ToString(), state.WorkflowRevision, state.TerminalAtUtc, state.StopReasonCode], token, "workflowRevision")
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        internal async Task WorkflowWriteTimelineAsync(WorkflowStrategyStateUpdatedEvent source, CancellationToken token)
        {
            var state = source.State;
            await TradeDbContextExtensions.WorkflowUpsertAsync(db, "intrinsic_time_strategy_workflow_timeline", "workflowId,eventId,workflowEntityId,workflowRevision,stage,eventName,eventSchemaVersion,eventPayload,occurredAtUtc", "workflowId,eventId", [source.WorkflowId.Value, source.EventId, source.EntityId.Format(), source.WorkflowRevision, state.CurrentStage.ToString(), source.EventName, 2, MessagePackSerializer.Serialize(source, cancellationToken: token), source.UpdatedAtUtc], token)
            .ConfigureAwait(false);
            if (state.Status == WorkflowStrategyMachineStatus.Started && source.PreviousStatus != WorkflowStrategyMachineStatus.Started)
                await TradeDbContextExtensions.WorkflowUpsertAsync(db, "intrinsic_time_strategy_workflow_start_attempt", "workflowEntityId,requestedAtUtc,requestedWorkflowId,decision,activeWorkflowId,startCommandId,triggerEventId,activeStage,reasonCode,sourceEventId", "workflowEntityId,requestedAtUtc,requestedWorkflowId", [source.EntityId.Format(), state.StartedAtUtc, source.WorkflowId.Value, StrategyWorkflowStartDecision.Accepted.ToString(), source.WorkflowId.Value, source.CommandId, state.TriggerEventId, state.CurrentStage.ToString(), string.Empty, source.EventId], token)
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        internal async Task WorkflowWriteActiveAsync(WorkflowStrategyStateUpdatedEvent source, byte[] payload, CancellationToken token)
        {
            var state = source.State;
            if (state.Status != WorkflowStrategyMachineStatus.Started)
            {
                await db.ExecuteAsync(TradeDbSql.HelperStatement46, [source.EntityId.Format(), source.EventId], token)
            .ConfigureAwait(false);
                return;
            }

            var iti = source.EntityId.ItiSignalEntityId;
            await TradeDbContextExtensions.WorkflowUpsertAsync(db, "intrinsic_time_strategy_workflow_active", "workflowEntityId,workflowId,contractId,timeFrameStartValueDate,timePeriod,currentStage,workflowRevision,lastEventId,stateSchemaVersion,statePayload,startedAtUtc,updatedAtUtc", "workflowEntityId", [source.EntityId.Format(), source.WorkflowId.Value, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod.ToString(), state.CurrentStage.ToString(), state.WorkflowRevision, source.EventId, 2, payload, state.StartedAtUtc, state.UpdatedAtUtc], token, "lastEventId")
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        internal async Task WorkflowWriteRiskAsync(WorkflowStrategyStateUpdatedEvent source, CancellationToken token)
        {
            var row = RiskHistoryIdentity.Row(source);
            if (row is null)
                return;
            var canonical = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(MessagePackBinarySerializer.Shared.Serialize(source with { EventId = 0 }));
            var hash = RiskContracts.Hash(canonical);
            var payload = MessagePackBinarySerializer.Shared.Serialize(canonical);
            var stored = await db.ScalarAsync(TradeDbSql.HelperStatement47, [row.WorkflowId, row.InvocationId, row.Revision, payload, hash], token)
            .ConfigureAwait(false);
            if (stored is null)
                throw new InvalidDataException("Accepted risk history conflicts with different source facts.");
            await TradeDbContextExtensions.WorkflowUpsertAsync(db, "risk_management_history", "portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload", "portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id", [row.PortfolioId, row.FundId, row.ValueDate, row.EvaluatedAtUtc, row.InvocationId, row.Revision, MessagePackBinarySerializer.Shared.Serialize(row)], token, "revision")
            .ConfigureAwait(false);
        }
    }

    extension(IEnlistedPostgresTransaction db)
    {
        internal Task<int> WorkflowUpsertAsync(string table, string columnList, string keyList, object? [] values, CancellationToken token, string? revision = null)
        {
            return db.ExecuteAsync(TradeDbSql.WorkflowUpsert(table, columnList, keyList, values.Length, revision), values, token);
        }
    }


    /// <summary>Reads bounded PostgreSQL Risk pages ordered by evaluation time and invocation identity.</summary>
    public static async Task<QueryPage<RiskHistoryRow>> RiskReadHistoryAsync(IEnlistedPostgresTransaction db,int portfolio,int fund,DateOnly date,int size,byte[]? pagingState,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        if(portfolio<=0||fund<=0||date==default||size<1||size>100)throw new ArgumentException("Invalid Risk history scope.");
        var scope=string.Create(CultureInfo.InvariantCulture,$"risk:{portfolio}:{fund}:{date.DayNumber}:{size}");
        RiskReadCursor? cursor=null;
        if(pagingState is not null){if(pagingState.Length>2048)throw new ArgumentException("Invalid Risk history cursor.");try{cursor=JsonSerializer.Deserialize<RiskReadCursor>(pagingState);}catch(JsonException e){throw new ArgumentException("Invalid Risk history cursor.",nameof(pagingState),e);}if(cursor is null||cursor.Scope!=scope||cursor.AtUtc.Kind!=DateTimeKind.Utc||cursor.Invocation==Guid.Empty)throw new ArgumentException("Risk history cursor does not belong to this scope.");}
        object?[] values=cursor is null?[portfolio,fund,date,size+1]:[portfolio,fund,date,cursor.AtUtc,cursor.Invocation,size+1];
        var rows=await db.QueryAsync(TradeDbSql.RiskHistory(cursor is not null),values,r=>(Item:MessagePackBinarySerializer.Shared.Deserialize<RiskHistoryRow>(r.GetFieldValue<byte[]>(0)),AtUtc:r.GetDateTime(1),Invocation:r.GetGuid(2)),token).ConfigureAwait(false);
        var items=rows.Take(size).ToArray();
        var next=rows.Count>size?JsonSerializer.SerializeToUtf8Bytes(new RiskReadCursor(scope,items[^1].AtUtc,items[^1].Invocation)):null;
        return new(items.Select(r=>r.Item).ToArray(),next);
    }

}

internal sealed record TradeReadPositionCursor(Guid PositionId, DateTime FromUtc, DateTime ToUtc, DateTime LastAtUtc, long LastSequence);
internal sealed record TradeReadRouteCursor(Guid PositionId, Guid LegId);
internal sealed record PipelineReadCursor(string Scope, DateTime AtUtc, Guid Workflow, Guid Invocation);
internal sealed record PipelineReadHistoryRow(DateTime AtUtc, Guid Workflow, Guid Invocation, Guid Event, short Horizon, byte Outcome, string Reason, Guid Result, string Hash);
internal sealed record TradePlanForwardLossRatioRow(double ForwardLossRatio, long SequenceId);

internal sealed record RiskReadCursor(string Scope,DateTime AtUtc,Guid Invocation);
