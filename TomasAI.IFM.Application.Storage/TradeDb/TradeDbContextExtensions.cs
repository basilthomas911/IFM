using TomasAI.IFM.Domain.MarketData.Shared;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
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
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Provides internal persistence helpers for <see cref="TradeDbContext"/>.</summary>
internal static class TradeDbContextExtensions
{
    extension(TradeDbContext context)
    {
        /// <summary>Requires a bounded route query page between one and five hundred rows.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Validation belongs to the DbContext binding receiver contract.")]
        internal void ValidateRoutePageSize(int pageSize)
        {
            if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            List<object> queuedCommands = [db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegById)}", TradeDbCql.DeleteOptionLegById)
                .SetParameters(new DeleteOptionLegById(e.OrderId, e.TradeId, e.ContractId))
                .QueueCommand(), db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
                .SetParameters(new InsertOptionLeg(e.OrderId, e.TradeId, e.ContractId, e.Quantity, e.StrikePrice, e.OptionLegType.ToStringFast(), e.OptionLegAction.ToStringFast(), DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand()];
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionLegsAsync operation.</summary>
        internal async Task<long> InsertOptionLegsAsyncCoreBinding(IEnumerable<OptionTradeLegReadModel> optionLegs)
        {
            ArgumentNullException.ThrowIfNull(optionLegs);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            List<object> queuedCommands = [db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegDataById)}", TradeDbCql.DeleteOptionLegDataById)
                .SetParameters(new DeleteOptionLegDataById(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.OptionLegId))
                .QueueCommand(), db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                .SetParameters(new InsertOptionLegData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.OptionLegId, e.BidPrice, e.AskPrice, e.ImpliedVolatility, e.Delta, e.Gamma, e.Theta, e.Vega, e.Rho, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand()];
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionLegDataAsync operation.</summary>
        internal async Task<long> InsertOptionLegDataAsyncCoreBinding(IEnumerable<OptionTradeLegDataReadModel> optionLegData)
        {
            ArgumentNullException.ThrowIfNull(optionLegData);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            var dbWriter = db as ITradeDbWriteContext;
            await (dbWriter!.DeleteOptionTradeAsync(e.OrderId, e.TradeId))
                .ConfigureAwait(false);
            queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTrade)}", TradeDbCql.InsertOptionTrade)
                .SetParameters(new InsertOptionTrade(e.TradeId, e.OrderId, e.TradeDate, e.MaturityDate, e.TradeType.ToStringFast(), e.TradeState.ToStringFast(), e.TradeStrategy ?? string.Empty, e.TradeAction.ToStringFast(), e.UnderlyingContractId, e.UnderlyingAssetType.ToStringFast(), e.IsPrimaryTrade, e.IsHedgeTrade, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand());
            // save each option leg info...
            if (e.OptionLegs is not null)
            {
                foreach (var ol in e.OptionLegs)
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLeg)}", TradeDbCql.InsertOptionLeg)
                .SetParameters(new InsertOptionLeg(e.OrderId, ol.TradeId, ol.ContractId, ol.Quantity, ol.StrikePrice, ol.OptionLegType.ToStringFast(), ol.OptionLegAction.ToStringFast(), DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand());
            }
        
            // save trade positions...
            if (e.TradePositions is not null)
            {
                foreach (var o in e.TradePositions)
                {
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
                .SetParameters(new InsertTradePosition(e.OrderId, o.TradeId, o.TradeType.ToStringFast(), o.ValueDate, o.DaysToExpiry, o.TradeStatus.ToStringFast(), o.Commission, o.DeltaHedge, o.NetSpread, o.TradeValue, o.TradePnl, o.AssetPrice, o.OTMProbability, o.ForwardPrice, o.ForwardLossRatio, o.LossProbability, o.RiskFreeRate, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand());
                    // save option leg data within each trade position...
                    if (o.OptionLegData is not null)
                    {
                        foreach (var old in o.OptionLegData)
                            queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                .SetParameters(new InsertOptionLegData(e.OrderId, old.TradeId, old.TradeType.ToStringFast(), old.ValueDate, old.DaysToExpiry, old.TradeStatus.ToStringFast(), old.OptionLegId, old.BidPrice, old.AskPrice, old.ImpliedVolatility, old.Delta, old.Gamma, old.Theta, old.Vega, old.Rho, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand());
                    }
                }
            }
        
            // save trade limit...
            if (e.TradeLimit is not null)
            {
                queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
                .SetParameters(new InsertTradeLimit(e.TradeLimit.TradeId, e.TradeLimit.TradeType.ToStringFast(), e.TradeLimit.RiskMargin, e.TradeLimit.MaxProfit, e.TradeLimit.MaxLoss, e.TradeLimit.MaxReturn, e.TradeLimit.MaxLossLimit, e.TradeLimit.MinProfitLimit, e.TradeLimit.MaxProfitLimit, e.TradeLimit.MinProfitTarget, e.TradeLimit.DailyProfitTarget, DateTime.Now, e.CreatedBy, DateTime.Now, e.CreatedBy))
                .QueueCommand());
                // save trade type limits...
                queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(new InsertTradeTypeLimit(e.TradeLimit.TradeId, e.TradeLimit.TradeType.ToStringFast(), e.TradeLimit.MaxLossLimit, e.TradeLimit.MinProfitLimit, e.TradeLimit.MaxProfitLimit))
                .QueueCommand());
            }
        
            if (e.TradeTypeLimits is not null)
            {
                foreach (var ttl in e.TradeTypeLimits)
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(new InsertTradeTypeLimit(ttl.TradeId, ttl.TradeType.ToStringFast(), ttl.MaxLossLimit, ttl.MinProfitLimit, ttl.MaxProfitLimit))
                .QueueCommand());
            }
        
