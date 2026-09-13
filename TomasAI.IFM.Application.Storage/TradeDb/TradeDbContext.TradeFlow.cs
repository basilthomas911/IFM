using TomasAI.IFM.Domain.Trade.Shared;
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
    Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default);
    Task<ICollection<(string ContractId, PortfolioFundTradeLeg Route)>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default);
    Task<ICollection<(string ContractId, PortfolioFundTradeLeg Route)>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default);
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
            await _dbFactory.TradeDb.Use("TradeFlow.Fill.Upsert.V2", "INSERT INTO order_execution_fill_v2 (executionAttemptId,executionFillId,componentId,tradeLegId,contractId,filledAtUtc,payload) VALUES (?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([fill.ExecutionAttemptId, fill.ExecutionFillId, fill.ComponentId, fill.TradeLegId,
                    fill.ContractId, fill.FilledAtUtc, MessagePackBinarySerializer.Shared.Serialize(fill)]))
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

    public async Task ReplaceOpenPositionRoutesAsync(StrategyPositionSnapshot position, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        var existing = await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Position.Get.V2",
                "SELECT contractId,tradeLegId FROM open_position_route_recovery_v2 WHERE shard=? AND positionId=?;")
            .SetParameters(new TradeDbValues([(sbyte)0, position.Id.PositionId]))
            .ExecuteQueryAsync(row => (ContractId: row.GetString(0), TradeLegId: row.GetGuid(1)), token)
            .ConfigureAwait(false);
        foreach (var old in existing)
        {
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Delete.V2",
                    "DELETE FROM open_position_route_v2 WHERE contractId=? AND positionId=? AND tradeLegId=?;")
                .SetParameters(new TradeDbValues([old.ContractId, position.Id.PositionId, old.TradeLegId]))
                .ExecuteCommandAsync(token).ConfigureAwait(false);
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Delete.V2",
                    "DELETE FROM open_position_route_recovery_v2 WHERE shard=? AND positionId=? AND tradeLegId=?;")
                .SetParameters(new TradeDbValues([(sbyte)0, position.Id.PositionId, old.TradeLegId]))
                .ExecuteCommandAsync(token).ConfigureAwait(false);
        }
        if (!position.IsOpen) return;

        foreach (var leg in position.Legs)
        {
            if (string.IsNullOrWhiteSpace(leg.ContractId))
                throw new ArgumentException("Every routed position leg requires ContractId.", nameof(position));
            var route = new PortfolioFundTradeLeg(position.Id.Trade.PortfolioId, position.Id.Trade.FundId,
                position.Id.Trade.OrderId, position.Id.Trade.TradeId, position.Id.PositionId, leg.TradeLegId,
                position.StrategyKind, position.RouteGeneration);
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Upsert.V2", "INSERT INTO open_position_route_v2 (contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation) VALUES (?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([leg.ContractId, route.PortfolioId, route.FundId, route.OrderId,
                    route.TradeId, route.StrategyPositionId, route.TradeLegId, route.TradeType.ToString(), route.Generation]))
                .ExecuteCommandAsync(token).ConfigureAwait(false);
            await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Upsert.V2", "INSERT INTO open_position_route_recovery_v2 (shard,positionId,tradeLegId,contractId,portfolioId,fundId,orderId,tradeId,tradeType,generation) VALUES (?,?,?,?,?,?,?,?,?,?);")
                .SetParameters(new TradeDbValues([(sbyte)0, route.StrategyPositionId, route.TradeLegId, leg.ContractId,
                    route.PortfolioId, route.FundId, route.OrderId, route.TradeId, route.TradeType.ToString(), route.Generation]))
                .ExecuteCommandAsync(token).ConfigureAwait(false);
        }
    }

    public async Task<ICollection<(string ContractId, PortfolioFundTradeLeg Route)>> GetOpenPositionRoutesAsync(string contractId, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(contractId)) throw new ArgumentException("ContractId is required.", nameof(contractId));
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Get.V2", "SELECT portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route_v2 WHERE contractId=?;")
            .SetParameters(new TradeDbValues([contractId]))
            .ExecuteQueryAsync(row => (contractId, new PortfolioFundTradeLeg(row.GetInt(0), row.GetInt(1), row.GetInt(2), row.GetInt(3),
                row.GetGuid(4), row.GetGuid(5), row.GetEnum<TradeStrategyKind>(6), row.GetLong(7))), token)
            .ConfigureAwait(false);
        return result;
    }

    public async Task<ICollection<(string ContractId, PortfolioFundTradeLeg Route)>> GetOpenPositionRouteSnapshotAsync(CancellationToken token = default)
    {
        var result = await _dbFactory.TradeDb.Use("TradeFlow.Route.Recovery.Get.V2", "SELECT contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation FROM open_position_route_recovery_v2 WHERE shard=?;")
            .SetParameters(new TradeDbValues([(sbyte)0]))
            .ExecuteQueryAsync(row => (row.GetString(0), new PortfolioFundTradeLeg(row.GetInt(1), row.GetInt(2), row.GetInt(3), row.GetInt(4),
                row.GetGuid(5), row.GetGuid(6), row.GetEnum<TradeStrategyKind>(7), row.GetLong(8))), token)
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
