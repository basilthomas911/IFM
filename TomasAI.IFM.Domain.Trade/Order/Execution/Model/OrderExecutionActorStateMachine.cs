using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Order.Model;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Model;

/// <summary>Pure broker-neutral execution model used by the OrderExecution command actor.</summary>
public sealed class OrderExecutionActorStateMachine
{
    readonly List<ExecutionFillEvidence> _fills = [];
    readonly HashSet<Guid> _fillIds = [];
    readonly HashSet<string> _externalIds = new(StringComparer.Ordinal);

    public OrderExecutionDefinition? Current { get; private set; }

    public TradeDecision<OrderExecutionDefinition> Start(
        TradeOrderDefinition order,
        Guid executionAttemptId,
        ExecutionChannel channel,
        DateTime startedAtUtc)
    {
        if (Current is not null) return Reject("OE.ALREADY_EXISTS", "Execution already exists.");
        if (order.Status != TradeOrderStatus.Executing)
            return Reject("OE.ORDER_NOT_BOUND", "Trade Order must be bound to execution.");
        if (executionAttemptId == Guid.Empty || startedAtUtc.Kind != DateTimeKind.Utc)
            return Reject("OE.INVALID_IDENTITY", "ExecutionAttemptId and UTC start time are required.");
        Current = new OrderExecutionDefinition
        {
            TradeOrderId = order.Id,
            ExecutionAttemptId = executionAttemptId,
            Channel = channel,
            Status = OrderExecutionStatus.Pending,
            OrderRevision = order.Revision,
            Components = order.Components,
            StartedAtUtc = startedAtUtc
        };
        return TradeDecision<OrderExecutionDefinition>.Accept(Current);
    }

    public TradeDecision<OrderExecutionDefinition> MarkSubmitted() =>
        Move([OrderExecutionStatus.Pending], OrderExecutionStatus.Submitted, "submit");

    public TradeDecision<OrderExecutionDefinition> AddFill(ExecutionFillEvidence fill)
    {
        if (Current is null) return Missing();
        if (Current.Status is OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected or OrderExecutionStatus.Filled)
            return Reject("OE.TERMINAL", $"Cannot add a fill to {Current.Status} execution.");
        if (fill.ExecutionFillId == Guid.Empty || fill.ExecutionAttemptId != Current.ExecutionAttemptId ||
            fill.TradeLegId == Guid.Empty || fill.SignedQuantity == 0 || fill.Price <= 0 ||
            fill.FilledAtUtc.Kind != DateTimeKind.Utc)
            return Reject("OE.INVALID_FILL", "Fill identity, quantity, positive price, attempt, and UTC time are required.");

        var component = Current.Components.SingleOrDefault(value => value.ComponentId == fill.ComponentId);
        var leg = component?.Legs.SingleOrDefault(value => value.TradeLegId == fill.TradeLegId);
        if (leg is null || !string.Equals(leg.ContractId, fill.ContractId, StringComparison.Ordinal) ||
            Math.Sign(leg.SignedQuantity) != Math.Sign(fill.SignedQuantity))
            return Reject("OE.FILL_NOT_ALLOCATABLE", "Fill does not match a proposed component leg.");

        if (_fillIds.Contains(fill.ExecutionFillId) ||
            (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId) && _externalIds.Contains(fill.ExternalExecutionId)))
            return TradeDecision<OrderExecutionDefinition>.Accept(Current);

