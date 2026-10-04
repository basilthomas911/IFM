using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Model;

/// <summary>The proposed execution definition, establishment evidence, or business rejection.</summary>
internal sealed record OrderExecutionCompute(bool Accepted, OrderExecutionDefinition? OrderExecutionDefinition,
    string RejectionCode, string RejectionReason, EstablishedTradeDefinition[] CreatedTrades,
    PositionCloseExecution[] ClosedPositions)
{
    /// <summary>Checks execution ownership and required business identity before event creation.</summary>
    public bool IsValidFor(OrderExecutionId executionId) => Accepted && OrderExecutionDefinition is { } execution &&
        executionId.IsValid && execution.Id == executionId && execution.TradeOrderId.IsValid;

    /// <summary>Accepts computed business data and optional establishment evidence.</summary>
    public static OrderExecutionCompute Accept(OrderExecutionDefinition execution,
        EstablishedTradeDefinition[]? createdTrades = null, PositionCloseExecution[]? closedPositions = null)
        => new(true, execution, string.Empty, string.Empty, createdTrades ?? [], closedPositions ?? []);

    /// <summary>Preserves the business rejection classification and detail.</summary>
    public static OrderExecutionCompute Reject(string rejectionCode, string rejectionReason)
        => new(false, null, rejectionCode, rejectionReason, [], []);
}

