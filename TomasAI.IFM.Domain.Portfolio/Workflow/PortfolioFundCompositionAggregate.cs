using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

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
            (reservation.Trades.Length == 0 && (reservation.Order.Origin != CompositionOrigin.ManualUi || reservation.Order.Status != FundCompositionState.Draft.ToString())) ||
            reservation.Trades.Any(x => x.OrderId != reservation.Order.OrderId))
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


    /// <summary>Replays a committed manual mutation containing the complete canonical order state.</summary>
    /// <param name="reservation">The committed order and trade collection.</param>
    public void ApplyManualChange(FundCompositionReservationResult reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        var current = ReservationForOrder(reservation.Order.OrderId);
        if (reservation.Order.AggregateVersion != current.Order.AggregateVersion + 1)
            throw new InvalidOperationException("Manual order aggregate version is not contiguous.");
        if (reservation.Order.IdempotencyKey != current.Order.IdempotencyKey)
            throw new InvalidOperationException("Manual order idempotency identity cannot change.");
        SaveReservation(reservation.Order, [.. reservation.Trades.OrderBy(x => x.LegOrdinal)]);
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
            Origin = request.Origin,
            UnderlyingRoot = request.UnderlyingRoot.Trim(),
            RequestedTradeDate = request.RequestedTradeDate,
            RequestedMaturityDate = request.RequestedMaturityDate,
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

    /// <summary>Adds an operator-authored trade to an open manual Portfolio Fund order.</summary>
    /// <param name="request">The fully scoped trade request.</param>
    /// <param name="principal">The authenticated operator principal.</param>
    /// <returns>The updated canonical order and trade collection.</returns>
    public FundCompositionReservationResult AddManualTrade(AddManualFundOrderTradeRequest request, string principal)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        var order = RequireManualOrder(request.PortfolioId, request.FundId, request.OrderId, request.ExpectedOrderVersion);
        return AddManualTradeCore(order, request, principal.Trim());
    }
    /// <summary>Removes an economically inactive trade from an open manual Portfolio Fund order.</summary>
    /// <param name="request">The scoped trade-removal request.</param>
    /// <returns>The updated canonical order composition.</returns>
    public FundCompositionReservationResult RemoveManualTrade(ManualFundOrderTradeMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        var order = RequireManualOrder(
            request.PortfolioId, request.FundId, request.OrderId, request.ExpectedOrderVersion);
        var trades = _trades[order.OrderId];
        var trade = trades.SingleOrDefault(x => x.TradeId == request.TradeId)
            ?? throw new KeyNotFoundException($"FundOrderTrade {request.TradeId} was not found.");
        if (trades.Length >= 2 ||
            !Enum.TryParse<TradeState>(trade.TradeState, out var state) ||
            state != TradeState.NewTrade ||
            trades.Any(x => x.TradeState is nameof(TradeState.TradeToClose) or nameof(TradeState.OrderCompleted)))
            throw new InvalidOperationException("The trade has economic evidence or participates in a closing pair.");

        var nextVersion = checked(order.AggregateVersion + 1);
        return SaveReservation(
            order with { AggregateVersion = nextVersion },
            [.. trades.Where(x => x.TradeId != request.TradeId)]);
    }

    /// <summary>Deletes an empty draft manual Portfolio Fund order.</summary>
    /// <param name="request">The scoped order deletion request.</param>
    public void DeleteManualOrder(ManualFundOrderMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        var order = RequireManualOrder(
            request.PortfolioId, request.FundId, request.OrderId, request.ExpectedOrderVersion);
        if (_trades[order.OrderId].Length != 0)
            throw new InvalidOperationException("Only an empty manual order can be deleted.");
        _orders.Remove(order.OrderId);
        _trades.Remove(order.OrderId);
        _reservations.Remove(order.IdempotencyKey);
    }

    /// <summary>Replays deletion of a committed manual Portfolio Fund order.</summary>
    /// <param name="orderId">The deleted order identifier.</param>
    public void ApplyManualDeletion(int orderId)
    {
        if (!_orders.TryGetValue(orderId, out var order))
            throw new InvalidOperationException("Manual order deletion precedes its reservation.");
        _orders.Remove(orderId);
        _trades.Remove(orderId);
        _reservations.Remove(order.IdempotencyKey);
    }
    /// <summary>Changes the lifecycle state of a trade on an open manual Portfolio Fund order.</summary>
    /// <param name="request">The scoped trade-state mutation request.</param>
    /// <returns>The updated canonical order composition.</returns>
    public FundCompositionReservationResult ChangeManualTradeState(ManualFundOrderTradeMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        if (!Enum.TryParse<TradeState>(request.TradeState, out var state))
            throw new ArgumentException("A valid trade state is required.", nameof(request));
        var order = RequireManualOrder(
            request.PortfolioId, request.FundId, request.OrderId, request.ExpectedOrderVersion);
        var trades = _trades[order.OrderId];
        if (trades.All(x => x.TradeId != request.TradeId))
            throw new KeyNotFoundException($"FundOrderTrade {request.TradeId} was not found.");

        var nextVersion = checked(order.AggregateVersion + 1);
        var changed = trades.Select(x => x.TradeId == request.TradeId
            ? x with { TradeState = state.ToString(), AggregateVersion = nextVersion }
            : x with { AggregateVersion = nextVersion }).ToArray();
        return SaveReservation(order with { AggregateVersion = nextVersion }, changed);
    }

    /// <summary>Closes a manual Portfolio Fund order after its compatible closing trade completes.</summary>
    /// <param name="request">The scoped order-close request.</param>
    /// <returns>The closed canonical order composition.</returns>
    public FundCompositionReservationResult CloseManualOrder(ManualFundOrderMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        var order = RequireManualOrder(
            request.PortfolioId, request.FundId, request.OrderId, request.ExpectedOrderVersion);
        var trades = _trades[order.OrderId];
        if (trades.Length != 2 || trades.Count(x => x.PrimaryTrade) != 1 ||
            !trades.Any(x => !x.PrimaryTrade && x.TradeState == nameof(TradeState.OrderCompleted)))
            throw new InvalidOperationException("The order requires one completed compatible closing trade.");

        var nextVersion = checked(order.AggregateVersion + 1);
        return SaveReservation(
            order with
            {
                Status = nameof(FundCompositionState.Executed),
                AggregateVersion = nextVersion,
                StopReason = request.Reason.Trim(),
            },
            [.. trades.Select(x => x with { AggregateVersion = nextVersion })]);
    }


    public FundCompositionReservationResult CreateManualDraft(
        CreateManualFundOrderRequest request, int orderId, DateTime committedOnUtc, string principal)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
        ValidateUtc(committedOnUtc, nameof(committedOnUtc));
        var requestHash = PortfolioCanonicalHash.Compute(request);
        if (_reservations.TryGetValue(request.IdempotencyKey, out var prior))
        {
            if (!string.Equals(prior.CanonicalRequestSha256, requestHash, StringComparison.Ordinal))
                throw new InvalidOperationException("IdempotencyKeyConflict: the key was already committed for a different manual draft.");
            return prior with { Disposition = ReservationDisposition.IdempotentReplay };
        }
        if (request.IdempotencyKey == Guid.Empty || request.PortfolioId <= 0 || request.PortfolioVersion <= 0 ||
            request.FundId <= 0 || request.FundMandateVersion <= 0 || string.IsNullOrWhiteSpace(request.UnderlyingRoot) || orderId <= 0)
            throw new ArgumentException("A manual draft requires positive Portfolio/Fund identities and a non-empty underlying root.", nameof(request));
        ValidateUtc(request.RequestedAtUtc, nameof(request.RequestedAtUtc));
        ValidateUtc(request.ExpiresAtUtc, nameof(request.ExpiresAtUtc));
        if (request.RequestedAtUtc > committedOnUtc || committedOnUtc >= request.ExpiresAtUtc ||
            request.RequestedMaturityDate < request.RequestedTradeDate)
            throw new InvalidOperationException("The manual draft request is stale or has an invalid date range.");
        if (_orders.ContainsKey(orderId)) throw new InvalidOperationException("OrderId is already reserved.");

        var order = new FundOrderProjectionReadModel
        {
            PortfolioId = request.PortfolioId,
            FundId = request.FundId,
            OrderId = orderId,
            WorkflowId = request.IdempotencyKey,
            WorkflowRevision = 1,
            Status = FundCompositionState.Draft.ToString(),
            CreatedOnUtc = committedOnUtc,
            CreatedBy = principal.Trim(),
            ExpiresAtUtc = request.ExpiresAtUtc,
            AggregateVersion = 1,
            IdempotencyKey = request.IdempotencyKey,
            CanonicalRequestHash = requestHash,
            Origin = CompositionOrigin.ManualUi,
            OperatorReference = request.Reference.Trim(),
            UnderlyingRoot = request.UnderlyingRoot.Trim(),
            RequestedTradeDate = request.RequestedTradeDate,
            RequestedMaturityDate = request.RequestedMaturityDate,
        };
        var result = new FundCompositionReservationResult
        {
            Order = order,
            Trades = [],
            AggregateVersion = 1,
            CommittedOnUtc = committedOnUtc,
            Disposition = ReservationDisposition.Committed,
            CanonicalRequestSha256 = requestHash,
        };
        _orders.Add(orderId, order);
        _trades.Add(orderId, []);
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

    /// <summary>Accepts exact reserved units for an existing composed order; PostgreSQL separately verifies the committed grant.</summary>
    public FundOrderProjectionReadModel AuthorizeRisk(int orderId, long expectedVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.FundRiskAuthorizationReference authorization, DateTime now)
    {
        authorization.Validate();
        ValidateUtc(now, nameof(now));
        var current = RequireOrder(orderId, expectedVersion);
        if (authorization.PortfolioId != current.PortfolioId || authorization.FundId != current.FundId ||
            authorization.OrderId != current.OrderId || authorization.WorkflowId != current.WorkflowId ||
            authorization.CompositionResultHash != current.CompositionResultHash || now >= authorization.ValidUntilUtc ||
            now >= current.ExpiresAtUtc || authorization.ValidUntilUtc > current.ExpiresAtUtc)
            throw new InvalidOperationException("Financial authorization does not match the current composed Fund order.");
        if (current.RiskAuthorization == authorization) return current;
        if (current.Status != nameof(FundCompositionState.RiskPending) || current.RiskAuthorization is not null)
            throw new InvalidOperationException("Only a RiskPending Fund order can accept new financial authorization.");
        return Save(current with
        {
            Status = nameof(FundCompositionState.RiskApproved), RiskResultId = authorization.RiskResultId,
            RiskResultHash = authorization.RiskAssessmentHash, RiskAuthorization = authorization,
            AggregateVersion = checked(current.AggregateVersion + 1)
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

    public FundOrderProjectionReadModel SynchronizeRisk(long version, TomasAI.IFM.Domain.Portfolio.Shared.Financial.RiskTerminalEvidence evidence)
    {
        var current = RequireOrder(evidence.OrderId, version);
        if (current.TerminalRisk == evidence) return current;
        if (current.RiskAuthorization is not null || current.TerminalRisk is not null || current.Status != nameof(FundCompositionState.RiskPending)
            || current.PortfolioId != evidence.PortfolioId || current.FundId != evidence.FundId || current.WorkflowId != evidence.WorkflowId
            || current.CompositionResultHash != evidence.CompositionHash || evidence.SourceCommandId == Guid.Empty || evidence.SourceEventId == Guid.Empty
            || evidence.TargetStatus is not ("RiskRejected" or "Cancelled" or "Expired") || evidence.DecidedAtUtc.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Terminal Risk outcome conflicts with the current Fund order.");
        return Save(current with { Status = evidence.TargetStatus, AggregateVersion = current.AggregateVersion + 1,
            TerminalRisk = evidence, StopReason = evidence.Reason, RiskResultId = evidence.RiskResultId, RiskResultHash = evidence.RiskResultHash });
    }

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

    FundOrderProjectionReadModel RequireManualOrder(int portfolioId, int fundId, int orderId, long version)
    {
        var order = RequireOrder(orderId, version);
        if (order.PortfolioId != portfolioId) throw new InvalidOperationException("Portfolio ownership does not match.");
        if (order.FundId != fundId) throw new InvalidOperationException("Fund ownership does not match.");
        if (order.Origin != CompositionOrigin.ManualUi) throw new InvalidOperationException("Automated orders are read-only.");
        if (order.Status != nameof(FundCompositionState.Draft)) throw new InvalidOperationException("Only an open manual order can be changed.");
        return order;
    }

    FundCompositionReservationResult AddManualTradeCore(
        FundOrderProjectionReadModel order, AddManualFundOrderTradeRequest request, string principal)
    {
        ValidateManualTrade(request);
        var trades = _trades[order.OrderId];
        ValidateManualTradeAddition(trades, request);
        var nextVersion = checked(order.AggregateVersion + 1);
        var trade = CreateManualTrade(request, principal, trades.Length + 1, nextVersion);
        return SaveReservation(order with { AggregateVersion = nextVersion }, [.. trades, trade]);
    }


    FundCompositionReservationResult SaveReservation(
        FundOrderProjectionReadModel order, FundOrderTradeProjectionReadModel[] trades)
    {
        _orders[order.OrderId] = order;
        _trades[order.OrderId] = trades;
        var updated = _reservations[order.IdempotencyKey] with
        {
            Order = order, Trades = trades, AggregateVersion = order.AggregateVersion,
        };
        _reservations[order.IdempotencyKey] = updated;
        return updated;
    }

    static void ValidateManualTrade(AddManualFundOrderTradeRequest request)
    {
        if (!Enum.TryParse<TradeType>(request.TradeType, out var type) || type == TradeType.Unknown)
            throw new ArgumentException("A valid trade type is required.", nameof(request));
        if (!Enum.TryParse<TradeState>(request.TradeState, out _))
            throw new ArgumentException("A valid trade state is required.", nameof(request));
        if (!Enum.TryParse<TradeAction>(request.TradeAction, out _))
            throw new ArgumentException("A valid trade action is required.", nameof(request));
        if (request.TradeId <= 0 || request.TradeDate == default || request.MaturityDate < request.TradeDate)
            throw new ArgumentException("A valid trade identity and date range are required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Reference) || string.IsNullOrWhiteSpace(request.BaseContractSymbol))
            throw new ArgumentException("A trade reference and base symbol are required.", nameof(request));
    }

    static void ValidateManualTradeAddition(
        FundOrderTradeProjectionReadModel[] trades, AddManualFundOrderTradeRequest request)
    {
        if (trades.Length >= 2 || trades.Any(x => x.TradeId == request.TradeId))
            throw new InvalidOperationException("The maximum trade count was reached or the TradeId is duplicated.");
        if (trades.Length == 0 && !request.PrimaryTrade)
            throw new InvalidOperationException("The first manual trade must be the primary opening trade.");
        if (trades.Length == 1 && !IsCompatibleClosingTrade(trades.Single(), request))
            throw new InvalidOperationException("The closing trade is incompatible with the primary opening trade.");
    }

    static bool IsCompatibleClosingTrade(
        FundOrderTradeProjectionReadModel opening, AddManualFundOrderTradeRequest request) =>
        opening.PrimaryTrade &&
        opening.TradeState == nameof(TradeState.TradeToOpen) &&
        !request.PrimaryTrade &&
        request.TradeType == ClosingType(opening.TradeType) &&
        string.Equals(request.BaseContractSymbol, opening.BaseContractSymbol, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(request.Reference, opening.InstructionReference, StringComparison.Ordinal);

    static string ClosingType(string openingType) => Enum.TryParse<TradeType>(openingType, out var value)
        ? value switch
        {
            TradeType.ShortIronCondor => nameof(TradeType.LongIronCondor),
            TradeType.LongIronCondor => nameof(TradeType.ShortIronCondor),
            TradeType.PutCreditSpread => nameof(TradeType.PutDebitSpread),
            TradeType.PutDebitSpread => nameof(TradeType.PutCreditSpread),
            TradeType.CallCreditSpread => nameof(TradeType.CallDebitSpread),
            TradeType.CallDebitSpread => nameof(TradeType.CallCreditSpread),
            TradeType.FuturesOutright => nameof(TradeType.FuturesOutright),
            _ => string.Empty,
        }
        : string.Empty;

    static FundOrderTradeProjectionReadModel CreateManualTrade(
        AddManualFundOrderTradeRequest request, string principal, int ordinal, long version) => new()
    {
        PortfolioId = request.PortfolioId,
        FundId = request.FundId,
        OrderId = request.OrderId,
        TradeId = request.TradeId,
        TradeFamily = request.TradeType,
        TradeType = request.TradeType,
        InstructionReference = request.Reference.Trim(),
        LegOrdinal = ordinal,
        AggregateVersion = version,
        TradeAction = request.TradeAction,
        UnderlyingRoot = request.BaseContractSymbol.Trim(),
        RequestedTradeDate = request.TradeDate,
        RequestedMaturityDate = request.MaturityDate,
        TradeState = request.TradeState,
        PrimaryTrade = request.PrimaryTrade,
        BaseContractSymbol = request.BaseContractSymbol.Trim(),
        CreatedOnUtc = request.RequestedAtUtc,
        CreatedBy = principal,
    };
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