        _fillIds.Add(fill.ExecutionFillId);
        if (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId)) _externalIds.Add(fill.ExternalExecutionId);
        _fills.Add(fill);
        Current = Current with { Status = OrderExecutionStatus.PartiallyFilled, Fills = [.. _fills] };
        return TradeDecision<OrderExecutionDefinition>.Accept(Current);
    }

    public TradeDecision<EstablishedTradeDefinition[]> Accept(DateTime completedAtUtc)
    {
        if (Current is null)
            return TradeDecision<EstablishedTradeDefinition[]>.Reject("OE.NOT_FOUND", "Execution does not exist.");
        if (completedAtUtc.Kind != DateTimeKind.Utc)
            return TradeDecision<EstablishedTradeDefinition[]>.Reject("OE.INVALID_TIME", "Completion time must be UTC.");
        if (Current.Status == OrderExecutionStatus.Filled)
            return TradeDecision<EstablishedTradeDefinition[]>.Reject("OE.ALREADY_ACCEPTED", "Execution was already accepted.");

        List<EstablishedTradeDefinition> trades = [];
        foreach (var component in Current.Components)
        {
            var componentFills = _fills.Where(value => value.ComponentId == component.ComponentId).ToArray();
            if (!TryResolveAcceptedScale(component, componentFills, out _))
                return TradeDecision<EstablishedTradeDefinition[]>.Reject(
                    "OE.UNBALANCED_EXPOSURE", $"Component {component.ComponentId} is not completely filled or an allowed balanced partial fill.");

            var assetFamily = component.StrategyKind == TradeStrategyKind.FuturesOutright
                ? TradeAssetFamily.Futures
                : TradeAssetFamily.FuturesOption;
            var id = new TradeEntityId(
                Current.TradeOrderId.PortfolioId,
                Current.TradeOrderId.FundId,
                Current.TradeOrderId.OrderId,
                component.ReservedTradeId);
            trades.Add(new EstablishedTradeDefinition
            {
                Id = id,
                AssetFamily = assetFamily,
                StrategyKind = component.StrategyKind,
                SourceComponentId = component.ComponentId,
                ExecutionAttemptId = Current.ExecutionAttemptId,
                Status = EstablishedTradeStatus.Open,
                Legs = component.Legs,
                OriginalFills = componentFills,
                OpeningValue = componentFills.Sum(static value => value.Price * value.SignedQuantity),
                OpeningCommission = componentFills.Sum(static value => value.Commission),
                EstablishedAtUtc = completedAtUtc,
                EvidenceRevision = 1
            });
        }

        Current = Current with { Status = OrderExecutionStatus.Filled, CompletedAtUtc = completedAtUtc, Fills = [.. _fills] };
        return TradeDecision<EstablishedTradeDefinition[]>.Accept([.. trades]);
    }

    public TradeDecision<OrderExecutionDefinition> Cancel() =>
        Move([OrderExecutionStatus.Pending, OrderExecutionStatus.Submitted, OrderExecutionStatus.PartiallyFilled], OrderExecutionStatus.Cancelled, "cancel");

    public TradeDecision<OrderExecutionDefinition> RejectExecution() =>
        Move([OrderExecutionStatus.Pending, OrderExecutionStatus.Submitted], OrderExecutionStatus.Rejected, "reject");

    public void Replay(OrderExecutionDefinition state)
    {
        Current = state;
        _fills.Clear(); _fillIds.Clear(); _externalIds.Clear();
        foreach (var fill in state.Fills)
        {
            _fills.Add(fill); _fillIds.Add(fill.ExecutionFillId);
            if (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId)) _externalIds.Add(fill.ExternalExecutionId);
        }
    }

    static bool TryResolveAcceptedScale(
        TradeOrderComponentDefinition component,
        ExecutionFillEvidence[] fills,
        out decimal scale)
    {
        scale = 0;
        decimal? common = null;
        foreach (var leg in component.Legs)
        {
            var actual = fills.Where(value => value.TradeLegId == leg.TradeLegId).Sum(static value => value.SignedQuantity);
            if (actual == 0 || Math.Sign(actual) != Math.Sign(leg.SignedQuantity)) return false;
            var legScale = (decimal)actual / leg.SignedQuantity;
            if (legScale <= 0 || legScale > 1) return false;
            if (common.HasValue && common.Value != legScale) return false;
            common = legScale;
        }
        scale = common ?? 0;
        return scale == 1 || component.PermitBalancedPartialAcceptance;
    }

    TradeDecision<OrderExecutionDefinition> Move(
        OrderExecutionStatus[] expected,
        OrderExecutionStatus next,
        string operation)
    {
        if (Current is null) return Missing();
        if (!expected.Contains(Current.Status)) return Reject("OE.INVALID_TRANSITION", $"Cannot {operation} an execution in {Current.Status} state.");
        Current = Current with { Status = next, Fills = [.. _fills] };
        return TradeDecision<OrderExecutionDefinition>.Accept(Current);
    }

    static TradeDecision<OrderExecutionDefinition> Reject(string code, string detail) =>
        TradeDecision<OrderExecutionDefinition>.Reject(code, detail);
    static TradeDecision<OrderExecutionDefinition> Missing() => Reject("OE.NOT_FOUND", "Execution does not exist.");
}
