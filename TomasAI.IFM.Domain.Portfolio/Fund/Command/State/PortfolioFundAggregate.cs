using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

public readonly record struct FundActivationContext(
    bool ParentPortfolioIsActive,
    int EnabledCompatibleAssignmentCount,
    bool HasSelectionHintProfile,
    bool HasCompositionProfile)
{
    public bool IsValid => ParentPortfolioIsActive && EnabledCompatibleAssignmentCount > 0 &&
                           HasSelectionHintProfile && HasCompositionProfile;
}

/// <summary>Pure state machine for one Portfolio-owned Fund mandate.</summary>
public sealed class PortfolioFundAggregate
{
    readonly HashSet<Guid> _commandIds = [];
    readonly List<FundTradeTemplateAssignmentReadModel> _tradeTemplateAssignments = [];
    readonly PortfolioFundCompositionAggregate _fundOrderCompositions = new();

    /// <summary>Gets the authoritative FundMandate applied from source events.</summary>
    public FundMandateReadModel? FundMandate { get; private set; }
    /// <summary>Gets the existing read-only compatibility view of the business state.</summary>
    public FundMandateReadModel? Current => FundMandate;
    public long Revision { get; private set; }
    public bool Exists => FundMandate is not null;
    public IReadOnlyList<FundTradeTemplateAssignmentReadModel> Assignments => _tradeTemplateAssignments;
    public IReadOnlyCollection<FundOrderProjectionReadModel> Orders => _fundOrderCompositions.Orders;

