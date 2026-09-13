using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial interface ITradeDbContext
{
    Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default);
    Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default);
    Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default);
    Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default);
    Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default);
    Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default);
    Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId, TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default);
    Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default);
    Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default);
    Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, string actor, CancellationToken token = default);
    Task<ICollection<(uint InstrumentId, MarketPositionRoute Route)>> GetOpenPositionRoutesAsync(uint marketInstrumentId, CancellationToken token = default);
    Task<ICollection<(uint InstrumentId, MarketPositionRoute Route)>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default);
}

public partial class TradeDbContext
{
    public Task UpsertTradeOrderAsync(TradeOrderDefinition order, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        return _dbFactory.TradeDb.Use("TradeFlow.Order.Upsert", "INSERT INTO trade_order_v3 (portfolioId,fundId,orderId,revision,status,valueDate,updatedAtUtc,definitionHash,payload) VALUES (?,?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([order.Id.PortfolioId, order.Id.FundId, order.Id.OrderId, order.Revision,
                order.Status.ToString(), order.ValueDate, DateTime.UtcNow, order.DefinitionHash,
                MessagePackBinarySerializer.Shared.Serialize(order)]))
            .ExecuteCommandAsync(token);
    }

    public Task<TradeOrderDefinition?> GetTradeOrderAsync(TradeOrderId id, CancellationToken token = default)
    {
        Require(id);
        return _dbFactory.TradeDb.Use("TradeFlow.Order.Get", "SELECT payload FROM trade_order_v3 WHERE portfolioId=? AND fundId=? AND orderId=? LIMIT 1;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<TradeOrderDefinition>(row.GetBytes(0)), token);
    }

    public async Task UpsertOrderExecutionAsync(OrderExecutionDefinition execution, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        Require(execution.TradeOrderId);
        var payload = MessagePackBinarySerializer.Shared.Serialize(execution);
        await _dbFactory.TradeDb.Use("TradeFlow.Execution.Upsert", "INSERT INTO order_execution_v1 (portfolioId,fundId,orderId,executionAttemptId,status,startedAtUtc,completedAtUtc,payload) VALUES (?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([execution.TradeOrderId.PortfolioId, execution.TradeOrderId.FundId, execution.TradeOrderId.OrderId,
                execution.ExecutionAttemptId, execution.Status.ToString(), execution.StartedAtUtc, execution.CompletedAtUtc, payload]))
            .ExecuteCommandAsync(token).ConfigureAwait(false);
        foreach (var fill in execution.Fills)
            await _dbFactory.TradeDb.Use("TradeFlow.Fill.Upsert", "INSERT INTO order_execution_fill_v1 (executionAttemptId,executionFillId,componentId,tradeLegId,marketInstrumentId,filledAtUtc,payload) VALUES (?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([fill.ExecutionAttemptId, fill.ExecutionFillId, fill.ComponentId, fill.TradeLegId,
                    (long)fill.MarketInstrumentId, fill.FilledAtUtc, MessagePackBinarySerializer.Shared.Serialize(fill)]))
                .ExecuteCommandAsync(token).ConfigureAwait(false);
    }

    public Task<OrderExecutionDefinition?> GetOrderExecutionAsync(TradeOrderId id, Guid executionAttemptId, CancellationToken token = default)
    {
        Require(id);
        if (executionAttemptId == Guid.Empty) throw new ArgumentException("ExecutionAttemptId is required.", nameof(executionAttemptId));
        return _dbFactory.TradeDb.Use("TradeFlow.Execution.Get", "SELECT payload FROM order_execution_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND executionAttemptId=?;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, executionAttemptId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(row.GetBytes(0)), token);
    }

    public async Task UpsertEstablishedTradeAsync(EstablishedTradeDefinition trade, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(trade);
        Require(trade.Id);
        var payload = MessagePackBinarySerializer.Shared.Serialize(trade);
        await _dbFactory.TradeDb.Use("TradeFlow.Trade.Upsert", "INSERT INTO established_trade_v1 (portfolioId,fundId,orderId,tradeId,assetFamily,strategyKind,establishedAtUtc,evidenceRevision,payload) VALUES (?,?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId, trade.Id.TradeId,
                trade.AssetFamily.ToString(), trade.StrategyKind.ToString(), trade.EstablishedAtUtc, trade.EvidenceRevision,
                payload])).ExecuteCommandAsync(token).ConfigureAwait(false);
        await _dbFactory.TradeDb.Use("TradeFlow.Trade.History.Upsert", "INSERT INTO established_trade_history_v2 (portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision,payload) VALUES (?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([trade.Id.PortfolioId, trade.Id.FundId, trade.StrategyKind.ToString(),
                trade.EstablishedAtUtc, trade.Id.OrderId, trade.Id.TradeId, trade.EvidenceRevision, payload]))
            .ExecuteCommandAsync(token).ConfigureAwait(false);
    }

    public Task<EstablishedTradeDefinition?> GetEstablishedTradeAsync(TradeEntityId id, CancellationToken token = default)
    {
        Require(id);
        return _dbFactory.TradeDb.Use("TradeFlow.Trade.Get", "SELECT payload FROM established_trade_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=?;")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, id.TradeId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), token);
    }

    public Task<QueryPage<EstablishedTradeDefinition>> GetEstablishedTradesAsync(int portfolioId, int fundId,
        TradeStrategyKind strategyKind, DateTime fromUtc, DateTime toUtc, int pageSize,
        byte[]? pagingState = null, CancellationToken token = default)
    {
        if (portfolioId <= 0 || fundId <= 0 || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc ||
            fromUtc > toUtc || pageSize is < 1 or > 1000)
            throw new ArgumentException("Valid ownership, UTC range, and page size 1..1000 are required.");
        return _dbFactory.TradeDb.Use("TradeFlow.Trade.History.Get", "SELECT payload FROM established_trade_history_v2 WHERE portfolioId=? AND fundId=? AND strategyKind=? AND establishedAtUtc>=? AND establishedAtUtc<=?;")
            .SetParameters(new TradeDbValues([portfolioId, fundId, strategyKind.ToString(), fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<EstablishedTradeDefinition>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    public async Task UpsertStrategyPositionAsync(StrategyPositionSnapshot position, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (!position.Id.IsValid) throw new ArgumentException("Valid position identity is required.", nameof(position));
        var payload = MessagePackBinarySerializer.Shared.Serialize(position);
        var id = position.Id.Trade;
        await _dbFactory.TradeDb.Use("TradeFlow.Position.Current", "INSERT INTO strategy_position_current_v1 (portfolioId,fundId,orderId,tradeId,positionId,strategyKind,positionSequence,routeGeneration,isOpen,asOfUtc,payload) VALUES (?,?,?,?,?,?,?,?,?,?,?);")
            .SetParameters(new TradeDbValues([id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.Id.PositionId,
                position.StrategyKind.ToString(), position.PositionSequence, position.RouteGeneration, position.IsOpen, position.AsOfUtc, payload]))
            .ExecuteCommandAsync(token).ConfigureAwait(false);
        await _dbFactory.TradeDb.Use("TradeFlow.Position.History", "INSERT INTO strategy_position_history_v1 (positionId,asOfUtc,positionSequence,phase,payload) VALUES (?,?,?,?,?);")
            .SetParameters(new TradeDbValues([position.Id.PositionId, position.AsOfUtc, position.PositionSequence, position.Phase.ToString(), payload]))
            .ExecuteCommandAsync(token).ConfigureAwait(false);
    }

    public Task<QueryPage<StrategyPositionSnapshot>> GetStrategyPositionHistoryAsync(Guid positionId, DateTime fromUtc, DateTime toUtc, int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        if (positionId == Guid.Empty || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc ||
            fromUtc > toUtc || pageSize is < 1 or > 1000)
            throw new ArgumentException("Valid PositionId, UTC range, and page size 1..1000 are required.");
        return _dbFactory.TradeDb.Use("TradeFlow.Position.History.Get", "SELECT payload FROM strategy_position_history_v1 WHERE positionId=? AND asOfUtc>=? AND asOfUtc<=?;")
            .SetParameters(new TradeDbValues([positionId, fromUtc, toUtc]))
            .ExecutePageAsync(row => MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetBytes(0)), pageSize, pagingState, token);
    }

    public Task<StrategyPositionSnapshot?> GetStrategyPositionAsync(StrategyPositionId id, CancellationToken token = default)
    {
        if (!id.IsValid) throw new ArgumentException("Valid position identity is required.", nameof(id));
        return _dbFactory.TradeDb.Use("TradeFlow.Position.Current.Get", "SELECT payload FROM strategy_position_current_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=?;")
            .SetParameters(new TradeDbValues([id.Trade.PortfolioId, id.Trade.FundId, id.Trade.OrderId, id.Trade.TradeId, id.PositionId]))
            .ExecuteSingleAsync(row => MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(row.GetBytes(0)), token);
    }

    public async Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, string actor, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Actor name is required.", nameof(actor));
        foreach (var leg in position.Legs)
        {
            var route = new MarketPositionRoute(position.Id.Trade.PortfolioId, position.Id.Trade.FundId,
                position.Id.Trade.OrderId, position.Id.Trade.TradeId, position.Id.PositionId, leg.TradeLegId,
                position.StrategyKind, actor, position.Id.Format(), position.RouteGeneration);
            if (position.IsOpen)
            {
                await _dbFactory.TradeDb.Use("TradeFlow.Route.Upsert", "INSERT INTO open_position_route_v1 (marketInstrumentId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,strategyKind,positionActor,positionActorThreadId,generation) VALUES (?,?,?,?,?,?,?,?,?,?,?);")
                    .SetParameters(new TradeDbValues([(long)leg.MarketInstrumentId, route.PortfolioId, route.FundId, route.OrderId,
                        route.TradeId, route.StrategyPositionId, route.TradeLegId, route.StrategyKind.ToString(), route.PositionActor,
                        route.PositionActorThreadId, route.Generation])).ExecuteCommandAsync(token).ConfigureAwait(false);
                await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Upsert", "INSERT INTO open_position_route_recovery_v1 (shard,positionId,tradeLegId,marketInstrumentId,portfolioId,fundId,orderId,tradeId,strategyKind,positionActor,positionActorThreadId,generation) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
                    .SetParameters(new TradeDbValues([(sbyte)0, route.StrategyPositionId, route.TradeLegId, (long)leg.MarketInstrumentId,
                        route.PortfolioId, route.FundId, route.OrderId, route.TradeId, route.StrategyKind.ToString(), route.PositionActor,
                        route.PositionActorThreadId, route.Generation])).ExecuteCommandAsync(token).ConfigureAwait(false);
            }
            else
            {
                await _dbFactory.TradeDb.Use("TradeFlow.Route.Delete", "DELETE FROM open_position_route_v1 WHERE marketInstrumentId=? AND positionId=? AND tradeLegId=?;")
                    .SetParameters(new TradeDbValues([(long)leg.MarketInstrumentId, route.StrategyPositionId, route.TradeLegId]))
                    .ExecuteCommandAsync(token).ConfigureAwait(false);
                await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Delete", "DELETE FROM open_position_route_recovery_v1 WHERE shard=? AND positionId=? AND tradeLegId=?;")
                    .SetParameters(new TradeDbValues([(sbyte)0, route.StrategyPositionId, route.TradeLegId]))
                    .ExecuteCommandAsync(token).ConfigureAwait(false);
            }
        }
    }

    public async Task<ICollection<(uint InstrumentId, MarketPositionRoute Route)>> GetOpenPositionRoutesAsync(uint marketInstrumentId, CancellationToken token = default)
    {
        if (marketInstrumentId == 0) throw new ArgumentOutOfRangeException(nameof(marketInstrumentId));
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Get", "SELECT portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,strategyKind,positionActor,positionActorThreadId,generation FROM open_position_route_v1 WHERE marketInstrumentId=?;")
            .SetParameters(new TradeDbValues([(long)marketInstrumentId]))
            .ExecuteQueryAsync(row => (marketInstrumentId, new MarketPositionRoute(row.GetInt(0), row.GetInt(1), row.GetInt(2), row.GetInt(3),
                row.GetGuid(4), row.GetGuid(5), row.GetEnum<TradeStrategyKind>(6), row.GetString(7), row.GetString(8), row.GetLong(9))), token)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<ICollection<(uint InstrumentId, MarketPositionRoute Route)>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default)
    {
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Get", "SELECT marketInstrumentId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,strategyKind,positionActor,positionActorThreadId,generation FROM open_position_route_recovery_v1 WHERE shard=?;")
            .SetParameters(new TradeDbValues([(sbyte)0]))
            .ExecuteQueryAsync(row => ((uint)row.GetLong(0), new MarketPositionRoute(row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetInt(4),
                row.GetGuid(5), row.GetGuid(6), row.GetEnum<TradeStrategyKind>(7), row.GetString(8), row.GetString(9), row.GetLong(10))), token)
            .ConfigureAwait(false);
        return result;
    }

    static void Require(TradeEntityId id)
    {
        if (!id.IsValid) throw new ArgumentException("Valid Portfolio, Fund, Order, and Trade identity is required.", nameof(id));
    }

    static void Require(TradeOrderId id)
    {
        if (!id.IsValid) throw new ArgumentException("Valid Portfolio, Fund, and Order identity is required.", nameof(id));
    }

    readonly record struct TradeDbValues(object?[] Values) : IBindValue
    {
        public object Bind() => Values;
    }
}