/// <summary>Stateless execution decisions; only State.Apply owns authoritative mutations.</summary>
internal static class OrderExecutionComputation
{
    /// <summary>Computes Start business data without modifying the input definition.</summary>
    internal static OrderExecutionCompute Start(OrderExecutionDefinition? orderExecutionDefinition, TradeOrderDefinition order, Guid executionAttemptId, ExecutionChannel channel, DateTime startedAtUtc)
    {
        if (orderExecutionDefinition is not null) return OrderExecutionCompute.Reject("OrderExecution.ALREADY_EXISTS", "Execution already exists.");
        if (order.Status != TradeOrderStatus.Executing)
            return OrderExecutionCompute.Reject("OrderExecution.ORDER_NOT_BOUND", "Trade Order must be bound to execution.");
        if (executionAttemptId == Guid.Empty || startedAtUtc.Kind != DateTimeKind.Utc)
            return OrderExecutionCompute.Reject("OrderExecution.INVALID_IDENTITY", "ExecutionAttemptId and UTC start time are required.");
        orderExecutionDefinition = new OrderExecutionDefinition
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
            OrderQuantity = order.Components.Sum(static component =>
                component.Legs.Select(static leg => Math.Abs(leg.SignedQuantity)).DefaultIfEmpty().Max()),
            StartedAtUtc = startedAtUtc
        };
        return OrderExecutionCompute.Accept(orderExecutionDefinition!);
    }

    /// <summary>Computes AddFill business data without modifying the input definition.</summary>
    internal static OrderExecutionCompute AddFill(OrderExecutionDefinition? orderExecutionDefinition, ExecutionFillEvidence fill)
    {
        var executionFills = orderExecutionDefinition?.Fills.ToList() ?? [];
        var pendingFillCosts = orderExecutionDefinition?.PendingFillCosts.ToDictionary(cost => cost.ExternalExecutionId, cost => cost.Commission, StringComparer.Ordinal) ?? new Dictionary<string, decimal>(StringComparer.Ordinal);

        if (orderExecutionDefinition is null) return OrderExecutionCompute.Reject("OrderExecution.NOT_FOUND", "Execution does not exist.");
        if (orderExecutionDefinition.Status is OrderExecutionStatus.Rejected or OrderExecutionStatus.Filled)
            return OrderExecutionCompute.Reject("OrderExecution.TERMINAL", $"Cannot add a fill to {orderExecutionDefinition.Status} execution.");
        if (fill.ExecutionFillId == Guid.Empty || fill.ExecutionAttemptId != orderExecutionDefinition.ExecutionAttemptId ||
            fill.TradeLegId == Guid.Empty || fill.SignedQuantity == 0 || fill.Price <= 0 ||
            fill.FilledAtUtc.Kind != DateTimeKind.Utc)
            return OrderExecutionCompute.Reject("OrderExecution.INVALID_FILL", "Fill identity, quantity, positive price, attempt, and UTC time are required.");

        var component = orderExecutionDefinition.Components.SingleOrDefault(value => value.ComponentId == fill.ComponentId);
        var leg = component?.Legs.SingleOrDefault(value => value.TradeLegId == fill.TradeLegId);
        if (leg is null || !string.Equals(leg.ContractId, fill.ContractId, StringComparison.Ordinal) ||
            Math.Sign(leg.SignedQuantity) != Math.Sign(fill.SignedQuantity))
            return OrderExecutionCompute.Reject("OrderExecution.FILL_NOT_ALLOCATABLE", "Fill does not match a proposed component leg.");

        var existing = executionFills.FirstOrDefault(value => value.ExecutionFillId == fill.ExecutionFillId ||
            !string.IsNullOrWhiteSpace(fill.ExternalExecutionId) && value.ExternalExecutionId == fill.ExternalExecutionId);
        if (existing is not null)
            return existing == fill
                ? OrderExecutionCompute.Accept(orderExecutionDefinition!)
                : OrderExecutionCompute.Reject("OrderExecution.FILL_ID_CONFLICT", "A fill identity was reused with different evidence.");

        var allocatedQuantity = executionFills
            .Where(value => value.ComponentId == fill.ComponentId && value.TradeLegId == fill.TradeLegId)
            .Sum(static value => value.SignedQuantity);
        if (Math.Abs(allocatedQuantity + fill.SignedQuantity) > Math.Abs(leg.SignedQuantity))
            return OrderExecutionCompute.Reject("OrderExecution.FILL_OVER_ALLOCATED", "Fill quantity exceeds the approved component leg quantity.");

        if (!string.IsNullOrWhiteSpace(fill.ExternalExecutionId) &&
            pendingFillCosts.Remove(fill.ExternalExecutionId, out var pendingCommission))
            fill = fill with { Commission = pendingCommission };
        executionFills.Add(fill);
        orderExecutionDefinition = orderExecutionDefinition with
        {
            Status = OrderExecutionStatus.PartiallyFilled,
            Fills = [.. executionFills],
            CumulativeFilledQuantity = FilledStrategyQuantity(orderExecutionDefinition.Components, executionFills),
            PendingFillCosts = CreatePendingCosts(pendingFillCosts)
        };
        return OrderExecutionCompute.Accept(orderExecutionDefinition!);
    }

    /// <summary>Computes UpdateFillCost business data without modifying the input definition.</summary>
    internal static OrderExecutionCompute UpdateFillCost(OrderExecutionDefinition? orderExecutionDefinition, string externalExecutionId, decimal commission)
    {
        var executionFills = orderExecutionDefinition?.Fills.ToList() ?? [];
        var pendingFillCosts = orderExecutionDefinition?.PendingFillCosts.ToDictionary(cost => cost.ExternalExecutionId, cost => cost.Commission, StringComparer.Ordinal) ?? new Dictionary<string, decimal>(StringComparer.Ordinal);

        if (orderExecutionDefinition is null) return OrderExecutionCompute.Reject("OrderExecution.NOT_FOUND", "Execution does not exist.");
        if (string.IsNullOrWhiteSpace(externalExecutionId) || commission < 0)
            return OrderExecutionCompute.Reject("OrderExecution.INVALID_FILL_COST", "External execution identity and non-negative commission are required.");
        var index = executionFills.FindIndex(value => value.ExternalExecutionId == externalExecutionId);
        if (index < 0)
        {
            if (pendingFillCosts.TryGetValue(externalExecutionId, out var pending) && pending == commission)
                return OrderExecutionCompute.Accept(orderExecutionDefinition!);
            pendingFillCosts[externalExecutionId] = commission;
            orderExecutionDefinition = orderExecutionDefinition with { PendingFillCosts = CreatePendingCosts(pendingFillCosts) };
            return OrderExecutionCompute.Accept(orderExecutionDefinition!);
        }
        if (executionFills[index].Commission == commission) return OrderExecutionCompute.Accept(orderExecutionDefinition!);
        executionFills[index] = executionFills[index] with { Commission = commission };
        orderExecutionDefinition = orderExecutionDefinition with { Fills = [.. executionFills], PendingFillCosts = CreatePendingCosts(pendingFillCosts) };
        return OrderExecutionCompute.Accept(orderExecutionDefinition!);
    }

    /// <summary>Computes Accept business data without modifying the input definition.</summary>
    internal static OrderExecutionCompute Accept(OrderExecutionDefinition? orderExecutionDefinition, DateTime completedAtUtc)
    {
        var executionFills = orderExecutionDefinition?.Fills.ToList() ?? [];

        if (orderExecutionDefinition is null)
            return OrderExecutionCompute.Reject("OrderExecution.NOT_FOUND", "Execution does not exist.");
        if (completedAtUtc.Kind != DateTimeKind.Utc)
            return OrderExecutionCompute.Reject("OrderExecution.INVALID_TIME", "Completion time must be UTC.");
        if (orderExecutionDefinition.Status == OrderExecutionStatus.Filled)
            return OrderExecutionCompute.Reject("OrderExecution.ALREADY_ACCEPTED", "Execution was already accepted.");

        List<EstablishedTradeDefinition> trades = [];
        foreach (var component in orderExecutionDefinition.Components)
        {
            var componentFills = executionFills.Where(value => value.ComponentId == component.ComponentId).ToArray();
            if (!TryResolveAcceptedScale(component, componentFills, out _))
                return OrderExecutionCompute.Reject(
                    "OrderExecution.UNBALANCED_EXPOSURE", $"Component {component.ComponentId} is not completely filled or an allowed balanced partial fill.");

            if (orderExecutionDefinition.PositionType == TradeOrderPositionType.Closing)
                continue;

            var assetFamily = component.StrategyKind == TradeStrategyKind.FuturesOutright
                ? TradeAssetFamily.Futures
                : TradeAssetFamily.FuturesOption;
            var id = new TradeEntityId(
                orderExecutionDefinition.TradeOrderId.PortfolioId,
                orderExecutionDefinition.TradeOrderId.FundId,
                orderExecutionDefinition.TradeOrderId.OrderId,
                component.ReservedTradeId);
            trades.Add(new EstablishedTradeDefinition
            {
                Id = id,
                AssetFamily = assetFamily,
                StrategyKind = component.StrategyKind,
                SourceComponentId = component.ComponentId,
                ExecutionAttemptId = orderExecutionDefinition.ExecutionAttemptId,
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
        if (orderExecutionDefinition.PositionType == TradeOrderPositionType.Closing)
        {
            if (orderExecutionDefinition.TargetPositionId is not { IsValid: true } target)
                return OrderExecutionCompute.Reject(
                    "OrderExecution.CLOSE_TARGET_REQUIRED", "A closing execution requires its existing strategy position identity.");
            var strategyKinds = orderExecutionDefinition.Components.Select(static component => component.StrategyKind).Distinct().ToArray();
            if (strategyKinds.Length != 1)
                return OrderExecutionCompute.Reject(
                    "OrderExecution.CLOSE_STRATEGY_AMBIGUOUS", "A closing order must contain exactly one strategy kind.");
            closedPositions =
            [
                new PositionCloseExecution
                {
                    PositionId = target,
                    StrategyKind = strategyKinds[0],
                    ExecutionAttemptId = orderExecutionDefinition.ExecutionAttemptId,
                    Fills = [.. executionFills],
                    CompletedAtUtc = completedAtUtc
                }
            ];
        }

        orderExecutionDefinition = orderExecutionDefinition with { Status = OrderExecutionStatus.Filled, CompletedAtUtc = completedAtUtc, Fills = [.. executionFills] };
        return OrderExecutionCompute.Accept(orderExecutionDefinition, [.. trades], closedPositions);
        }
    /// <summary>Checks complete or permitted balanced component fill ratios.</summary>
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

    /// <summary>Counts fully evidenced strategy units across components.</summary>
    static int FilledStrategyQuantity(
        IEnumerable<TradeOrderComponentDefinition> components,
        IEnumerable<ExecutionFillEvidence> fills)
    {
        var evidence = fills.ToArray();
        return components.Sum(component => component.Legs
            .Select(leg => Math.Abs(evidence
                .Where(fill => fill.ComponentId == component.ComponentId && fill.TradeLegId == leg.TradeLegId)
                .Sum(static fill => fill.SignedQuantity)))
            .DefaultIfEmpty()
            .Min());
    }

    /// <summary>Computes a status transition permitted by the expected lifecycle states.</summary>
    internal static OrderExecutionCompute Move(OrderExecutionDefinition? execution,
        OrderExecutionStatus[] expected, OrderExecutionStatus status, string operation) => execution switch
    {
        null => OrderExecutionCompute.Reject("OrderExecution.NOT_FOUND", "Execution does not exist."),
        _ when !expected.Contains(execution.Status) => OrderExecutionCompute.Reject("OrderExecution.INVALID_TRANSITION", $"Cannot {operation} an execution in {execution.Status} state."),
        _ => OrderExecutionCompute.Accept(execution with { Status = status, Fills = [.. execution.Fills] })
    };

    /// <summary>Computes cancellation, establishing only balanced already-filled exposure.</summary>
    internal static OrderExecutionCompute Cancel(OrderExecutionDefinition? execution, DateTime completedAtUtc)
    {
        var cancellation = execution switch
        {
            null => OrderExecutionCompute.Reject("OrderExecution.NOT_FOUND", "Execution does not exist."),
            _ when completedAtUtc.Kind != DateTimeKind.Utc => OrderExecutionCompute.Reject("OrderExecution.INVALID_TIME", "Completion time must be UTC."),
            _ when execution.Status is not (OrderExecutionStatus.Pending or OrderExecutionStatus.Submitted or OrderExecutionStatus.PartiallyFilled)
                => OrderExecutionCompute.Reject("OrderExecution.INVALID_TRANSITION", $"Cannot cancel an execution in {execution.Status} state."),
            _ when execution.Fills.Length == 0 => OrderExecutionCompute.Accept(execution with { Status = OrderExecutionStatus.Cancelled, CompletedAtUtc = completedAtUtc }),
            _ => Accept(execution, completedAtUtc)
        };
        return cancellation.Accepted
            ? cancellation with { OrderExecutionDefinition = cancellation.OrderExecutionDefinition! with { Status = OrderExecutionStatus.Cancelled } }
            : cancellation;
    }

    /// <summary>Creates sorted commission evidence from a local computation dictionary.</summary>
    static PendingExecutionCostEvidence[] CreatePendingCosts(Dictionary<string, decimal> pendingFillCosts) =>
        [.. pendingFillCosts.OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => new PendingExecutionCostEvidence { ExternalExecutionId = item.Key, Commission = item.Value })];
}
