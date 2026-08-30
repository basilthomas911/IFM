using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Workflow;

/// <summary>Fail-closed lifecycle for planned composition identities. It has no broker or live-position capability.</summary>
public sealed class PortfolioFundCompositionAggregate
{
    readonly Dictionary<Guid, FundCompositionReservationResult> _reservations = [];
    readonly Dictionary<int, FundOrderProjectionReadModel> _orders = [];
    readonly Dictionary<int, FundOrderTradeProjectionReadModel[]> _trades = [];

    public IReadOnlyCollection<FundOrderProjectionReadModel> Orders => _orders.Values;

    public IReadOnlyList<FundCompositionReservationResult> CaptureState() =>
        _orders.Values.OrderBy(x => x.OrderId).Select(order => new FundCompositionReservationResult
        {
            Order = order,
            Trades = [.. _trades[order.OrderId].OrderBy(x => x.LegOrdinal)],
            AggregateVersion = order.AggregateVersion,
            CommittedOnUtc = order.CreatedOnUtc,
            Disposition = ReservationDisposition.Committed,
            CanonicalRequestSha256 = order.CanonicalRequestHash,
        }).ToArray();

    public FundCompositionReservationResult ReservationForOrder(int orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order)) throw new KeyNotFoundException($"FundOrder {orderId} was not found.");
        return new FundCompositionReservationResult
        {
            Order = order,
            Trades = [.. _trades[orderId].OrderBy(x => x.LegOrdinal)],
            AggregateVersion = order.AggregateVersion,
            CommittedOnUtc = order.CreatedOnUtc,
            Disposition = ReservationDisposition.Committed,
            CanonicalRequestSha256 = order.CanonicalRequestHash,
        };
    }

    public void Restore(IEnumerable<FundCompositionReservationResult> reservations)
    {
        if (_orders.Count != 0) throw new InvalidOperationException("Composition state can only restore into an empty aggregate.");
        foreach (var reservation in reservations.OrderBy(x => x.Order.OrderId)) ApplyReservation(reservation);
    }

    public void ApplyReservation(FundCompositionReservationResult reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (reservation.Order.OrderId <= 0 || reservation.Order.IdempotencyKey == Guid.Empty ||
            reservation.Trades.Length == 0 || reservation.Trades.Any(x => x.OrderId != reservation.Order.OrderId))
            throw new InvalidOperationException("Committed composition reservation is invalid.");
        if (_orders.ContainsKey(reservation.Order.OrderId) || _reservations.ContainsKey(reservation.Order.IdempotencyKey))
            throw new InvalidOperationException("Committed composition reservation is duplicated.");
        var committed = reservation with { Disposition = ReservationDisposition.Committed };
        _orders.Add(committed.Order.OrderId, committed.Order);
        _trades.Add(committed.Order.OrderId, [.. committed.Trades.OrderBy(x => x.LegOrdinal)]);
        _reservations.Add(committed.Order.IdempotencyKey, committed);
    }

    public void ApplyOrder(FundOrderProjectionReadModel order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!_orders.TryGetValue(order.OrderId, out var current))
            throw new InvalidOperationException("Composition state event precedes its reservation.");
        if (order.AggregateVersion != current.AggregateVersion + 1)
            throw new InvalidOperationException("Composition aggregate version is not contiguous.");
        _orders[order.OrderId] = order;
        var prior = _reservations[current.IdempotencyKey];
        _reservations[current.IdempotencyKey] = prior with { Order = order, AggregateVersion = order.AggregateVersion };
    }

    public bool TryGetReservation(Guid idempotencyKey, out FundCompositionReservationResult result) =>
        _reservations.TryGetValue(idempotencyKey, out result!);

    public FundCompositionReservationResult Reserve(
        ReserveFundOrderCompositionRequest request,
        PortfolioFundStrategySnapshot snapshot,
        int orderId,
        IReadOnlyList<int> tradeIds,
        DateTime committedOnUtc,
        string principal)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        ValidateUtc(committedOnUtc, nameof(committedOnUtc));
        var requestHash = PortfolioCanonicalHash.Compute(request.DefensiveCopy());
        if (_reservations.TryGetValue(request.IdempotencyKey, out var prior))
        {
            if (!string.Equals(prior.CanonicalRequestSha256, requestHash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different canonical request.");
            return prior with { Disposition = ReservationDisposition.IdempotentReplay };
        }

        ValidateReservation(request, snapshot, committedOnUtc);
        if (orderId <= 0 || tradeIds.Count != request.TradeInstructions.Length || tradeIds.Any(x => x <= 0) || tradeIds.Distinct().Count() != tradeIds.Count)
            throw new ArgumentException("One unique positive TradeId is required per TradeInstruction.", nameof(tradeIds));
        if (_orders.ContainsKey(orderId)) throw new InvalidOperationException("OrderId is already reserved.");

        const long aggregateVersion = 1;
        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = request.PortfolioId,
            FundId = request.FundId,
            OrderId = orderId,
            WorkflowId = request.WorkflowId,
            WorkflowRevision = request.WorkflowRevision,
            Status = FundCompositionState.TemplateSelected.ToString(),
            CreatedOnUtc = committedOnUtc,
            CreatedBy = principal.Trim(),
            TradeSelectionResultId = request.TradeSelectionResultId,
            TradeSelectionResultHash = request.TradeSelectionResultSha256,
            TradeTemplateId = request.TradeTemplateId,
            TradeTemplateVersion = request.TradeTemplateVersion,
            OrderCompositionProfileId = request.OrderCompositionProfileId,
            OrderCompositionProfileVersion = request.OrderCompositionProfileVersion,
            StrategySnapshotHash = snapshot.PayloadSha256,
            ExpiresAtUtc = request.ExpiresAtUtc,
            AggregateVersion = aggregateVersion,
            IdempotencyKey = request.IdempotencyKey,
            CanonicalRequestHash = requestHash,
        };
        var trades = request.TradeInstructions.Select((instruction, index) => new FundOrderTradeProjectionReadModel
        {
            PortfolioId = request.PortfolioId,
            FundId = request.FundId,
            OrderId = orderId,
            TradeId = tradeIds[index],
            TradeFamily = instruction.TradeFamily,
            InstructionReference = instruction.Reference,
            LegOrdinal = index + 1,
            AggregateVersion = aggregateVersion,
            DirectionOrBias = instruction.DirectionOrBias,
            TradeAction = instruction.TradeAction,
            UnderlyingRoot = instruction.UnderlyingRoot,
            RequestedTradeDate = instruction.RequestedTradeDate,
            RequestedMaturityDate = instruction.RequestedMaturityDate,
        }).ToArray();
        var result = new FundCompositionReservationResult
        {
            Order = order,
            Trades = trades,
            AggregateVersion = aggregateVersion,
            CommittedOnUtc = committedOnUtc,
            Disposition = ReservationDisposition.Committed,
            CanonicalRequestSha256 = requestHash,
        };
        _orders.Add(orderId, order);
        _trades.Add(orderId, trades);
        _reservations.Add(request.IdempotencyKey, result);
        return result;
    }

    public FundOrderProjectionReadModel MarkComposing(int orderId, long expectedVersion) =>
        Transition(orderId, expectedVersion, FundCompositionState.Composing, [FundCompositionState.TemplateSelected]);

    public FundOrderProjectionReadModel RecordComposed(
        int orderId,
        long expectedVersion,
        OrderCompositionResultReference result,
        DateTime acceptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateUtc(acceptedAtUtc, nameof(acceptedAtUtc));
        var current = RequireOrder(orderId, expectedVersion);
        if (current.Status == FundCompositionState.RiskPending.ToString())
        {
            if (current.CompositionResultId == result.ResultId && current.CompositionResultHash == result.ResultSha256) return current;
            throw new InvalidOperationException("A different composition result is already terminally recorded.");
        }
        if (current.Status != FundCompositionState.Composing.ToString()) throw InvalidTransition(current.Status, FundCompositionState.RiskPending);
        ValidateResult(result.ResultId, result.ResultSha256, result.EvaluatedAtUtc, result.ExpiresAtUtc, acceptedAtUtc);
        return Save(current with
        {
            Status = FundCompositionState.RiskPending.ToString(),
            CompositionResultId = result.ResultId,
            CompositionResultHash = result.ResultSha256,
            AggregateVersion = current.AggregateVersion + 1,
        });
    }

    public FundOrderProjectionReadModel RecordRiskOutcome(
        int orderId,
        long expectedVersion,
        RiskManagementResultReference result,
        DateTime acceptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(result);
        ValidateUtc(acceptedAtUtc, nameof(acceptedAtUtc));
        var current = RequireOrder(orderId, expectedVersion);
        var desired = result.Decision switch
        {
            RiskDecision.Approved => FundCompositionState.RiskApproved,
            RiskDecision.Rejected => FundCompositionState.RiskRejected,
            _ => throw new ArgumentException("A Risk decision is required.", nameof(result)),
        };
        if (current.Status is nameof(FundCompositionState.RiskApproved) or nameof(FundCompositionState.RiskRejected))
        {
            if (current.RiskResultId == result.ResultId && current.RiskResultHash == result.ResultSha256 && current.Status == desired.ToString()) return current;
            throw new InvalidOperationException("A different Risk result is already terminally recorded.");
        }
        if (current.Status != FundCompositionState.RiskPending.ToString()) throw InvalidTransition(current.Status, desired);
        ValidateResult(result.ResultId, result.ResultSha256, result.EvaluatedAtUtc, result.ExpiresAtUtc, acceptedAtUtc);
        if (result.EnvelopeId == Guid.Empty || result.EnvelopeVersion <= 0) throw new ArgumentException("A versioned risk envelope reference is required.", nameof(result));
        if (!string.Equals(result.CandidateSha256, current.CompositionResultHash, StringComparison.Ordinal)) throw new InvalidOperationException("Risk candidate hash does not match the accepted composition.");
        return Save(current with
        {
            Status = desired.ToString(),
            RiskResultId = result.ResultId,
            RiskResultHash = result.ResultSha256,
            AggregateVersion = current.AggregateVersion + 1,
        });
    }

    public FundOrderProjectionReadModel FailComposition(int orderId, long expectedVersion, string reason) =>
        Stop(orderId, expectedVersion, FundCompositionState.CompositionFailed, reason, [FundCompositionState.Composing]);

    public FundOrderProjectionReadModel Cancel(int orderId, long expectedVersion, string reason) =>
        Stop(orderId, expectedVersion, FundCompositionState.Cancelled, reason,
            [FundCompositionState.Draft, FundCompositionState.IdentityReserved, FundCompositionState.TemplateSelected, FundCompositionState.Composing, FundCompositionState.Composed, FundCompositionState.RiskPending]);

    public FundOrderProjectionReadModel Expire(int orderId, long expectedVersion, string reason) =>
        Stop(orderId, expectedVersion, FundCompositionState.Expired, reason,
            [FundCompositionState.IdentityReserved, FundCompositionState.TemplateSelected, FundCompositionState.Composing, FundCompositionState.Composed, FundCompositionState.RiskPending]);

    FundOrderProjectionReadModel Stop(int orderId, long version, FundCompositionState desired, string reason, FundCompositionState[] allowed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var updated = Transition(orderId, version, desired, allowed);
        return Save(updated with { StopReason = reason.Trim() });
    }

    FundOrderProjectionReadModel Transition(int orderId, long expectedVersion, FundCompositionState desired, FundCompositionState[] allowed)
    {
        if (desired is FundCompositionState.ExecutionRequested or FundCompositionState.Executing or FundCompositionState.Executed or FundCompositionState.ExecutionFailed)
            throw new InvalidOperationException("Execution states are outside the Portfolio implementation boundary.");
        var current = RequireOrder(orderId, expectedVersion);
        if (!allowed.Any(x => current.Status == x.ToString())) throw InvalidTransition(current.Status, desired);
        return Save(current with { Status = desired.ToString(), AggregateVersion = current.AggregateVersion + 1 });
    }

    FundOrderProjectionReadModel RequireOrder(int orderId, long expectedVersion)
    {
        if (!_orders.TryGetValue(orderId, out var current)) throw new KeyNotFoundException($"FundOrder {orderId} was not found.");
        if (current.AggregateVersion != expectedVersion) throw new InvalidOperationException($"Expected version {expectedVersion}, current version is {current.AggregateVersion}.");
        return current;
    }

    FundOrderProjectionReadModel Save(FundOrderProjectionReadModel order) => _orders[order.OrderId] = order;

    static void ValidateReservation(ReserveFundOrderCompositionRequest request, PortfolioFundStrategySnapshot snapshot, DateTime nowUtc)
    {
        if (request.IdempotencyKey == Guid.Empty || request.WorkflowId == Guid.Empty || request.TradeSelectionInvocationId == Guid.Empty || request.TradeSelectionResultId == Guid.Empty)
            throw new ArgumentException("Reservation identities are required.", nameof(request));
        if (request.PortfolioId <= 0 || request.PortfolioVersion <= 0 || request.FundId <= 0 || request.FundMandateVersion <= 0)
            throw new ArgumentException("Positive Portfolio/Fund identities and versions are required.", nameof(request));
        if (request.TradeTemplateId == Guid.Empty || request.TradeTemplateVersion <= 0 || request.OrderCompositionProfileId == Guid.Empty || request.OrderCompositionProfileVersion <= 0)
            throw new ArgumentException("Versioned template and composition-profile references are required.", nameof(request));
        if (request.Origin == CompositionOrigin.Unknown || request.TradeInstructions.Length is < 1 or > 16 || request.TradeInstructions.Count(x => x.IsPrimaryTrade) != 1)
            throw new ArgumentException("One to sixteen instructions with exactly one primary instruction are required.", nameof(request));
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        ValidateUtc(request.ExpiresAtUtc, nameof(request.ExpiresAtUtc));
        if (request.RequestedAtUtc > nowUtc || nowUtc >= request.ExpiresAtUtc) throw new InvalidOperationException("Reservation request is not current.");
        if (snapshot.WorkflowId != request.WorkflowId || snapshot.WorkflowRevision != request.WorkflowRevision
            || snapshot.Portfolio.PortfolioId != request.PortfolioId || snapshot.Portfolio.PortfolioVersion != request.PortfolioVersion
            || snapshot.Fund.FundId != request.FundId || snapshot.Fund.FundMandateVersion != request.FundMandateVersion)
            throw new InvalidOperationException("Reservation does not match the frozen Portfolio/Fund snapshot.");
        if (nowUtc >= snapshot.ValidUntilUtc || !string.Equals(snapshot.PayloadSha256, request.PortfolioFundStrategySnapshotSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Frozen Portfolio/Fund snapshot is expired or its hash does not match.");
        if (!snapshot.Assignments.Any(x => x.TradeTemplateId == request.TradeTemplateId && x.TradeTemplateVersion == request.TradeTemplateVersion
                                           && x.OrderCompositionProfileId == request.OrderCompositionProfileId && x.OrderCompositionProfileVersion == request.OrderCompositionProfileVersion))
            throw new InvalidOperationException("Selected template/profile is not present in the frozen snapshot.");
        if (string.IsNullOrWhiteSpace(request.TradeSelectionResultSha256) || request.TradeSelectionResultSha256.Length != 64)
            throw new ArgumentException("A SHA-256 TradeSelection result hash is required.", nameof(request));
    }

    static void ValidateResult(Guid id, string hash, DateTime evaluatedAtUtc, DateTime expiresAtUtc, DateTime acceptedAtUtc)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(hash) || hash.Length != 64) throw new ArgumentException("A result identity and SHA-256 are required.");
        ValidateUtc(evaluatedAtUtc, nameof(evaluatedAtUtc));
        ValidateUtc(expiresAtUtc, nameof(expiresAtUtc));
        if (evaluatedAtUtc > acceptedAtUtc || acceptedAtUtc >= expiresAtUtc) throw new InvalidOperationException("Result is stale, future-dated, or expired.");
    }

    static void ValidateUtc(DateTime value, string name)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Value must be UTC.", name);
    }

    static InvalidOperationException InvalidTransition(string current, FundCompositionState desired) =>
        new($"FundOrder transition {current} -> {desired} is not allowed.");
}
