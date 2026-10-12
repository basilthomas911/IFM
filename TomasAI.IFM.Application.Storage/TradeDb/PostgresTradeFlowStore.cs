using TomasAI.IFM.Domain.Trade.Shared.TradeOrder;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Storage.EventSourceDb;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Materializes accepted trade lifecycle facts on the source event owner's transaction. No broker or actor calls.</summary>
public sealed class PostgresTradeFlowStore
{
    /// <summary>Stores the accepted immutable order revision, retaining its operational value date.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task<int> ProjectOrderAsync(IEnlistedPostgresTransaction db,TradeOrderDefinition order,DateTime acceptedAtUtc,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(order);
        var stored=await (db.ScalarAsync("""
            INSERT INTO trade.trade_order(portfolioId,fundId,orderId,revision,status,valueDate,updatedAtUtc,definitionHash,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT(portfolioId,fundId,orderId,revision) DO UPDATE SET payload=EXCLUDED.payload,status=EXCLUDED.status,
              valueDate=EXCLUDED.valueDate,definitionHash=EXCLUDED.definitionHash,updatedAtUtc=EXCLUDED.updatedAtUtc
              WHERE trade.trade_order.payload=EXCLUDED.payload AND trade.trade_order.updatedAtUtc=EXCLUDED.updatedAtUtc RETURNING revision;
            """,[order.Id.PortfolioId,order.Id.FundId,order.Id.OrderId,order.Revision,order.Status.ToString(),order.ValueDate,
                acceptedAtUtc,order.DefinitionHash,MessagePackBinarySerializer.Shared.Serialize(order)],token))
            .ConfigureAwait(false);
        if(stored is null) throw new InvalidDataException("Accepted order revision conflicts with different source facts.");
        return 1;
    }

    /// <summary>Stores the accepted execution and all recorded fills in the same transaction.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectExecutionAsync(IEnlistedPostgresTransaction db,OrderExecutionDefinition execution,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(execution);
        var id=execution.TradeOrderId;
        await (db.ExecuteAsync("""
            INSERT INTO trade.order_execution(portfolioId,fundId,orderId,executionAttemptId,status,startedAtUtc,completedAtUtc,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT(portfolioId,fundId,orderId,executionAttemptId) DO UPDATE SET status=EXCLUDED.status,
              startedAtUtc=EXCLUDED.startedAtUtc,completedAtUtc=EXCLUDED.completedAtUtc,payload=EXCLUDED.payload;
            """,[id.PortfolioId,id.FundId,id.OrderId,execution.ExecutionAttemptId,execution.Status.ToString(),execution.StartedAtUtc,
                execution.CompletedAtUtc,MessagePackBinarySerializer.Shared.Serialize(execution)],token))
            .ConfigureAwait(false);
        foreach(var fill in execution.Fills)
            await (db.ExecuteAsync("""
                INSERT INTO trade.order_execution_fill(executionAttemptId,executionFillId,componentId,tradeLegId,contractId,filledAtUtc,payload)
                VALUES($1,$2,$3,$4,$5,$6,$7)
                ON CONFLICT(executionAttemptId,filledAtUtc,executionFillId) DO UPDATE SET componentId=EXCLUDED.componentId,
                  tradeLegId=EXCLUDED.tradeLegId,contractId=EXCLUDED.contractId,payload=EXCLUDED.payload;
                """,[fill.ExecutionAttemptId,fill.ExecutionFillId,fill.ComponentId,fill.TradeLegId,fill.ContractId,fill.FilledAtUtc,
                    MessagePackBinarySerializer.Shared.Serialize(fill)],token))
            .ConfigureAwait(false);
    }

    /// <summary>Stores the established trade and its dated evidence history together.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectTradeAsync(IEnlistedPostgresTransaction db,EstablishedTradeDefinition trade,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(trade);
        var id=trade.Id;var payload=MessagePackBinarySerializer.Shared.Serialize(trade);
        var historyApplied = await db.ScalarAsync("""
            INSERT INTO trade.established_trade_history(portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT(portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision)
            DO UPDATE SET payload=EXCLUDED.payload
              WHERE trade.established_trade_history.payload=EXCLUDED.payload
            RETURNING evidenceRevision;
            """, [id.PortfolioId,id.FundId,trade.StrategyKind.ToString(),trade.EstablishedAtUtc,id.OrderId,id.TradeId,trade.EvidenceRevision,payload],token)
            .ConfigureAwait(false);
        if (historyApplied is null)
            throw new InvalidDataException("Accepted trade history revision conflicts with different source facts.");
        var currentApplied = await db.ScalarAsync("""
            INSERT INTO trade.established_trade(portfolioId,fundId,orderId,tradeId,assetFamily,strategyKind,establishedAtUtc,evidenceRevision,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT(portfolioId,fundId,orderId,tradeId) DO UPDATE SET assetFamily=EXCLUDED.assetFamily,strategyKind=EXCLUDED.strategyKind,
              establishedAtUtc=EXCLUDED.establishedAtUtc,evidenceRevision=EXCLUDED.evidenceRevision,payload=EXCLUDED.payload
              WHERE trade.established_trade.evidenceRevision<EXCLUDED.evidenceRevision OR
                (trade.established_trade.evidenceRevision=EXCLUDED.evidenceRevision AND trade.established_trade.payload=EXCLUDED.payload)
            RETURNING evidenceRevision;
            """,[id.PortfolioId,id.FundId,id.OrderId,id.TradeId,trade.AssetFamily.ToString(),trade.StrategyKind.ToString(),trade.EstablishedAtUtc,trade.EvidenceRevision,payload],token)
            .ConfigureAwait(false);
        if (currentApplied is null)
        {
            var currentRevision = await db.ScalarAsync("SELECT evidenceRevision FROM trade.established_trade WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4;",
                [id.PortfolioId,id.FundId,id.OrderId,id.TradeId],token)
            .ConfigureAwait(false);
            if (Convert.ToInt32(currentRevision, System.Globalization.CultureInfo.InvariantCulture) == trade.EvidenceRevision)
                throw new InvalidDataException("Accepted current trade revision conflicts with different source facts.");
        }
    }

    /// <summary>Atomically updates current state, the stable daily history row and both open-leg routing indexes.
    /// Older position sequences cannot overwrite current state or restore routes for an already closed position.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectPositionAsync(IEnlistedPostgresTransaction db,StrategyPositionSnapshot position,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(position);
        var id=position.Id.Trade;var payload=MessagePackBinarySerializer.Shared.Serialize(position);
        var applied=await (db.ScalarAsync("""
            INSERT INTO trade.strategy_position_current(portfolioId,fundId,orderId,tradeId,positionId,strategyKind,positionSequence,routeGeneration,isOpen,asOfUtc,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            ON CONFLICT(portfolioId,fundId,orderId,tradeId,positionId) DO UPDATE SET strategyKind=EXCLUDED.strategyKind,
              positionSequence=EXCLUDED.positionSequence,routeGeneration=EXCLUDED.routeGeneration,isOpen=EXCLUDED.isOpen,
              asOfUtc=EXCLUDED.asOfUtc,payload=EXCLUDED.payload
              WHERE trade.strategy_position_current.positionSequence<EXCLUDED.positionSequence OR
                (trade.strategy_position_current.positionSequence=EXCLUDED.positionSequence AND trade.strategy_position_current.payload=EXCLUDED.payload)
              RETURNING positionSequence;
            """,[id.PortfolioId,id.FundId,id.OrderId,id.TradeId,position.Id.PositionId,position.StrategyKind.ToString(),position.PositionSequence,
                position.RouteGeneration,position.IsOpen,position.AsOfUtc,payload],token))
            .ConfigureAwait(false);
        if(applied is null)
        {
            var current=await (db.ScalarAsync("SELECT positionSequence FROM trade.strategy_position_current WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4 AND positionId=$5;",[id.PortfolioId,id.FundId,id.OrderId,id.TradeId,position.Id.PositionId],token))
            .ConfigureAwait(false);
            if(Convert.ToInt64(current,System.Globalization.CultureInfo.InvariantCulture)==position.PositionSequence) throw new InvalidDataException("Accepted position sequence conflicts with different source facts.");
            return;
        }
        await (db.ExecuteAsync("""
            INSERT INTO trade.strategy_position_history(positionId,asOfUtc,positionSequence,phase,payload,projection_sequence)
            VALUES($1,$2,$3,$4,$5,$6)
            ON CONFLICT(positionId,asOfUtc,positionSequence) DO UPDATE SET phase=EXCLUDED.phase,payload=EXCLUDED.payload,
              projection_sequence=EXCLUDED.projection_sequence
              WHERE trade.strategy_position_history.projection_sequence<=EXCLUDED.projection_sequence;
            """,[position.Id.PositionId,position.HistoryAsOfUtc==default?position.AsOfUtc:position.HistoryAsOfUtc,
                position.HistoryPositionSequence==0?position.PositionSequence:position.HistoryPositionSequence,position.Phase.ToString(),payload,position.PositionSequence],token))
            .ConfigureAwait(false);
        await (db.ExecuteAsync("DELETE FROM trade.open_position_route WHERE positionId=$1;",[position.Id.PositionId],token))
            .ConfigureAwait(false);
        await (db.ExecuteAsync("DELETE FROM trade.open_position_route_recovery WHERE positionId=$1;",[position.Id.PositionId],token))
            .ConfigureAwait(false);
        if(!position.IsOpen) return;
        foreach(var leg in position.Legs)
        {
            await (db.ExecuteAsync("""
                INSERT INTO trade.open_position_route(contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9);
                """,[leg.ContractId,id.PortfolioId,id.FundId,id.OrderId,id.TradeId,position.Id.PositionId,leg.TradeLegId,position.StrategyKind.ToString(),position.RouteGeneration],token))
            .ConfigureAwait(false);
            await (db.ExecuteAsync("""
                INSERT INTO trade.open_position_route_recovery(shard,positionId,tradeLegId,contractId,portfolioId,fundId,orderId,tradeId,tradeType,generation)
                VALUES(0,$1,$2,$3,$4,$5,$6,$7,$8,$9);
                """,[position.Id.PositionId,leg.TradeLegId,leg.ContractId,id.PortfolioId,id.FundId,id.OrderId,id.TradeId,position.StrategyKind.ToString(),position.RouteGeneration],token))
            .ConfigureAwait(false);
        }
    }
    /// <summary>Stores the accepted whole-trade limit and both spread limits on the source transaction.</summary>
    public async Task ProjectMonitoringLimitsAsync(IEnlistedPostgresTransaction db,
        TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit,
        IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(limit);
        await (db.ExecuteAsync("""
            INSERT INTO trade.trade_limit (tradeId,tradeType,riskMargin,maxProfit,maxLoss,maxReturn,maxLossLimit,minProfitLimit,maxProfitLimit,minProfitTarget,dailyProfitTarget,createdOn,createdBy,updatedOn,updatedBy)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            ON CONFLICT(tradeId,tradeType) DO UPDATE SET riskMargin=EXCLUDED.riskMargin,maxProfit=EXCLUDED.maxProfit,maxLoss=EXCLUDED.maxLoss,
                maxReturn=EXCLUDED.maxReturn,maxLossLimit=EXCLUDED.maxLossLimit,minProfitLimit=EXCLUDED.minProfitLimit,maxProfitLimit=EXCLUDED.maxProfitLimit,
                minProfitTarget=EXCLUDED.minProfitTarget,dailyProfitTarget=EXCLUDED.dailyProfitTarget,createdOn=EXCLUDED.createdOn,createdBy=EXCLUDED.createdBy,
                updatedOn=EXCLUDED.updatedOn,updatedBy=EXCLUDED.updatedBy;
            """,[limit.TradeId,limit.TradeType.ToString(),limit.RiskMargin,limit.MaxProfit,limit.MaxLoss,limit.MaxReturn,limit.MaxLossLimit,limit.MinProfitLimit,
                limit.MaxProfitLimit,limit.MinProfitTarget,limit.DailyProfitTarget,limit.CreatedOn,limit.CreatedBy,limit.UpdatedOn,limit.UpdatedBy],token))
            .ConfigureAwait(false);
        await (ProjectSpreadLimitsAsync(db,spreads,token))
            .ConfigureAwait(false);
    }

    /// <summary>Stores the exact accepted spread limit values without recalculating the whole-trade limit.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectSpreadLimitsAsync(IEnlistedPostgresTransaction db,
        IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(spreads);
        foreach(var spread in spreads)
            await (db.ExecuteAsync("""
                INSERT INTO trade.trade_type_limit(tradeId,tradeType,maxLossLimit,minProfitLimit,maxProfitLimit) VALUES($1,$2,$3,$4,$5)
                ON CONFLICT(tradeId,tradeType) DO UPDATE SET maxLossLimit=EXCLUDED.maxLossLimit,minProfitLimit=EXCLUDED.minProfitLimit,maxProfitLimit=EXCLUDED.maxProfitLimit;
                """,[spread.TradeId,spread.TradeType.ToString(),spread.MaxLossLimit,spread.MinProfitLimit,spread.MaxProfitLimit],token))
            .ConfigureAwait(false);
    }

}
