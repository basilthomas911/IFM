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
    readonly Dictionary<string, decimal> _pendingCosts = new(StringComparer.Ordinal);

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
            PositionType = order.PositionType,
            TargetPositionId = order.TargetPositionId,
            Order = order,
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
        if (Current.Status is OrderExecutionStatus.Rejected or OrderExecutionStatus.Filled)
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

        var existing = _fills.FirstOrDefault(value => value.ExecutionFillId == fill.ExecutionFillId ||
            !string.IsNullOrWhiteSpace(fill.ExternalExecutionId) && value.ExternalExecutionId == fill.ExternalExecutionId);
        if (existing is not null)
            return existing == fill
                ? TradeDecision<OrderExecutionDefinition>.Accept(Current)
                : Reject("OE.FILL_ID_CONFLICT", "A fill identity was reused with different evidence.");

        var allocatedQuantity = _fills
            .Where(value => value.ComponentId == fill.ComponentId && value.TradeLegId == fill.TradeLegId)
            .Sum(static value => value.SignedQuantity);
        if (Math.Abs(allocatedQuantity + fill.SignedQuantity) > Math.Abs(leg.SignedQuantity))
            return Reject("OE.FILL_OVER_ALLOCATED", "Fill quantity exceeds the approved component leg quantity.");

        if (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId) &&
            _pendingCosts.Remove(fill.ExternalExecutionId, out var pendingCommission))
            fill = fill with { Commission = pendingCommission };
        _fillIds.Add(fill.ExecutionFillId);
        if (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId)) _externalIds.Add(fill.ExternalExecutionId);
        _fills.Add(fill);
        Current = Current with
        {
            Status = OrderExecutionStatus.PartiallyFilled,
            Fills = [.. _fills],
            PendingFillCosts = PendingCosts()
        };
        return TradeDecision<OrderExecutionDefinition>.Accept(Current);
    }

    public TradeDecision<OrderExecutionDefinition> UpdateFillCost(string externalExecutionId, decimal commission)
    {
        if (Current is null) return Missing();
        if (string.IsNullOrWhiteSpace(externalExecutionId) || commission < 0)
            return Reject("OE.INVALID_FILL_COST", "External execution identity and non-negative commission are required.");
        var index = _fills.FindIndex(value => value.ExternalExecutionId == externalExecutionId);
        if (index < 0)
        {
            if (_pendingCosts.TryGetValue(externalExecutionId, out var pending) && pending == commission)
                return TradeDecision<OrderExecutionDefinition>.Accept(Current);
            _pendingCosts[externalExecutionId] = commission;
            Current = Current with { PendingFillCosts = PendingCosts() };
            return TradeDecision<OrderExecutionDefinition>.Accept(Current);
        }
        if (_fills[index].Commission == commission) return TradeDecision<OrderExecutionDefinition>.Accept(Current);
        _fills[index] = _fills[index] with { Commission = commission };
        Current = Current with { Fills = [.. _fills], PendingFillCosts = PendingCosts() };
        return TradeDecision<OrderExecutionDefinition>.Accept(Current);
    }

    public TradeDecision<OrderExecutionAcceptance> Accept(DateTime completedAtUtc)
    {
        if (Current is null)
            return TradeDecision<OrderExecutionAcceptance>.Reject("OE.NOT_FOUND", "Execution does not exist.");
        if (completedAtUtc.Kind != DateTimeKind.Utc)
            return TradeDecision<OrderExecutionAcceptance>.Reject("OE.INVALID_TIME", "Completion time must be UTC.");
        if (Current.Status == OrderExecutionStatus.Filled)
            return TradeDecision<OrderExecutionAcceptance>.Reject("OE.ALREADY_ACCEPTED", "Execution was already accepted.");

        List<EstablishedTradeDefinition> trades = [];
        foreach (var component in Current.Components)
        {
            var componentFills = _fills.Where(value => value.ComponentId == component.ComponentId).ToArray();
            if (!TryResolveAcceptedScale(component, componentFills, out _))
                return TradeDecision<OrderExecutionAcceptance>.Reject(
                    "OE.UNBALANCED_EXPOSURE", $"Component {component.ComponentId} is not completely filled or an allowed balanced partial fill.");

            if (Current.PositionType == TradeOrderPositionType.Closing)
                continue;

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

        PositionCloseExecution[] closedPositions = [];
        if (Current.PositionType == TradeOrderPositionType.Closing)
        {
            if (Current.TargetPositionId is not { IsValid: true } target)
                return TradeDecision<OrderExecutionAcceptance>.Reject(
                    "OE.CLOSE_TARGET_REQUIRED", "A closing execution requires its existing strategy position identity.");
            var strategyKinds = Current.Components.Select(static component => component.StrategyKind).Distinct().ToArray();
            if (strategyKinds.Length != 1)
                return TradeDecision<OrderExecutionAcceptance>.Reject(
                    "OE.CLOSE_STRATEGY_AMBIGUOUS", "A closing order must contain exactly one strategy kind.");
            closedPositions =
            [
                new PositionCloseExecution
                {
                    PositionId = target,
                    StrategyKind = strategyKinds[0],
                    ExecutionAttemptId = Current.ExecutionAttemptId,
                    Fills = [.. _fills],
                    CompletedAtUtc = completedAtUtc
                }
            ];
        }

        Current = Current with { Status = OrderExecutionStatus.Filled, CompletedAtUtc = completedAtUtc, Fills = [.. _fills] };
        return TradeDecision<OrderExecutionAcceptance>.Accept(new([.. trades], closedPositions));
    }

    public TradeDecision<OrderExecutionDefinition> Cancel() =>
        Move([OrderExecutionStatus.Pending, OrderExecutionStatus.Submitted, OrderExecutionStatus.PartiallyFilled], OrderExecutionStatus.Cancelled, "cancel");

    public TradeDecision<OrderExecutionDefinition> RejectExecution() =>
        Move([OrderExecutionStatus.Pending, OrderExecutionStatus.Submitted], OrderExecutionStatus.Rejected, "reject");

    public void Replay(OrderExecutionDefinition state)
    {
        Current = state;
        _fills.Clear(); _fillIds.Clear(); _externalIds.Clear(); _pendingCosts.Clear();
        foreach (var fill in state.Fills)
        {
            _fills.Add(fill); _fillIds.Add(fill.ExecutionFillId);
            if (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId)) _externalIds.Add(fill.ExternalExecutionId);
        }
        foreach (var cost in state.PendingFillCosts)
            _pendingCosts[cost.ExternalExecutionId] = cost.Commission;
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

    PendingExecutionCostEvidence[] PendingCosts() =>
        [.. _pendingCosts.OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => new PendingExecutionCostEvidence
            {
                ExternalExecutionId = item.Key,
                Commission = item.Value
            })];
}

public sealed record OrderExecutionAcceptance(
    EstablishedTradeDefinition[] CreatedTrades,
    PositionCloseExecution[] ClosedPositions);