    public IPortfolioFundDomainEvent Create(Guid commandId, FundMandateReadModel mandate, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeCreate(commandId, mandate, nowUtc, principal)));

    /// <summary>Computes Create without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeCreate(Guid commandId, FundMandateReadModel mandate, DateTime nowUtc, string principal) {
        ValidateCommand(commandId, nowUtc, principal);
        if (Exists) throw new InvalidOperationException("Fund mandate already exists.");
        if (mandate.FundMandateVersion != 1) throw new ArgumentException("A new Fund mandate must have version 1.", nameof(mandate));
        if (mandate.OperatingState != FundOperatingState.Draft) throw new ArgumentException("A new Fund mandate must begin in Draft.", nameof(mandate));
        ThrowIfInvalid(mandate.Validate());
        return new FundMandateCreatedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, mandate.DefensiveCopy());
    }

    public IPortfolioFundDomainEvent AddVersion(
        Guid commandId,
        long expectedRevision,
        FundMandateReadModel replacement,
        FundActivationContext activation,
        DateTime nowUtc,
        string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAddVersion(commandId, expectedRevision, replacement, activation, nowUtc, principal)));

    /// <summary>Computes AddVersion without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeAddVersion(
        Guid commandId,
        long expectedRevision,
        FundMandateReadModel replacement,
        FundActivationContext activation,
        DateTime nowUtc,
        string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (FundMandate!.OperatingState == FundOperatingState.Retired) throw new InvalidOperationException("A retired Fund cannot be versioned.");
        if (replacement.PortfolioId != FundMandate.PortfolioId || replacement.FundId != FundMandate.FundId)
            throw new ArgumentException("Portfolio/Fund parent identity cannot change.", nameof(replacement));
        if (replacement.FundCode != FundMandate.FundCode) throw new ArgumentException("FundCode cannot change.", nameof(replacement));
        var unassignedInactive = replacement.SchemaVersion >= 3 && replacement.OperatingState is FundOperatingState.Draft or FundOperatingState.Disabled or FundOperatingState.Retired
            && replacement.PermittedTradeFamilies is { Length: 0 } && replacement.PermittedTradeStrategyFamilies is { Length: 0 };
        if (FundMandate.PermittedTradeStrategyFamilies.Length > 0 && (replacement.SchemaVersion < 2 || (replacement.PermittedTradeStrategyFamilies.Length == 0 && !unassignedInactive)))
            throw new ArgumentException("An exact-reference Fund mandate cannot downgrade to legacy family names.", nameof(replacement));
        if (replacement.FundMandateVersion != FundMandate.FundMandateVersion + 1)
            throw new ArgumentException("FundMandateVersion must increment by one.", nameof(replacement));
        if (replacement.OperatingState != FundMandate.OperatingState &&
            !CanTransition(FundMandate.OperatingState, replacement.OperatingState, throughNewVersion: true))
            throw new InvalidOperationException($"Fund transition {FundMandate.OperatingState} -> {replacement.OperatingState} is not allowed.");
        if (replacement.OperatingState == FundOperatingState.Active && !activation.IsValid)
            throw new InvalidOperationException("Fund activation configuration is incomplete.");
        ThrowIfInvalid(replacement.Validate());
        return new FundMandateVersionAddedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, replacement.DefensiveCopy());
    }

    public IPortfolioFundDomainEvent ChangeState(
        Guid commandId,
        long expectedRevision,
        FundOperatingState state,
        string reason,
        FundActivationContext activation,
        DateTime nowUtc,
        string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeChangeState(commandId, expectedRevision, state, reason, activation, nowUtc, principal)));

    /// <summary>Computes ChangeState without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeChangeState(
        Guid commandId,
        long expectedRevision,
        FundOperatingState state,
        string reason,
        FundActivationContext activation,
        DateTime nowUtc,
        string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!CanTransition(FundMandate!.OperatingState, state, throughNewVersion: false))
            throw new InvalidOperationException($"Fund transition {FundMandate.OperatingState} -> {state} is not allowed.");
        if (state == FundOperatingState.Active && !activation.IsValid)
            throw new InvalidOperationException("Fund activation configuration is incomplete.");
        if (state == FundOperatingState.Active)
            ThrowIfInvalid((FundMandate with { OperatingState = state }).Validate());
        return new FundOperatingStateChangedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, state, reason.Trim());
    }

    public void Replay(IEnumerable<IPortfolioFundDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(x => x.Revision)) Apply(domainEvent, isReplay: true);
    }

    public PortfolioFundAggregateSnapshot CaptureSnapshot()
    {
        if (FundMandate is null) throw new InvalidOperationException("A missing Fund cannot be snapshotted.");
        return new PortfolioFundAggregateSnapshot(
            Revision,
            FundMandate.DefensiveCopy(),
            [.. _tradeTemplateAssignments.OrderBy(x => x.AssignmentVersion).Select(x => x.DefensiveCopy())],
            [.. _fundOrderCompositions.CaptureState()],
            [.. _commandIds.Order()]);
    }

    public void RestoreSnapshot(PortfolioFundAggregateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Exists || Revision != 0) throw new InvalidOperationException("A snapshot can only restore an empty Fund aggregate.");
        if (snapshot.Revision <= 0) throw new InvalidOperationException("Snapshot revision must be positive.");
        ThrowIfInvalid(snapshot.Current.Validate());
        FundMandate = snapshot.Current.DefensiveCopy();
        _tradeTemplateAssignments.AddRange(snapshot.Assignments.Select(x => x.DefensiveCopy()));
        _fundOrderCompositions.Restore(snapshot.Compositions);
        foreach (var commandId in snapshot.AppliedCommandIds)
            if (commandId == Guid.Empty || !_commandIds.Add(commandId)) throw new InvalidOperationException("Snapshot contains invalid command history.");
        Revision = snapshot.Revision;
    }

    public IPortfolioFundDomainEvent AssignTradeTemplate(
        Guid commandId,
        long expectedRevision,
        FundTradeTemplateAssignmentReadModel assignment,
        DateTime nowUtc,
        string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAssignTradeTemplate(commandId, expectedRevision, assignment, nowUtc, principal)));

    /// <summary>Computes AssignTradeTemplate without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeAssignTradeTemplate(
        Guid commandId,
        long expectedRevision,
        FundTradeTemplateAssignmentReadModel assignment,
        DateTime nowUtc,
        string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (FundMandate!.OperatingState == FundOperatingState.Retired) throw new InvalidOperationException("A retired Fund cannot receive assignments.");
        if (assignment.PortfolioId != FundMandate.PortfolioId || assignment.FundId != FundMandate.FundId)
            throw new ArgumentException("Assignment parent identity does not match the Fund.", nameof(assignment));
        if (assignment.FundMandateVersion != FundMandate.FundMandateVersion)
            throw new ArgumentException("Assignment mandate version is not current.", nameof(assignment));
        if (assignment.DecisionHorizon != FundMandate.DecisionHorizon)
            throw new ArgumentException("Assignment horizon is incompatible with the Fund mandate.", nameof(assignment));
        if (!assignment.UnderlyingUniverse.All(x => FundMandate.UnderlyingUniverse.Contains(x, StringComparer.Ordinal)))
            throw new ArgumentException("Assignment underlying is incompatible with the Fund mandate.", nameof(assignment));
        if (!FundMandate.EligibleAssetTypes.Contains(assignment.AssetType, StringComparer.Ordinal))
            throw new ArgumentException("Assignment asset type is incompatible with the Fund mandate.", nameof(assignment));
        if (FundMandate.PermittedTradeStrategyFamilies.Length > 0
            ? assignment.TradeStrategyFamily is null || !FundMandate.PermittedTradeStrategyFamilies.Contains(assignment.TradeStrategyFamily)
            : !FundMandate.PermittedTradeFamilies.Contains(assignment.TradeFamily, StringComparer.Ordinal))
            throw new ArgumentException("Assignment trade family is incompatible with the Fund mandate.", nameof(assignment));
        ThrowIfInvalid(assignment.Validate());
        var latestVersion = _tradeTemplateAssignments.Count == 0 ? 0 : _tradeTemplateAssignments.Max(x => x.AssignmentVersion);
        if (assignment.AssignmentVersion != (assignment.SchemaVersion >= 3 ? Revision + 1 : latestVersion + 1))
            throw new ArgumentException("AssignmentVersion must be the next Fund revision for schema v3, or the next assignment sequence for legacy data.", nameof(assignment));
        if (_tradeTemplateAssignments.Any(existing => (assignment.SchemaVersion < 3 || (existing.FundMandateVersion == assignment.FundMandateVersion && existing.Enabled && assignment.Enabled)) && existing.TradeTemplateId == assignment.TradeTemplateId &&
                                         WindowsOverlap(existing.EffectiveFromUtc, existing.EffectiveUntilUtc,
                                             assignment.EffectiveFromUtc, assignment.EffectiveUntilUtc)))
            throw new InvalidOperationException("The same TradeTemplate has an overlapping assignment window.");
        return new FundTradeTemplateAssignedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, assignment.DefensiveCopy());
    }

    public IReadOnlyList<FundTradeTemplateAssignmentReadModel> EffectiveAssignments(DateTime atUtc) =>
        _tradeTemplateAssignments.Where(x => x.IsEffectiveAt(atUtc)).OrderBy(x => x.Priority).ThenBy(x => x.TradeTemplateId).Select(x => x.DefensiveCopy()).ToArray();

    public IPortfolioFundDomainEvent ReserveComposition(
        Guid commandId, long expectedRevision, ReserveFundOrderCompositionRequest request,
        PortfolioFundStrategySnapshot snapshot, int orderId, IReadOnlyList<int> tradeIds,
        DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeReserveComposition(commandId, expectedRevision, request, snapshot, orderId, tradeIds, nowUtc, principal)));

    /// <summary>Computes ReserveComposition without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeReserveComposition(
        Guid commandId, long expectedRevision, ReserveFundOrderCompositionRequest request,
        PortfolioFundStrategySnapshot snapshot, int orderId, IReadOnlyList<int> tradeIds,
        DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (FundMandate!.OperatingState != FundOperatingState.Active)
            throw new InvalidOperationException("Only an active Fund can reserve a composition.");
        if (request.PortfolioId != FundMandate.PortfolioId || request.FundId != FundMandate.FundId ||
            request.FundMandateVersion != FundMandate.FundMandateVersion)
            throw new ArgumentException("Reservation parent identity/version does not match the Fund.", nameof(request));
        var reservation = CreateCompositionComputation().Reserve(request, snapshot, orderId, tradeIds, nowUtc, principal);
        if (reservation.Disposition == ReservationDisposition.IdempotentReplay)
            throw new InvalidOperationException("An idempotent reservation must be returned from committed-command lookup before aggregate mutation.");
        return new FundCompositionReservedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reservation);
    }

    public IPortfolioFundDomainEvent CreateManualOrder(
        Guid commandId, CreateManualFundOrderRequest request, int orderId, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeCreateManualOrder(commandId, request, orderId, nowUtc, principal)));

    /// <summary>Computes CreateManualOrder without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeCreateManualOrder(
        Guid commandId, CreateManualFundOrderRequest request, int orderId, DateTime nowUtc, string principal) {
        RequireCurrent(Revision);
        ValidateCommand(commandId, nowUtc, principal);
        if (FundMandate!.OperatingState != FundOperatingState.Active)
            throw new InvalidOperationException("Only an active Fund can create a manual order draft.");
        if (request.PortfolioId != FundMandate.PortfolioId || request.FundId != FundMandate.FundId ||
            request.FundMandateVersion != FundMandate.FundMandateVersion)
            throw new ArgumentException("Manual draft parent identity/version does not match the Fund.", nameof(request));
        var reservation = CreateCompositionComputation().CreateManualDraft(request, orderId, nowUtc, principal);
        if (reservation.Disposition == ReservationDisposition.IdempotentReplay)
            throw new InvalidOperationException("An idempotent manual draft must be returned from committed-command lookup.");
        return new FundCompositionReservedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reservation);
    }


    /// <summary>Adds a trade to an existing manual Portfolio Fund order.</summary>
    /// <param name="commandId">The stable command identifier.</param>
    /// <param name="request">The fully scoped trade request.</param>
    /// <param name="nowUtc">The authoritative UTC command time.</param>
    /// <param name="principal">The authenticated operator principal.</param>
    /// <returns>The event containing the complete updated canonical order state.</returns>
    public IPortfolioFundDomainEvent AddManualTrade(
        Guid commandId, AddManualFundOrderTradeRequest request, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAddManualTrade(commandId, request, nowUtc, principal)));

    /// <summary>Computes AddManualTrade without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeAddManualTrade(
        Guid commandId, AddManualFundOrderTradeRequest request, DateTime nowUtc, string principal) {
        RequireCurrent(Revision);
        ValidateCommand(commandId, nowUtc, principal);
        var reservation = CreateCompositionComputation().AddManualTrade(request, principal);
        return new FundManualOrderChangedCompute(
            Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reservation);
    }
    /// <summary>Removes an economically inactive trade from a manual Portfolio Fund order.</summary>
    /// <param name="commandId">The stable command identifier.</param>
    /// <param name="request">The scoped trade-removal request.</param>
    /// <param name="nowUtc">The authoritative UTC command time.</param>
    /// <param name="principal">The authenticated operator principal.</param>
    /// <returns>The event containing the complete updated canonical order state.</returns>
    public IPortfolioFundDomainEvent RemoveManualTrade(
        Guid commandId, ManualFundOrderTradeMutationRequest request, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeRemoveManualTrade(commandId, request, nowUtc, principal)));

    /// <summary>Computes RemoveManualTrade without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeRemoveManualTrade(
        Guid commandId, ManualFundOrderTradeMutationRequest request, DateTime nowUtc, string principal) =>
        ComputeManualCompositionChange(commandId, nowUtc, principal, () => CreateCompositionComputation().RemoveManualTrade(request), request.TradeId);

    /// <summary>Deletes an empty draft manual Portfolio Fund order.</summary>
    /// <param name="commandId">The stable command identifier.</param>
    /// <param name="request">The scoped order deletion request.</param>
    /// <param name="nowUtc">The authoritative UTC command time.</param>
    /// <param name="principal">The authenticated operator principal.</param>
    /// <returns>The committed deletion event.</returns>
    public IPortfolioFundDomainEvent DeleteManualOrder(
        Guid commandId, ManualFundOrderMutationRequest request, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeDeleteManualOrder(commandId, request, nowUtc, principal)));

    /// <summary>Computes DeleteManualOrder without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeDeleteManualOrder(
        Guid commandId, ManualFundOrderMutationRequest request, DateTime nowUtc, string principal) {
        RequireCurrent(Revision);
        ValidateCommand(commandId, nowUtc, principal);
        var removedTradeIds = CreateCompositionComputation().DeleteManualOrder(request);
        return new FundManualOrderDeletedCompute(
            Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, request.OrderId, removedTradeIds);
    }
    /// <summary>Changes a manual Portfolio Fund order trade lifecycle state.</summary>
    /// <param name="commandId">The stable command identifier.</param>
    /// <param name="request">The scoped trade-state mutation request.</param>
    /// <param name="nowUtc">The authoritative UTC command time.</param>
    /// <param name="principal">The authenticated operator principal.</param>
    /// <returns>The event containing the complete updated canonical order state.</returns>
    public IPortfolioFundDomainEvent ChangeManualTradeState(
        Guid commandId, ManualFundOrderTradeMutationRequest request, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeChangeManualTradeState(commandId, request, nowUtc, principal)));

    /// <summary>Computes ChangeManualTradeState without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeChangeManualTradeState(
        Guid commandId, ManualFundOrderTradeMutationRequest request, DateTime nowUtc, string principal) =>
        ComputeManualCompositionChange(commandId, nowUtc, principal, () => CreateCompositionComputation().ChangeManualTradeState(request));

    /// <summary>Closes a manual Portfolio Fund order after its closing trade completes.</summary>
    /// <param name="commandId">The stable command identifier.</param>
    /// <param name="request">The scoped order-close request.</param>
    /// <param name="nowUtc">The authoritative UTC command time.</param>
    /// <param name="principal">The authenticated operator principal.</param>
    /// <returns>The event containing the complete closed canonical order state.</returns>
    public IPortfolioFundDomainEvent CloseManualOrder(
        Guid commandId, ManualFundOrderMutationRequest request, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeCloseManualOrder(commandId, request, nowUtc, principal)));

    /// <summary>Computes CloseManualOrder without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeCloseManualOrder(
        Guid commandId, ManualFundOrderMutationRequest request, DateTime nowUtc, string principal) =>
        ComputeManualCompositionChange(commandId, nowUtc, principal, () => CreateCompositionComputation().CloseManualOrder(request));

    public IPortfolioFundDomainEvent MarkCompositionComposing(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeMarkCompositionComposing(commandId, expectedRevision, orderId, expectedOrderVersion, nowUtc, principal)));

    /// <summary>Computes MarkCompositionComposing without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeMarkCompositionComposing(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion, DateTime nowUtc, string principal) =>
        ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal, () => CreateCompositionComputation().MarkComposing(orderId, expectedOrderVersion));

    public IPortfolioFundDomainEvent RecordCompositionResult(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        OrderCompositionResultReference result, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeRecordCompositionResult(commandId, expectedRevision, orderId, expectedOrderVersion, result, nowUtc, principal)));

    /// <summary>Computes RecordCompositionResult without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeRecordCompositionResult(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        OrderCompositionResultReference result, DateTime nowUtc, string principal) =>
        ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal, () => CreateCompositionComputation().RecordComposed(orderId, expectedOrderVersion, result, nowUtc));

    public IPortfolioFundDomainEvent SynchronizeRisk(Guid commandId, long expectedOrderVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.RiskTerminalEvidence evidence, DateTime now, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeSynchronizeRisk(commandId, expectedOrderVersion, evidence, now, principal)));

    /// <summary>Computes SynchronizeRisk without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeSynchronizeRisk(Guid commandId, long expectedOrderVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.RiskTerminalEvidence evidence, DateTime now, string principal) => ComputeCompositionChange(commandId, Revision, now, principal, () => CreateCompositionComputation().SynchronizeRisk(expectedOrderVersion, evidence));

    public IPortfolioFundDomainEvent RecordRiskResult(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        RiskManagementResultReference result, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeRecordRiskResult(commandId, expectedRevision, orderId, expectedOrderVersion, result, nowUtc, principal)));

    /// <summary>Computes RecordRiskResult without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeRecordRiskResult(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        RiskManagementResultReference result, DateTime nowUtc, string principal) =>
        ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal, () => CreateCompositionComputation().RecordRiskOutcome(orderId, expectedOrderVersion, result, nowUtc));

    public IPortfolioFundDomainEvent AuthorizeRisk(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.FundRiskAuthorizationReference authorization, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAuthorizeRisk(commandId, expectedRevision, orderId, expectedOrderVersion, authorization, nowUtc, principal)));

    /// <summary>Computes AuthorizeRisk without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeAuthorizeRisk(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.FundRiskAuthorizationReference authorization, DateTime nowUtc, string principal) {
        if (FundMandate?.OperatingState != FundOperatingState.Active)
            throw new InvalidOperationException("Only an active Fund can authorize a new order.");
        return ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal,
            () => CreateCompositionComputation().AuthorizeRisk(orderId, expectedOrderVersion, authorization, nowUtc));
    }

    public IPortfolioFundDomainEvent FailComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeFailComposition(commandId, expectedRevision, orderId, expectedOrderVersion, reason, nowUtc, principal)));

    /// <summary>Computes FailComposition without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeFailComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal, () => CreateCompositionComputation().FailComposition(orderId, expectedOrderVersion, reason));

    public IPortfolioFundDomainEvent CancelComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeCancelComposition(commandId, expectedRevision, orderId, expectedOrderVersion, reason, nowUtc, principal)));

    /// <summary>Computes CancelComposition without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeCancelComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal, () => CreateCompositionComputation().Cancel(orderId, expectedOrderVersion, reason));

    public IPortfolioFundDomainEvent ExpireComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeExpireComposition(commandId, expectedRevision, orderId, expectedOrderVersion, reason, nowUtc, principal)));

    /// <summary>Computes ExpireComposition without changing authoritative state.</summary>
    internal IPortfolioFundChange ComputeExpireComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ComputeCompositionChange(commandId, expectedRevision, nowUtc, principal, () => CreateCompositionComputation().Expire(orderId, expectedOrderVersion, reason));

    public FundCompositionReservationResult Composition(int orderId) => _fundOrderCompositions.ReservationForOrder(orderId);

    public bool TryComposition(Guid idempotencyKey, out FundCompositionReservationResult reservation) =>
        _fundOrderCompositions.TryGetReservation(idempotencyKey, out reservation!);

    public static bool CanTransition(FundOperatingState from, FundOperatingState to, bool throughNewVersion) =>
        (from, to) switch
        {
            (FundOperatingState.Draft, FundOperatingState.Active or FundOperatingState.Disabled or FundOperatingState.Retired) => true,
            (FundOperatingState.Active, FundOperatingState.Paused or FundOperatingState.Disabled or FundOperatingState.Retired) => true,
            (FundOperatingState.Paused, FundOperatingState.Active or FundOperatingState.Disabled or FundOperatingState.Retired) => true,
            (FundOperatingState.Disabled, FundOperatingState.Active) => throughNewVersion,
            (FundOperatingState.Disabled, FundOperatingState.Retired) => true,
            _ => false,
        };

    /// <summary>Gets the event accepted by the current command for its existing durable append boundary.</summary>
    internal IPortfolioFundDomainEvent? PendingEvent { get; private set; }

    /// <summary>Applies one validated event; command identity and revision must match before mutation.</summary>
    internal bool Update(IPortfolioFundDomainEvent domainEvent, TomasAI.IFM.Shared.EventSourcing.ICommand command)
    {
        if (domainEvent.CommandId != command.CommandId || domainEvent.Revision != Revision + 1) return false;
        Apply(domainEvent, isReplay: false);
        PendingEvent = domainEvent;
        return true;
    }

    IPortfolioFundDomainEvent ApplyAndReturn(IPortfolioFundDomainEvent domainEvent)
    {
        Apply(domainEvent, isReplay: false);
        return domainEvent;
    }

    /// <summary>Creates an isolated calculation workspace from defensive business snapshots.</summary>
    PortfolioFundCompositionAggregate CreateCompositionComputation()
    {
        var composition = new PortfolioFundCompositionAggregate();
        composition.Restore(_fundOrderCompositions.CaptureState());
        return composition;
    }

    IPortfolioFundChange ComputeCompositionChange(Guid commandId, long expectedRevision, DateTime nowUtc, string principal,
        Func<FundOrderProjectionReadModel> change)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        var order = change();
        return new FundCompositionStateChangedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, order);
    }

    IPortfolioFundChange ComputeManualCompositionChange(
        Guid commandId,
        DateTime nowUtc,
        string principal,
        Func<FundCompositionReservationResult> change,
        int removedTradeId = 0)
    {
        RequireCurrent(Revision);
        ValidateCommand(commandId, nowUtc, principal);
        var reservation = change();
        return new FundManualOrderChangedCompute(
            Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reservation, removedTradeId);
    }


    void Apply(IPortfolioFundDomainEvent domainEvent, bool isReplay)
    {
        if (domainEvent.Revision != Revision + 1) throw new InvalidOperationException("Fund event revision is not contiguous.");
        if (!_commandIds.Add(domainEvent.CommandId))
            throw new InvalidOperationException(isReplay ? "Duplicate command in Fund history." : "Command was already applied.");
        switch (domainEvent)
        {
            case FundMandateCreatedEvent created:
                if (FundMandate is not null) throw new InvalidOperationException("Fund create event is duplicated.");
                FundMandate = created.Mandate.DefensiveCopy();
                break;
            case FundMandateVersionAddedEvent versioned:
                FundMandate = versioned.Mandate.DefensiveCopy();
                break;
            case FundOperatingStateChangedEvent changed:
                FundMandate = FundMandate! with { OperatingState = changed.State };
                break;
            case FundTradeTemplateAssignedEvent assigned:
                _tradeTemplateAssignments.Add(assigned.Assignment.DefensiveCopy());
                break;
            case FundCompositionReservedEvent reserved:
                _fundOrderCompositions.ApplyReservation(reserved.Reservation);
                break;
            case FundCompositionStateChangedEvent changed:
                _fundOrderCompositions.ApplyOrder(changed.Order);
                break;
            case FundManualOrderChangedEvent manual:
                _fundOrderCompositions.ApplyManualChange(manual.Reservation);
                break;
            case FundManualOrderDeletedEvent deleted:
                _fundOrderCompositions.ApplyManualDeletion(deleted.OrderId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
        Revision = domainEvent.Revision;
    }

    void RequireCurrent(long expectedRevision)
    {
        if (FundMandate is null) throw new InvalidOperationException("Fund mandate does not exist.");
        if (expectedRevision != Revision) throw new InvalidOperationException($"Expected revision {expectedRevision}, current revision is {Revision}.");
    }

    void ValidateCommand(Guid commandId, DateTime nowUtc, string principal)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("CommandId is required.", nameof(commandId));
        if (_commandIds.Contains(commandId)) throw new InvalidOperationException("Command was already applied.");
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Command time must be UTC.", nameof(nowUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
    }

    static void ThrowIfInvalid(IReadOnlyList<string> errors)
    {
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors));
    }

    static bool WindowsOverlap(DateTime leftStart, DateTime? leftEnd, DateTime rightStart, DateTime? rightEnd) =>
        leftStart < (rightEnd ?? DateTime.MaxValue) && rightStart < (leftEnd ?? DateTime.MaxValue);
}