            // save any trade fills...
            if (e.TradeFills is not null)
            {
                foreach (var tf in e.TradeFills)
                {
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
                .SetParameters(new InsertTradeFill(tf.OrderId, tf.TradeId, tf.FillDate, tf.FillQuantity, tf.CreatedOn, tf.CreatedBy))
                .QueueCommand());
                    if (tf.TradeFillData is not null)
                        foreach (var tfd in tf.TradeFillData)
                            queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFillData)}", TradeDbCql.InsertTradeFillData)
                .SetParameters(new InsertTradeFillData(tfd.OrderId, tfd.TradeId, tfd.ContractId, tfd.FillDate, tfd.BidPrice, tfd.AskPrice, tfd.Commission, tfd.OptionLegAction.ToStringFast(), tfd.CreatedOn, tfd.CreatedBy))
                .QueueCommand());
                }
            }
        
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionTradesAsync operation.</summary>
        internal async Task<long> InsertOptionTradesAsyncCoreBinding(IEnumerable<OptionTradeReadModel> optionTrades)
        {
            ArgumentNullException.ThrowIfNull(optionTrades);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTrade)}", TradeDbCql.InsertOptionTrade)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            List<object> queuedCommands = [db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
                .SetParameters(new DeleteTradePositionLowerCase(e.OrderId, e.TradeId))
                .QueueCommand(), db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegData)}", TradeDbCql.DeleteOptionLegData)
                .SetParameters(new DeleteOptionLegDataLowerCase(e.OrderId, e.TradeId))
                .QueueCommand()];
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
            queuedCommands.Clear();
            queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
                .SetParameters(new InsertTradePosition(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.Commission, e.DeltaHedge, e.NetSpread, e.TradeValue, e.TradePnl, e.AssetPrice, e.OTMProbability, e.ForwardPrice, e.ForwardLossRatio, e.LossProbability, e.RiskFreeRate, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy))
                .QueueCommand());
            // save option leg data within trade position...
            if (e.OptionLegData is not null && e.OptionLegData.Length > 0)
                queuedCommands.AddRange(e.OptionLegData.Select(old => db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                .SetParameters(new InsertOptionLegData(e.OrderId, old.TradeId, old.TradeType.ToStringFast(), old.ValueDate, old.DaysToExpiry, old.TradeStatus.ToStringFast(), old.OptionLegId, old.BidPrice, old.AskPrice, old.ImpliedVolatility, old.Delta, old.Gamma, old.Theta, old.Vega, old.Rho, old.CreatedOn, old.CreatedBy, old.UpdatedOn, old.UpdatedBy))
                .QueueCommand()));
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            foreach (var e in tradePositions)
            {
                queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePosition)}", TradeDbCql.DeleteTradePosition)
                .SetParameters(new DeleteTradePositionLowerCase(e.OrderId, e.TradeId))
                .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteOptionLegData)}", TradeDbCql.DeleteOptionLegData)
                .SetParameters(new DeleteOptionLegDataLowerCase(e.OrderId, e.TradeId))
                .QueueCommand());
                queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
                .SetParameters(new InsertTradePosition(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.DaysToExpiry, e.TradeStatus.ToStringFast(), e.Commission, e.DeltaHedge, e.NetSpread, e.TradeValue, e.TradePnl, e.AssetPrice, e.OTMProbability, e.ForwardPrice, e.ForwardLossRatio, e.LossProbability, e.RiskFreeRate, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy))
                .QueueCommand());
                // save option leg data within trade position...
                if (e.OptionLegData is not null)
                {
                    foreach (var old in e.OptionLegData)
                        queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionLegData)}", TradeDbCql.InsertOptionLegData)
                .SetParameters(new InsertOptionLegData(e.OrderId, old.TradeId, old.TradeType.ToStringFast(), old.ValueDate, old.DaysToExpiry, old.TradeStatus.ToStringFast(), old.OptionLegId, old.BidPrice, old.AskPrice, old.ImpliedVolatility, old.Delta, old.Gamma, old.Theta, old.Vega, old.Rho, old.CreatedOn, old.CreatedBy, old.UpdatedOn, old.UpdatedBy))
                .QueueCommand());
                }
            }
        
            await (db.ExecuteQueuedCommandsAsync(queuedCommands))
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradePositionsAsync operation.</summary>
        internal async Task<long> InsertTradePositionsAsyncCoreBinding(IEnumerable<TradePositionReadModel> tradePositions)
        {
            ArgumentNullException.ThrowIfNull(tradePositions);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePosition)}", TradeDbCql.InsertTradePosition)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeFillAsync operation.</summary>
        internal async Task InsertTradeFillAsyncCoreBinding(ICollection<TradeFillReadModel> tradeFills)
        {
            ArgumentNullException.ThrowIfNull(tradeFills);
            // save any trade fills...
            if (tradeFills.Count > 0)
            {
                var db = context._dbFactory.TradeDb;
                foreach (var tf in tradeFills)
                {
                    var queuedCommands = new List<object>();
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFill)}", TradeDbCql.DeleteTradeFill)
                .SetParameters(new DeleteTradeFill(tf.OrderId, tf.TradeId))
                .QueueCommand());
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradeFillData)}", TradeDbCql.DeleteTradeFillData)
                .SetParameters(new DeleteTradeFillData(tf.OrderId, tf.TradeId))
                .QueueCommand());
                    queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
                .SetParameters(new InsertTradeFill(tf.OrderId, tf.TradeId, tf.FillDate, tf.FillQuantity, tf.CreatedOn, tf.CreatedBy))
                .QueueCommand());
                    if (tf.TradeFillData?.Length > 0)
                        foreach (var tfd in tf.TradeFillData!)
                            queuedCommands.Add(db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFillData)}", TradeDbCql.InsertTradeFillData)
                .SetParameters(new InsertTradeFillData(tfd.OrderId, tfd.TradeId, tfd.ContractId, tfd.FillDate, tfd.BidPrice, tfd.AskPrice, tfd.Commission, tfd.OptionLegAction.ToStringFast(), tfd.CreatedOn, tfd.CreatedBy))
                .QueueCommand());
                    await (db.ExecuteQueuedCommandsAsync(queuedCommands, true))
                .ConfigureAwait(false);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeFillsAsync operation.</summary>
        internal async Task<long> InsertTradeFillsAsyncCoreBinding(IEnumerable<TradeFillReadModel> tradeFills)
        {
            ArgumentNullException.ThrowIfNull(tradeFills);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeFill)}", TradeDbCql.InsertTradeFill)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadData)}", TradeDbCql.InsertOptionTradeSpreadData)
                .SetParameters(new InsertOptionTradeSpreadData(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), sequenceId, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread, e.CreatedOn, e.CreatedBy))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            for (var index = 0; index < optionTradeSpreadsData.Count; index += 1000)
            {
                batchData.Clear();
                foreach (var e in optionTradeSpreadsData.Skip(index).Take(1000))
                {
                    var id = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId))
                .ConfigureAwait(false);
                    batchData.Add((id, e));
                }
        
                await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadData)}", TradeDbCql.InsertOptionTradeSpreadData)
                .SetParameters(batchData.Select(e => new InsertOptionTradeSpreadData(e.Data.OrderId, e.Data.TradeId, e.Data.ValueDate, e.Data.TradeType.ToStringFast(), e.SequenceId, e.Data.LossLimit, e.Data.WinLimit, e.Data.ForwardSpread, e.Data.NetSpread, e.Data.CreatedOn, e.Data.CreatedBy)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            var parameters = new List<InsertOptionTradeSpreadData>();
            foreach (var e in optionTradeSpreadsData)
            {
                var sequenceId = await (context._sequenceIdGenerator.GetSequenceIdAsync(SequenceName.OptionTradeSpreadData_SequenceId))
                .ConfigureAwait(false);
                parameters.Add(new InsertOptionTradeSpreadData(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), sequenceId, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread, e.CreatedOn, e.CreatedBy));
                rowCount++;
            }
        
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadData)}", TradeDbCql.InsertOptionTradeSpreadData)
                .SetParameters(parameters)
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            return rowCount;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadBarData)}", TradeDbCql.InsertOptionTradeSpreadBarData)
                .SetParameters(new InsertOptionTradeSpreadBarData(e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.ValueDate, e.BarDate, e.LossLimit, e.WinLimit, e.ForwardSpread, e.NetSpread))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertOptionTradeSpreadBarDataAsync operation.</summary>
        internal async Task<long> InsertOptionTradeSpreadBarDataAsyncCoreBinding(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData)
        {
            ArgumentNullException.ThrowIfNull(optionTradeSpreadBarsData);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertOptionTradeSpreadBarData)}", TradeDbCql.InsertOptionTradeSpreadBarData)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLiveFeed)}", TradeDbCql.InsertTradeLiveFeed)
                .SetParameters(new InsertTradeLiveFeed(e.OrderId, e.TradeId, e.LiveFeed))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            var dbReader = db as ITradeDbReadContext;
            var existingTradePlanForwardLossLimit = await (dbReader!.GetTradePlanForwardLossLimitAsync(e.OrderId, e.TradeId, e.ValueDate, e.TradeType))
                .ConfigureAwait(false);
            if (existingTradePlanForwardLossLimit is not null)
            {
                await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePlanForwardLossLimit)}", TradeDbCql.DeleteTradePlanForwardLossLimit)
                .SetParameters(new DeleteTradePlanForwardLossLimit(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast()))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            }
        
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossLimit)}", TradeDbCql.InsertTradePlanForwardLossLimit)
                .SetParameters(new InsertTradePlanForwardLossLimit(e.OrderId, e.TradeId, e.ValueDate, e.TradeType.ToStringFast(), e.LimitType.ToStringFast()))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlacementSignal)}", TradeDbCql.InsertTradePlacementSignal)
                .SetParameters(new InsertTradePlacementSignal(sequenceId, e.ContractId, e.ValueDate, e.TradePlacementSignal.ToStringFast(), e.TradePrice, e.CreatedOn, e.CreatedBy))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            return sequenceId;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpdateTradeLiveFeedAsync operation.</summary>
        internal async Task UpdateTradeLiveFeedAsyncCoreBinding(TradeLiveFeedReadModel tradeLiveFeed)
        {
            ArgumentNullException.ThrowIfNull(tradeLiveFeed);
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateTradeLiveFeed)}", TradeDbCql.UpdateTradeLiveFeed)
                .SetParameters(new UpdateTradeLiveFeed(tradeLiveFeed.OrderId, tradeLiveFeed.TradeId, tradeLiveFeed.LiveFeed))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpdateOptionLegData)}", TradeDbCql.UpdateOptionLegData)
                .SetParameters(new UpdateOptionLegData(e.OrderId, e.TradeId, e.ValueDate, e.OptionLegId, e.BidPrice, e.AskPrice, e.ImpliedVolatility, e.Delta, e.Gamma, e.Theta, e.Vega, e.Rho, e.UpdatedOn, e.UpdatedBy))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (context.tradeFlowWriter.ProjectMonitoringLimitsAsync(db, e, [], ct))
                .ConfigureAwait(false);
                    return true;
                }))
                .ConfigureAwait(false);
                return;
            }
        
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
                .SetParameters(new InsertTradeLimit(e.TradeId, e.TradeType.ToStringFast(), e.RiskMargin, e.MaxProfit, e.MaxLoss, e.MaxReturn, e.MaxLossLimit, e.MinProfitLimit, e.MaxProfitLimit, e.MinProfitTarget, e.DailyProfitTarget, e.CreatedOn, e.CreatedBy, e.UpdatedOn, e.UpdatedBy))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeLimitsAsync operation.</summary>
        internal async Task<long> InsertTradeLimitsAsyncCoreBinding(IEnumerable<TradeLimitReadModel> tradeLimits)
        {
            ArgumentNullException.ThrowIfNull(tradeLimits);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeLimit)}", TradeDbCql.InsertTradeLimit)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (context.tradeFlowWriter.ProjectSpreadLimitsAsync(db, [e], ct))
                .ConfigureAwait(false);
                    return true;
                }))
                .ConfigureAwait(false);
                return;
            }
        
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
                .SetParameters(new InsertTradeTypeLimit(e.TradeId, e.TradeType.ToStringFast(), e.MaxLossLimit, e.MinProfitLimit, e.MaxProfitLimit))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertTradeTypeLimitsAsync operation.</summary>
        internal async Task<long> InsertTradeTypeLimitsAsyncCoreBinding(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits)
        {
            ArgumentNullException.ThrowIfNull(tradeTypeLimits);
            var rowCount = 0L;
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradeTypeLimit)}", TradeDbCql.InsertTradeTypeLimit)
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlan)}", TradeDbCql.InsertTradePlan)
                .SetParameters(new InsertTradePlan(sequenceId, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.ValueDate, e.MaturityDate, e.ActionDate, e.ActionType.ToStringFast(), e.ActionSubType.ToStringFast(), e.ActionState.ToStringFast(), e.ActionReason, e.TradePnl, e.ForwardLossRatio, e.LossProbability, e.MScore, e.MaxProfit, e.MaxLoss, e.MinProfitTarget, e.DailyProfitTarget, e.AssetPrice, e.AssetStdDev, e.AssetMean, e.AssetPriceChange, e.MarketTrend.ToStringFast(), e.MarketVolatility.ToStringFast(), e.MarketDirection.ToStringFast(), e.VixVolatility.ToStringFast(), e.TradeRisk.ToStringFast(), e.FiftyDayMA, e.FiveDayXMA, e.PutOTMProbability, e.CallOTMProbability, e.ShortPutGamma, e.ShortCallGamma, e.GammaRisk.ToStringFast(), e.NetPrice, e.ForwardPrice, e.ForwardDelta, e.StopLossLimit, e.TrendType.ToStringFast(), e.TrendStrength.ToStringFast(), e.RSI, e.RSISlope, e.TDI.ToStringFast(), e.TDIStrength.ToStringFast(), e.CreatedOn, e.CreatedBy))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
                .SetParameters(new InsertTradePlanForwardLossRatio(1, e.ValueDate, e.ForwardLossRatio, sequenceId))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var db = context._dbFactory.TradeDb;
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
        
                await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlan)}", TradeDbCql.InsertTradePlan)
                .SetParameters(tradePlanPage.Select(e => new InsertTradePlan(e.Id, e.OrderId, e.TradeId, e.TradeType.ToStringFast(), e.TradeDate, e.ValueDate, e.MaturityDate, e.ActionDate, e.ActionType.ToStringFast(), e.ActionSubType.ToStringFast(), e.ActionState.ToStringFast(), e.ActionReason, e.TradePnl, e.ForwardLossRatio, e.LossProbability, e.MScore, e.MaxProfit, e.MaxLoss, e.MinProfitTarget, e.DailyProfitTarget, e.AssetPrice, e.AssetStdDev, e.AssetMean, e.AssetPriceChange, e.MarketTrend.ToStringFast(), e.MarketVolatility.ToStringFast(), e.MarketDirection.ToStringFast(), e.VixVolatility.ToStringFast(), e.TradeRisk.ToStringFast(), e.FiftyDayMA, e.FiveDayXMA, e.PutOTMProbability, e.CallOTMProbability, e.ShortPutGamma, e.ShortCallGamma, e.GammaRisk.ToStringFast(), e.NetPrice, e.ForwardPrice, e.ForwardDelta, e.StopLossLimit, e.TrendType.ToStringFast(), e.TrendStrength.ToStringFast(), e.RSI, e.RSISlope, e.TDI.ToStringFast(), e.TDIStrength.ToStringFast(), e.CreatedOn, e.CreatedBy)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
                await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertTradePlanForwardLossRatio)}", TradeDbCql.InsertTradePlanForwardLossRatio)
                .SetParameters(tradePlanPage.Select(e => new InsertTradePlanForwardLossRatio(1, e.ValueDate, e.ForwardLossRatio, e.Id)))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePositionState)}", TradeDbCql.DeleteTradePositionState)
                .SetParameters(new DeleteTradePositionState(e.OrderId, e.TradeId))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the DeleteTradePlanForwardLossLimitAsync operation.</summary>
        internal async Task DeleteTradePlanForwardLossLimitAsyncCoreBinding(TradePlanForwardLossLimitEntityId id)
        {
            ArgumentNullException.ThrowIfNull(id);
            await (context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.DeleteTradePlanForwardLossLimit)}", TradeDbCql.DeleteTradePlanForwardLossLimit)
                .SetParameters(new DeleteTradePlanForwardLossLimit(id.OrderId, id.TradeId, id.ValueDate, id.TradeType.ToStringFast()))
                .ExecuteCommandAsync())
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertIntrinsicTimeStrategyWorkflowAsync operation.</summary>
        internal async Task UpsertIntrinsicTimeStrategyWorkflowAsyncCoreBinding(IntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflow)
                .SetParameters(new UpsertIntrinsicTimeStrategyWorkflow(workflow.WorkflowId.Value, workflow.WorkflowEntityId, workflow.WorkflowDefinitionId, workflow.WorkflowDefinitionVersion, workflow.ContractId, workflow.TimeFrameStartValueDate, workflow.TimePeriod.ToString(), workflow.TriggerEventId, workflow.CorrelationId, workflow.Status.ToString(), workflow.Outcome.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.LastEventId, workflow.StateSchemaVersion, workflow.StatePayload.ToArray(), workflow.StopReasonCode, workflow.StartedAtUtc, workflow.TerminalAtUtc, workflow.UpdatedAtUtc))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertActiveIntrinsicTimeStrategyWorkflowAsync operation.</summary>
        internal async Task UpsertActiveIntrinsicTimeStrategyWorkflowAsyncCoreBinding(ActiveIntrinsicTimeStrategyWorkflowReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertActiveIntrinsicTimeStrategyWorkflow)}", TradeDbCql.UpsertActiveIntrinsicTimeStrategyWorkflow)
                .SetParameters(new UpsertActiveIntrinsicTimeStrategyWorkflow(workflow.WorkflowEntityId, workflow.WorkflowId.Value, workflow.ContractId, workflow.TimeFrameStartValueDate, workflow.TimePeriod.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.LastEventId, workflow.StateSchemaVersion, workflow.StatePayload.ToArray(), workflow.StartedAtUtc, workflow.UpdatedAtUtc))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertIntrinsicTimeStrategyWorkflowStartAttemptAsync operation.</summary>
        internal async Task InsertIntrinsicTimeStrategyWorkflowStartAttemptAsyncCoreBinding(IntrinsicTimeStrategyWorkflowStartAttemptReadModel attempt, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(attempt);
            attempt.RequireNotNull(nameof(attempt));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)}", TradeDbCql.InsertIntrinsicTimeStrategyWorkflowStartAttempt)
                .SetParameters(new InsertIntrinsicTimeStrategyWorkflowStartAttempt(attempt.WorkflowEntityId, attempt.RequestedAtUtc, attempt.RequestedWorkflowId.Value, attempt.Decision.ToString(), attempt.ActiveWorkflowId?.Value, attempt.StartCommandId, attempt.TriggerEventId, attempt.ActiveStage.ToString(), attempt.ReasonCode, attempt.SourceEventId))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the InsertIntrinsicTimeStrategyWorkflowTimelineAsync operation.</summary>
        internal async Task InsertIntrinsicTimeStrategyWorkflowTimelineAsyncCoreBinding(IntrinsicTimeStrategyWorkflowTimelineReadModel timelineEvent, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(timelineEvent);
            timelineEvent.RequireNotNull(nameof(timelineEvent));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.InsertIntrinsicTimeStrategyWorkflowTimeline)}", TradeDbCql.InsertIntrinsicTimeStrategyWorkflowTimeline)
                .SetParameters(new InsertIntrinsicTimeStrategyWorkflowTimeline(timelineEvent.WorkflowId.Value, timelineEvent.EventId, timelineEvent.WorkflowEntityId, timelineEvent.WorkflowRevision, timelineEvent.Stage.ToString(), timelineEvent.EventName, timelineEvent.EventSchemaVersion, timelineEvent.EventPayload.ToArray(), timelineEvent.OccurredAtUtc))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertIntrinsicTimeStrategyWorkflowByEntityAsync operation.</summary>
        internal async Task UpsertIntrinsicTimeStrategyWorkflowByEntityAsyncCoreBinding(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByEntity)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByEntity)
                .SetParameters(TradeDbContext.MapToByEntityParameters(workflow))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsync operation.</summary>
        internal async Task UpsertIntrinsicTimeStrategyWorkflowByStatusDayAsyncCoreBinding(IntrinsicTimeStrategyWorkflowHistoryReadModel workflow, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            workflow.RequireNotNull(nameof(workflow));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)}", TradeDbCql.UpsertIntrinsicTimeStrategyWorkflowByStatusDay)
                .SetParameters(new UpsertIntrinsicTimeStrategyWorkflowByStatusDay(workflow.Status.ToString(), DateOnly.FromDateTime(workflow.StartedAtUtc), workflow.StartedAtUtc, workflow.WorkflowId.Value, workflow.WorkflowEntityId, workflow.Outcome.ToString(), workflow.CurrentStage.ToString(), workflow.WorkflowRevision, workflow.TerminalAtUtc, workflow.StopReasonCode))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertMarketConditionAsync operation.</summary>
        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        internal async Task UpsertMarketConditionAsyncCoreBinding(MarketConditionReadModel result, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.RequireNotNull(nameof(result));
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertMarketCondition)}", TradeDbCql.UpsertMarketCondition)
                .SetParameters(new UpsertMarketCondition(result.WorkflowId.Value, result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.FundId, result.InstrumentRoot, result.TargetHorizon.ToString(), result.ParameterSetId, result.ParameterSetVersion, result.ParameterPayloadSha256, result.SnapshotId, result.SnapshotSha256, result.Tradeability.ToString(), result.ConditionType.ToString(), result.Direction.ToString(), result.Phase.ToString(), result.Strength, result.Confidence, result.PrimaryReasonCode, result.ResultPayload.ToArray(), result.ResultPayloadSha256, result.EvaluatedAtUtc, result.ValidUntilUtc, result.MarketDataAsOfUtc, result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(), result.LiquidityQuality.ToString(), result.DataQuality.ToString(), result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(), result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(), result.ReasonsPayload.ToArray(), result.SummaryText))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertMarketConditionByFund)}", TradeDbCql.UpsertMarketConditionByFund)
                .SetParameters(new UpsertMarketConditionByFund(result.FundId, result.InstrumentRoot, result.TargetHorizon.ToString(), result.EvaluatedAtUtc, result.WorkflowId.Value, result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.ParameterSetId, result.ParameterSetVersion, result.ParameterPayloadSha256, result.SnapshotId, result.SnapshotSha256, result.Tradeability.ToString(), result.ConditionType.ToString(), result.Direction.ToString(), result.Phase.ToString(), result.Strength, result.Confidence, result.PrimaryReasonCode, result.ResultPayload.ToArray(), result.ResultPayloadSha256, result.ValidUntilUtc, result.MarketDataAsOfUtc, result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(), result.LiquidityQuality.ToString(), result.DataQuality.ToString(), result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(), result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(), result.ReasonsPayload.ToArray(), result.SummaryText))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertMarketConditionAssessmentAsync operation.</summary>
        internal async Task UpsertMarketConditionAssessmentAsyncCoreBinding(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (new PostgresPipelineResultStore().ProjectAsync(db, completed, ct))
                .ConfigureAwait(false);
                    return true;
                }, cancellationToken))
                .ConfigureAwait(false);
                return;
            }
        
            var r = MarketConditionAssessmentContracts.ReadResult(completed.Result);
            completed.RequireAssessmentProjectionIdentity(r);
            var bytes = MessagePackSerializer.Serialize(completed, cancellationToken: cancellationToken);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            await context._dbFactory.TradeDb.Use("AssessmentProjection.Exact", "INSERT INTO market_condition_assessment (workflow_id,payload,payload_sha256) VALUES (?,?,?);")
                .SetParameters(new AssessmentValues([r.WorkflowId.Value, bytes, hash]))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            await context._dbFactory.TradeDb.Use("AssessmentProjection.History", "INSERT INTO market_condition_assessment_by_profile (market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id,payload,payload_sha256) VALUES (?,?,?,?,?,?,?);")
                .SetParameters(new AssessmentValues([r.MarketProfileId, r.InstrumentRoot, r.TargetHorizon.ToString(), r.EvaluatedAtUtc, r.WorkflowId.Value, bytes, hash]))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetMarketConditionAssessmentHistoryAsync operation.</summary>
        internal Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsyncCoreBinding(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            profile.RequireAssessmentHistoryScope(root, horizon, beforeUtc, pageSize);
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                return context.NativeTransaction().ExecuteOnceAsync<ICollection<MarketConditionAssessmentCompletedEvent>>(async (db, ct) => (await (db.QueryAsync("SELECT payload,payload_sha256 FROM trade.market_condition_assessment_by_profile WHERE market_profile_id=$1 AND instrument_root=$2 AND target_horizon=$3 AND evaluated_at_utc<$4 ORDER BY evaluated_at_utc DESC,workflow_id LIMIT $5;", [profile, root, horizon.ToString(), beforeUtc, pageSize], r => (r.GetFieldValue<byte[]>(0)).VerifyAssessment(r.GetString(1)), ct))
                .ConfigureAwait(false)).ToArray(), cancellationToken);
            return context._dbFactory.TradeDb.Use("AssessmentProjection.HistoryRead", "SELECT payload,payload_sha256 FROM market_condition_assessment_by_profile WHERE market_profile_id=? AND instrument_root=? AND target_horizon=? AND evaluated_at_utc<? LIMIT ?;")
                .SetParameters(new AssessmentValues([profile, root, horizon.ToString(), beforeUtc, pageSize]))
                .ExecuteQueryAsync(TradeDbContext.MapToAssessment, cancellationToken);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertOrderCompositionAsync operation.</summary>
        internal async Task UpsertOrderCompositionAsyncCoreBinding(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (new PostgresPipelineResultStore().ProjectAsync(db, completed, ct))
                .ConfigureAwait(false);
                    return true;
                }, token))
                .ConfigureAwait(false);
                return;
            }
        
            var result = OrderCompositionContracts.ReadResult(completed.Result);
            if (completed.Id != result.ResultId || completed.WorkflowId != result.WorkflowId || completed.EntityId != result.EntityId || completed.InputWorkflowRevision != result.InputWorkflowRevision || !string.Equals(completed.RequestFingerprint, result.InputSha256, StringComparison.Ordinal))
                throw new InvalidDataException("Composition projection identity mismatch.");
            var body = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
            var inserted = await context._dbFactory.TradeDb.Use("OrderComposition.Insert", "INSERT INTO order_composition_invocation (workflow_id,invocation_id,result_hash,input_hash,payload) VALUES (?,?,?,?,?) IF NOT EXISTS;")
                .SetParameters(new CompositionValues([result.WorkflowId.Value, result.InvocationId, completed.Result.PayloadSha256, result.InputSha256, body]))
                .ExecuteSingleAsync(row => row.GetBool(0), token)
                .ConfigureAwait(false);
            if (!inserted)
            {
                var old = await context.GetOrderCompositionInvocationAsync(result.WorkflowId, result.InvocationId, token)
                .ConfigureAwait(false);
                if (old is null || !OrderCompositionContracts.SameCompletion(old, completed))
                    throw new InvalidOperationException("Conflicting composition invocation.");
            }
        
            // The typed decision retains exact authority even when no candidate is available.
            int portfolioId = result.DecisionContext.PortfolioId;
            int fundId = result.DecisionContext.FundId;
            await context._dbFactory.TradeDb.Use("OrderComposition.HistoryInsert", "INSERT INTO order_composition_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
                .SetParameters(new CompositionValues([portfolioId, fundId, result.DecisionContext.ValueDate, result.EvaluatedAtUtc, result.InvocationId, result.WorkflowId.Value, completed.Id, (short)result.TargetHorizon, (sbyte)result.Outcome, result.Reasons[0], result.ResultId, completed.Result.PayloadSha256]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertRegimeDiscoveryAsync operation.</summary>
        internal async Task UpsertRegimeDiscoveryAsyncCoreBinding(RegimeDiscoveryReadModel result, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(result);
            result.RequireNotNull(nameof(result));
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await context.NativeTransaction().ExecuteOnceAsync((db, ct) => PostgresRegimeDiscoveryStore.UpsertAsync(db, result, ct), cancellationToken)
                .ConfigureAwait(false);
                return;
            }
        
            await context._dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertRegimeDiscovery)}", TradeDbCql.UpsertRegimeDiscovery)
                .SetParameters(new UpsertRegimeDiscovery(result.WorkflowId.Value, result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.SourceEventSequence, result.Status, result.ParameterPayloadSha256, result.SignalSnapshotId, result.ResultPayload.ToArray(), result.ResultPayloadSha256, result.FailureCode, result.FailureMessage, result.ReasonsPayload.ToArray(), result.SchemaVersion, result.TerminalAtUtc, result.UpdatedAtUtc))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
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
            var inserted = await (context._dbFactory.TradeDb.Use("RiskHistory.Insert", "INSERT INTO risk_management_invocation (workflow_id,invocation_id,revision,payload,content_hash) VALUES (?,?,?,?,?) IF NOT EXISTS;")
                .SetParameters(new RiskValues([row.WorkflowId, row.InvocationId, row.Revision, payload, hash]))
                .ExecuteSingleAsync(r => r.GetBool(0), projectionToken))
                .ConfigureAwait(false);
            if (!inserted)
            {
                var old = await (context._dbFactory.TradeDb.Use("RiskHistory.Verify", "SELECT content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? AND revision=?;")
                .SetParameters(new RiskValues([row.WorkflowId, row.InvocationId, row.Revision]))
                .ExecuteSingleAsync(r => r.GetString(0), projectionToken))
                .ConfigureAwait(false);
                if (!string.Equals(old, hash, StringComparison.Ordinal))
                    return new(RiskHistoryProjectionDisposition.Conflict, row.WorkflowId, row.InvocationId, row.Revision, old, hash);
            }
        
            var disposition = inserted ? RiskHistoryProjectionDisposition.Projected : RiskHistoryProjectionDisposition.AlreadyProjected;
            var summary = MessagePackBinarySerializer.Shared.Serialize(row);
            var added = await (context._dbFactory.TradeDb.Use("RiskHistory.SummaryInsert", "INSERT INTO risk_management_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload) VALUES (?,?,?,?,?,?,?) IF NOT EXISTS;")
                .SetParameters(new RiskValues([row.PortfolioId, row.FundId, row.ValueDate, row.EvaluatedAtUtc, row.InvocationId, row.Revision, summary]))
                .ExecuteSingleAsync(r => r.GetBool(0), projectionToken))
                .ConfigureAwait(false);
            if (!added)
                await (context._dbFactory.TradeDb.Use("RiskHistory.SummaryAdvance", "UPDATE risk_management_history SET revision=?,payload=? WHERE portfolio_id=? AND fund_id=? AND value_date=? AND evaluated_at_utc=? AND invocation_id=? IF revision<?;")
                .SetParameters(new RiskValues([row.Revision, summary, row.PortfolioId, row.FundId, row.ValueDate, row.EvaluatedAtUtc, row.InvocationId, row.Revision]))
                .ExecuteSingleAsync(r => r.GetBool(0), projectionToken))
                .ConfigureAwait(false);
            return new(disposition, row.WorkflowId, row.InvocationId, row.Revision, hash, hash);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertTradeOrderAsync operation.</summary>
        internal Task UpsertTradeOrderAsyncCoreBinding(TradeOrderDefinition order, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(order);
            order.RequireNotNull(nameof(order));
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                throw new InvalidOperationException("PostgreSQL order projections require the accepted source timestamp.");
            return context._dbFactory.TradeDb.Use("TradeFlow.Order.Upsert", "INSERT INTO trade_order (portfolioId,fundId,orderId,revision,status,valueDate,updatedAtUtc,definitionHash,payload) VALUES (?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([order.Id.PortfolioId, order.Id.FundId, order.Id.OrderId, order.Revision, order.Status.ToString(), order.ValueDate, DateTime.UtcNow, order.DefinitionHash, MessagePackBinarySerializer.Shared.Serialize(order)]))
                .ExecuteCommandAsync(token);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertOrderExecutionAsync operation.</summary>
        internal async Task UpsertOrderExecutionAsyncCoreBinding(OrderExecutionDefinition execution, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            execution.RequireNotNull(nameof(execution));
            execution.TradeOrderId.Require();
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (context.tradeFlowWriter.ProjectExecutionAsync(db, execution, ct))
                .ConfigureAwait(false);
                    return true;
                }, token))
                .ConfigureAwait(false);
                return;
            }
        
            var payload = MessagePackBinarySerializer.Shared.Serialize(execution);
            await context._dbFactory.TradeDb.Use("TradeFlow.Execution.Upsert", "INSERT INTO order_execution (portfolioId,fundId,orderId,executionAttemptId,status,startedAtUtc,completedAtUtc,payload) VALUES (?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([execution.TradeOrderId.PortfolioId, execution.TradeOrderId.FundId, execution.TradeOrderId.OrderId, execution.ExecutionAttemptId, execution.Status.ToString(), execution.StartedAtUtc, execution.CompletedAtUtc, payload]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            foreach (var fill in execution.Fills)
                await context._dbFactory.TradeDb.Use("TradeFlow.Fill.Upsert.V2", "INSERT INTO order_execution_fill (executionAttemptId,executionFillId,componentId,tradeLegId,contractId,filledAtUtc,payload) VALUES (?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([fill.ExecutionAttemptId, fill.ExecutionFillId, fill.ComponentId, fill.TradeLegId, fill.ContractId, fill.FilledAtUtc, MessagePackBinarySerializer.Shared.Serialize(fill)]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertEstablishedTradeAsync operation.</summary>
        internal async Task UpsertEstablishedTradeAsyncCoreBinding(EstablishedTradeDefinition trade, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(trade);
            trade.RequireNotNull(nameof(trade));
            trade.Id.Require();
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (context.tradeFlowWriter.ProjectTradeAsync(db, trade, ct))
                .ConfigureAwait(false);
                    return true;
                }, token))
                .ConfigureAwait(false);
                return;
            }
        
            var payload = MessagePackBinarySerializer.Shared.Serialize(trade);
            await context._dbFactory.TradeDb.Use("TradeFlow.Trade.Upsert", "INSERT INTO established_trade (portfolioId,fundId,orderId,tradeId,assetFamily,strategyKind,establishedAtUtc,evidenceRevision,payload) VALUES (?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId, trade.Id.TradeId, trade.AssetFamily.ToString(), trade.StrategyKind.ToString(), trade.EstablishedAtUtc, trade.EvidenceRevision, payload]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            await context._dbFactory.TradeDb.Use("TradeFlow.Trade.History.Upsert", "INSERT INTO established_trade_history (portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision,payload) VALUES (?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([trade.Id.PortfolioId, trade.Id.FundId, trade.StrategyKind.ToString(), trade.EstablishedAtUtc, trade.Id.OrderId, trade.Id.TradeId, trade.EvidenceRevision, payload]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertStrategyPositionAsync operation.</summary>
        internal async Task UpsertStrategyPositionAsyncCoreBinding(StrategyPositionSnapshot position, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(position);
            position.RequireNotNull(nameof(position));
            position.RequireValidIdentity();
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (context.tradeFlowWriter.ProjectPositionAsync(db, position, ct))
                .ConfigureAwait(false);
                    return true;
                }, token))
                .ConfigureAwait(false);
                return;
            }
        
            var payload = MessagePackBinarySerializer.Shared.Serialize(position);
            var id = position.Id.Trade;
            var projectionTimestamp = checked((position.AsOfUtc.Ticks - DateTime.UnixEpoch.Ticks) / 10 + position.PositionSequence);
            await context._dbFactory.TradeDb.Use("TradeFlow.Position.Current", "INSERT INTO strategy_position_current (portfolioId,fundId,orderId,tradeId,positionId,strategyKind,positionSequence,routeGeneration,isOpen,asOfUtc,payload) VALUES (?,?,?,?,?,?,?,?,?,?,?) USING TIMESTAMP ?;")
                .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.Id.PositionId, position.StrategyKind.ToString(), position.PositionSequence, position.RouteGeneration, position.IsOpen, position.AsOfUtc, payload, projectionTimestamp]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            await context._dbFactory.TradeDb.Use("TradeFlow.Position.History", "INSERT INTO strategy_position_history (positionId,asOfUtc,positionSequence,phase,payload) VALUES (?,?,?,?,?) USING TIMESTAMP ?;")
                .SetParameters(new TradeDbValues([position.Id.PositionId, position.HistoryAsOfUtc == default ? position.AsOfUtc : position.HistoryAsOfUtc, position.HistoryPositionSequence == 0 ? position.PositionSequence : position.HistoryPositionSequence, position.Phase.ToString(), payload, projectionTimestamp]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ReplaceOpenPositionRoutesAsync operation.</summary>
        internal async Task ReplaceOpenPositionRoutesAsyncCoreBinding(StrategyPositionSnapshot position, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(position);
            position.RequireNotNull(nameof(position));
            position.RequireRoutableLegs();
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (context.tradeFlowWriter.ProjectPositionAsync(db, position, ct))
                .ConfigureAwait(false);
                    return true;
                }, token))
                .ConfigureAwait(false);
                return;
            }
        
            var existing = await context._dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Position.Get.V2", "SELECT contractId,tradeLegId FROM open_position_route_recovery WHERE shard=? AND positionId=?;")
                .SetParameters(new TradeDbValues([(sbyte)0, position.Id.PositionId]))
                .ExecuteQueryAsync(row => new OpenPositionRouteKeyReadModel(row.GetString(0), row.GetGuid(1)), token)
                .ConfigureAwait(false);
            foreach (var old in existing)
            {
                await context._dbFactory.TradeDb.Use("TradeFlow.Route.Delete.V2", "DELETE FROM open_position_route WHERE contractId=? AND positionId=? AND tradeLegId=?;")
                .SetParameters(new TradeDbValues([old.ContractId, position.Id.PositionId, old.TradeLegId]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
                await context._dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Delete.V2", "DELETE FROM open_position_route_recovery WHERE shard=? AND positionId=? AND tradeLegId=?;")
                .SetParameters(new TradeDbValues([(sbyte)0, position.Id.PositionId, old.TradeLegId]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            }
        
            if (!position.IsOpen)
                return;
            foreach (var leg in position.Legs)
            {
                var route = new PortfolioFundTradeLeg(position.Id.Trade.PortfolioId, position.Id.Trade.FundId, position.Id.Trade.OrderId, position.Id.Trade.TradeId, position.Id.PositionId, leg.TradeLegId, position.StrategyKind, position.RouteGeneration);
                await context._dbFactory.TradeDb.Use("TradeFlow.Route.Upsert.V2", "INSERT INTO open_position_route (contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation) VALUES (?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([leg.ContractId, route.PortfolioId, route.FundId, route.OrderId, route.TradeId, route.StrategyPositionId, route.TradeLegId, route.TradeType.ToString(), route.Generation]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
                await context._dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Upsert.V2", "INSERT INTO open_position_route_recovery (shard,positionId,tradeLegId,contractId,portfolioId,fundId,orderId,tradeId,tradeType,generation) VALUES (?,?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([(sbyte)0, route.StrategyPositionId, route.TradeLegId, leg.ContractId, route.PortfolioId, route.FundId, route.OrderId, route.TradeId, route.TradeType.ToString(), route.Generation]))
                .ExecuteCommandAsync(token)
                .ConfigureAwait(false);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the GetOpenPositionRoutesAsync operation.</summary>
        internal async Task<ICollection<OpenPositionRouteReadModel>> GetOpenPositionRoutesAsyncCoreBinding(string contractId, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(contractId);
            contractId.RequireContractId();
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
                return (await (context.NativeTransaction().ExecuteOnceAsync((db, ct) => new PostgresTradeFlowReadStore().RoutesAsync(db, contractId, ct), token))
                .ConfigureAwait(false)).ToArray();
            var result = await context._dbFactory.TradeDb.Use("TradeFlow.Route.Get.V2", "SELECT portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route WHERE contractId=?;")
                .SetParameters(new TradeDbValues([contractId]))
                .ExecuteQueryAsync(row => new OpenPositionRouteReadModel(contractId, new PortfolioFundTradeLeg(row.GetInt(0), row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetGuid(4), row.GetGuid(5), row.GetEnum<TradeStrategyKind>(6), row.GetLong(7))), token)
                .ConfigureAwait(false);
            return result;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the UpsertTradeSelectionAsync operation.</summary>
        internal async Task UpsertTradeSelectionAsyncCoreBinding(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(completed);
            if (string.Equals(context.ProviderName, "System.Data.Postgres", StringComparison.Ordinal))
            {
                await (context.NativeTransaction().ExecuteOnceAsync(async (db, ct) =>
                {
                    await (new PostgresPipelineResultStore().ProjectAsync(db, completed, ct))
                .ConfigureAwait(false);
                    return true;
                }, cancellationToken))
                .ConfigureAwait(false);
                return;
            }
        
            var r = TradeSelectionContracts.ReadResult(completed.Result);
            TradeSelectionContracts.Require(completed.WorkflowId == r.WorkflowId && completed.CommandId == r.InvocationId && completed.Id == r.ResultId && completed.EntityId == r.EntityId && completed.InputWorkflowRevision == r.InputWorkflowRevision && completed.RequestFingerprint.Length == 64, "TS.RESULT.INVALID", "Projection event identity mismatch.");
            var selected = r.SelectedCandidate;
            var binding = r.DecisionContext.SelectionBinding;
            var bytes = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
            object[] values = [r.WorkflowId.Value, r.InvocationId, 1L, completed.Id, r.PortfolioId, r.FundId, (short)r.DecisionHorizon, (sbyte)2, (sbyte)r.Outcome, r.ProducedAtUtc, r.PrimaryReasonCode, selected?.DeploymentKey.Id!, selected?.DeploymentKey.Version!, selected?.StrategyKey.Id!, selected?.StrategyKey.Version!, selected?.StructureKey.Id!, selected?.StructureKey.Version!, selected?.VariantKey.Id!, selected?.VariantKey.Version!, TradeSelectionContracts.WireHash(binding.Candidates.Select(x => x.CandidateHash).ToArray()), binding.PayloadSha256, binding.CommonPolicy.Id, binding.CommonPolicy.Version, binding.CommonPolicy.PayloadSha256, r.ResultId, completed.Result.PayloadSha256, MessagePackBinarySerializer.Shared.Serialize(r)!, bytes];
            var inserted = await context._dbFactory.TradeDb.Use("TradeSelection.Insert", "INSERT INTO trade_selection_invocation_event (workflow_id,invocation_id,source_sequence,event_id,portfolio_id,fund_id,target_horizon,lifecycle_status,outcome,occurred_at_utc,reason_code,selected_deployment_id,selected_deployment_version,selected_strategy_id,selected_strategy_version,selected_structure_id,selected_structure_version,selected_variant_id,selected_variant_version,candidate_set_sha256,binding_sha256,parameter_set_id,parameter_version,parameter_sha256,result_id,result_sha256,result_payload,event_payload) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?) IF NOT EXISTS;")
                .SetParameters(new SelectionValues(values))
                .ExecuteSingleAsync(row => row.GetBool(0), cancellationToken)
                .ConfigureAwait(false);
            if (!inserted)
            {
                var existing = await context.GetTradeSelectionInvocationAsync(r.WorkflowId, r.InvocationId, cancellationToken)
                .ConfigureAwait(false);
                if (existing is null || !TradeSelectionContracts.SameCompletion(existing, completed))
                    throw new InvalidOperationException("Conflicting selector projection for the same invocation.");
            }
        
            await context._dbFactory.TradeDb.Use("TradeSelection.HistoryInsert", "INSERT INTO trade_selection_history_by_fund_date (portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
                .SetParameters(new SelectionValues([r.PortfolioId, r.FundId, DateOnly.FromDateTime(r.ProducedAtUtc), r.ProducedAtUtc, r.WorkflowId.Value, r.InvocationId, completed.Id, (short)r.DecisionHorizon, (sbyte)r.Outcome, r.PrimaryReasonCode, r.ResultId, completed.Result.PayloadSha256]))
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Executes the NativeTransaction persistence helper.</summary>
        internal EventSourceDb.PostgresEventTransaction NativeTransaction() => new(new DbConnectionSettings().Add(EventSourceDb.EventSourceActorDbContext.EventSourceActorDbConnection, context.ConnectionString, "System.Data.Postgres"));

        /// <summary>Hydrates an option trade with its persisted positions, legs, limits, and fills.</summary>
        internal async Task<OptionTradeReadModel> FillOptionTradeAsync(OptionTradeReadModel optionTrade)
        {
            var entityId = optionTrade.EntityId;
            var db = context._dbFactory.TradeDb;
            var tradePositionsTask = db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
                .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradePosition!);
            var optionLegsTask = db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
                .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToOptionLeg!);
            var tradeLimitTask = context.GetTradeLimitAsync(optionTrade.TradeId);
            var tradeTypeLimitsTask = db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
                .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradeTypeLimit);
            var tradeFillsTask = context.GetTradeFillsAsync(optionTrade.OrderId, optionTrade.TradeId);

            var tradePositions = await (tradePositionsTask)
                .ConfigureAwait(false);
            var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
            var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
            await (Parallel.ForEachAsync(
                Enumerable.Range(0, valueDates.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (dateIndex, _) => legDataByDate[dateIndex] = await (db
                    .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
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

            return optionTrade
                .AddOptionLegs(optionLegs)
                .AddTradePosition(tradePositions)
                .SetTradeLimit((await (tradeLimitTask)
                .ConfigureAwait(false))!)
                .AddTradeTypeLimits(await (tradeTypeLimitsTask)
                .ConfigureAwait(false))
                .AddTradeFills(await (tradeFillsTask)
                .ConfigureAwait(false));
        }

        /// <summary>Hydrates an option trade with its persisted graph while observing cancellation.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task<OptionTradeReadModel> FillOptionTradeAsync(
                OptionTradeReadModel optionTrade,
                CancellationToken cancellationToken)
        {
            var entityId = optionTrade.EntityId;
            var db = context._dbFactory.TradeDb;
            var tradePositionsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
                .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradePosition!, cancellationToken);
            var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
                .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToOptionLeg!, cancellationToken);
            var tradeLimitTask = context.GetTradeLimitAsync(optionTrade.TradeId, cancellationToken);
            var tradeTypeLimitsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
                .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradeTypeLimit, cancellationToken);
            var tradeFillsTask = context.GetTradeFillsAsync(
                optionTrade.OrderId,
                optionTrade.TradeId,
                cancellationToken);

            var tradePositions = await (tradePositionsTask)
                .ConfigureAwait(false);
            var valueDates = tradePositions
                .Select(position => position.ValueDate)
                .Distinct()
                .ToArray();
            var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
            await (Parallel.ForEachAsync(
                Enumerable.Range(0, valueDates.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellationToken
                },
                async (dateIndex, token) =>
                    legDataByDate[dateIndex] = await (db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                        .SetParameters(new GetOptionLegData(
                            entityId.OrderId,
                            entityId.TradeId,
                            valueDates[dateIndex]))
                        .ExecuteQueryAsync(TradeDbContext.MapToOptionLegData!, token))
                .ConfigureAwait(false)))
                .ConfigureAwait(false);

            await (Task.WhenAll(
                optionLegsTask,
                tradeLimitTask,
                tradeTypeLimitsTask,
                tradeFillsTask))
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var optionLegs = await (optionLegsTask)
                .ConfigureAwait(false);
            var optionLegById = optionLegs.ToDictionary(
                leg => leg.ContractId,
                StringComparer.Ordinal);
            var legDataByPosition = TradeDbContext.MapToLegDataByPosition(
                legDataByDate,
                optionLegById,
                requireOptionLeg: true);
            foreach (var position in tradePositions)
            {
                if (legDataByPosition.TryGetValue(TradeDbContext.MapToPositionKey(position), out var legData))
                    position.AddOptionLegData(legData);
            }

            return optionTrade
                .AddOptionLegs(optionLegs)
                .AddTradePosition(tradePositions)
                .SetTradeLimit((await (tradeLimitTask)
                .ConfigureAwait(false))!)
                .AddTradeTypeLimits(await (tradeTypeLimitsTask)
                .ConfigureAwait(false))
                .AddTradeFills(await (tradeFillsTask)
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
            if (string.IsNullOrWhiteSpace(profile) || !string.Equals(root,"ES",StringComparison.Ordinal) ||
                !MarketConditionAssessmentParameterSet.IsHorizon(horizon) || pageSize is < 1 or > 100 ||
                !MarketConditionAssessmentContracts.Utc(beforeUtc))
                throw new ArgumentException("Invalid assessment history partition or bound.",nameof(profile));
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
            if (!string.Equals(completed.Snapshot.ComputeHash(),result.SnapshotSha256,StringComparison.Ordinal) ||
                !string.Equals(completed.Snapshot.PayloadSha256,result.SnapshotSha256,StringComparison.Ordinal))
                throw new ArgumentException("Assessment projection snapshot hash mismatch.",nameof(result));
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
                throw new ArgumentException(message,nameof(workflowId));
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
                throw new ArgumentException(message,nameof(identifier));
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
            if (identifier == Guid.Empty || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc ||
                fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("Valid PositionId, UTC range, and page size 1..1000 are required.",nameof(identifier));
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
                throw new ArgumentException(message,nameof(portfolioId));
        }

        /// <summary>Validates a portfolio/fund UTC history range and page size.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireUtcHistoryScope(int fundId, DateTime fromUtc, DateTime toUtc, int pageSize)
        {
            if (portfolioId <= 0 || fundId <= 0 || fromUtc.Kind != DateTimeKind.Utc ||
                toUtc.Kind != DateTimeKind.Utc || fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("Valid ownership, UTC range, and page size 1..1000 are required.",nameof(portfolioId));
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
                throw new ArgumentException(
                    "Valid Portfolio, Fund, Order, and Trade identity is required.",
                    nameof(id));
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
                throw new ArgumentException(
                    "Valid Portfolio, Fund, and Order identity is required.",
                    nameof(id));
        }
    }

    extension<T>(T? value) where T : class
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
}
