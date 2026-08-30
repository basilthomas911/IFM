using TomasAI.IFM.Domain.Portfolio.Command.Model;
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
    readonly List<FundTradeTemplateAssignmentReadModel> _assignments = [];
    readonly PortfolioFundCompositionAggregate _compositions = new();

    public FundMandateReadModel? Current { get; private set; }
    public long Revision { get; private set; }
    public bool Exists => Current is not null;
    public IReadOnlyList<FundTradeTemplateAssignmentReadModel> Assignments => _assignments;
    public IReadOnlyCollection<FundOrderProjectionReadModel> Orders => _compositions.Orders;

    public PortfolioFundDomainEvent Create(Guid commandId, FundMandateReadModel mandate, DateTime nowUtc, string principal)
    {
        ValidateCommand(commandId, nowUtc, principal);
        if (Exists) throw new InvalidOperationException("Fund mandate already exists.");
        if (mandate.FundMandateVersion != 1) throw new ArgumentException("A new Fund mandate must have version 1.", nameof(mandate));
        if (mandate.OperatingState != FundOperatingState.Draft) throw new ArgumentException("A new Fund mandate must begin in Draft.", nameof(mandate));
        ThrowIfInvalid(mandate.Validate());
        return ApplyAndReturn(new FundMandateCreated(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, mandate.DefensiveCopy()));
    }

    public PortfolioFundDomainEvent AddVersion(
        Guid commandId,
        long expectedRevision,
        FundMandateReadModel replacement,
        FundActivationContext activation,
        DateTime nowUtc,
        string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (Current!.OperatingState == FundOperatingState.Retired) throw new InvalidOperationException("A retired Fund cannot be versioned.");
        if (replacement.PortfolioId != Current.PortfolioId || replacement.FundId != Current.FundId)
            throw new ArgumentException("Portfolio/Fund parent identity cannot change.", nameof(replacement));
        if (replacement.FundCode != Current.FundCode) throw new ArgumentException("FundCode cannot change.", nameof(replacement));
        if (replacement.FundMandateVersion != Current.FundMandateVersion + 1)
            throw new ArgumentException("FundMandateVersion must increment by one.", nameof(replacement));
        if (replacement.OperatingState != Current.OperatingState &&
            !CanTransition(Current.OperatingState, replacement.OperatingState, throughNewVersion: true))
            throw new InvalidOperationException($"Fund transition {Current.OperatingState} -> {replacement.OperatingState} is not allowed.");
        if (replacement.OperatingState == FundOperatingState.Active && !activation.IsValid)
            throw new InvalidOperationException("Fund activation configuration is incomplete.");
        ThrowIfInvalid(replacement.Validate());
        return ApplyAndReturn(new FundMandateVersionAdded(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, replacement.DefensiveCopy()));
    }

    public PortfolioFundDomainEvent ChangeState(
        Guid commandId,
        long expectedRevision,
        FundOperatingState state,
        string reason,
        FundActivationContext activation,
        DateTime nowUtc,
        string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!CanTransition(Current!.OperatingState, state, throughNewVersion: false))
            throw new InvalidOperationException($"Fund transition {Current.OperatingState} -> {state} is not allowed.");
        if (state == FundOperatingState.Active && !activation.IsValid)
            throw new InvalidOperationException("Fund activation configuration is incomplete.");
        return ApplyAndReturn(new FundOperatingStateChanged(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, state, reason.Trim()));
    }

    public void Replay(IEnumerable<PortfolioFundDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(x => x.Revision)) Apply(domainEvent, isReplay: true);
    }

    public PortfolioFundAggregateSnapshot CaptureSnapshot()
    {
        if (Current is null) throw new InvalidOperationException("A missing Fund cannot be snapshotted.");
        return new PortfolioFundAggregateSnapshot(
            Revision,
            Current.DefensiveCopy(),
            [.. _assignments.OrderBy(x => x.AssignmentVersion).Select(x => x.DefensiveCopy())],
            [.. _compositions.CaptureState()],
            [.. _commandIds.Order()]);
    }

    public void RestoreSnapshot(PortfolioFundAggregateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Exists || Revision != 0) throw new InvalidOperationException("A snapshot can only restore an empty Fund aggregate.");
        if (snapshot.Revision <= 0) throw new InvalidOperationException("Snapshot revision must be positive.");
        ThrowIfInvalid(snapshot.Current.Validate());
        Current = snapshot.Current.DefensiveCopy();
        _assignments.AddRange(snapshot.Assignments.Select(x => x.DefensiveCopy()));
        _compositions.Restore(snapshot.Compositions);
        foreach (var commandId in snapshot.AppliedCommandIds)
            if (commandId == Guid.Empty || !_commandIds.Add(commandId)) throw new InvalidOperationException("Snapshot contains invalid command history.");
        Revision = snapshot.Revision;
    }

    public PortfolioFundDomainEvent AssignTradeTemplate(
        Guid commandId,
        long expectedRevision,
        FundTradeTemplateAssignmentReadModel assignment,
        DateTime nowUtc,
        string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (Current!.OperatingState == FundOperatingState.Retired) throw new InvalidOperationException("A retired Fund cannot receive assignments.");
        if (assignment.PortfolioId != Current.PortfolioId || assignment.FundId != Current.FundId)
            throw new ArgumentException("Assignment parent identity does not match the Fund.", nameof(assignment));
        if (assignment.FundMandateVersion != Current.FundMandateVersion)
            throw new ArgumentException("Assignment mandate version is not current.", nameof(assignment));
        if (assignment.DecisionHorizon != Current.DecisionHorizon)
            throw new ArgumentException("Assignment horizon is incompatible with the Fund mandate.", nameof(assignment));
        if (!assignment.UnderlyingUniverse.All(x => Current.UnderlyingUniverse.Contains(x, StringComparer.Ordinal)))
            throw new ArgumentException("Assignment underlying is incompatible with the Fund mandate.", nameof(assignment));
        if (!Current.EligibleAssetTypes.Contains(assignment.AssetType, StringComparer.Ordinal))
            throw new ArgumentException("Assignment asset type is incompatible with the Fund mandate.", nameof(assignment));
        if (!Current.PermittedTradeFamilies.Contains(assignment.TradeFamily, StringComparer.Ordinal))
            throw new ArgumentException("Assignment trade family is incompatible with the Fund mandate.", nameof(assignment));
        ThrowIfInvalid(assignment.Validate());
        var latestVersion = _assignments.Count == 0 ? 0 : _assignments.Max(x => x.AssignmentVersion);
        if (assignment.AssignmentVersion != latestVersion + 1)
            throw new ArgumentException("AssignmentVersion must increment the Fund assignment stream by one.", nameof(assignment));
        if (_assignments.Any(existing => existing.TradeTemplateId == assignment.TradeTemplateId &&
                                         WindowsOverlap(existing.EffectiveFromUtc, existing.EffectiveUntilUtc,
                                             assignment.EffectiveFromUtc, assignment.EffectiveUntilUtc)))
            throw new InvalidOperationException("The same TradeTemplate has an overlapping assignment window.");
        return ApplyAndReturn(new FundTradeTemplateAssigned(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, assignment.DefensiveCopy()));
    }

    public IReadOnlyList<FundTradeTemplateAssignmentReadModel> EffectiveAssignments(DateTime atUtc) =>
        _assignments.Where(x => x.IsEffectiveAt(atUtc)).OrderBy(x => x.Priority).ThenBy(x => x.TradeTemplateId).Select(x => x.DefensiveCopy()).ToArray();

    public PortfolioFundDomainEvent ReserveComposition(
        Guid commandId, long expectedRevision, ReserveFundOrderCompositionRequest request,
        PortfolioFundStrategySnapshot snapshot, int orderId, IReadOnlyList<int> tradeIds,
        DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (Current!.OperatingState != FundOperatingState.Active)
            throw new InvalidOperationException("Only an active Fund can reserve a composition.");
        if (request.PortfolioId != Current.PortfolioId || request.FundId != Current.FundId ||
            request.FundMandateVersion != Current.FundMandateVersion)
            throw new ArgumentException("Reservation parent identity/version does not match the Fund.", nameof(request));
        var reservation = _compositions.Reserve(request, snapshot, orderId, tradeIds, nowUtc, principal);
        if (reservation.Disposition == ReservationDisposition.IdempotentReplay)
            throw new InvalidOperationException("An idempotent reservation must be returned from committed-command lookup before aggregate mutation.");
        return CommitAlreadyApplied(new FundCompositionReserved(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reservation));
    }

    public PortfolioFundDomainEvent MarkCompositionComposing(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion, DateTime nowUtc, string principal) =>
        ChangeComposition(commandId, expectedRevision, nowUtc, principal, () => _compositions.MarkComposing(orderId, expectedOrderVersion));

    public PortfolioFundDomainEvent RecordCompositionResult(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        OrderCompositionResultReference result, DateTime nowUtc, string principal) =>
        ChangeComposition(commandId, expectedRevision, nowUtc, principal, () => _compositions.RecordComposed(orderId, expectedOrderVersion, result, nowUtc));

    public PortfolioFundDomainEvent RecordRiskResult(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        RiskManagementResultReference result, DateTime nowUtc, string principal) =>
        ChangeComposition(commandId, expectedRevision, nowUtc, principal, () => _compositions.RecordRiskOutcome(orderId, expectedOrderVersion, result, nowUtc));

    public PortfolioFundDomainEvent FailComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ChangeComposition(commandId, expectedRevision, nowUtc, principal, () => _compositions.FailComposition(orderId, expectedOrderVersion, reason));

    public PortfolioFundDomainEvent CancelComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ChangeComposition(commandId, expectedRevision, nowUtc, principal, () => _compositions.Cancel(orderId, expectedOrderVersion, reason));

    public PortfolioFundDomainEvent ExpireComposition(Guid commandId, long expectedRevision, int orderId, long expectedOrderVersion,
        string reason, DateTime nowUtc, string principal) =>
        ChangeComposition(commandId, expectedRevision, nowUtc, principal, () => _compositions.Expire(orderId, expectedOrderVersion, reason));

    public FundCompositionReservationResult Composition(int orderId) => _compositions.ReservationForOrder(orderId);

    public bool TryComposition(Guid idempotencyKey, out FundCompositionReservationResult reservation) =>
        _compositions.TryGetReservation(idempotencyKey, out reservation!);

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

    PortfolioFundDomainEvent ApplyAndReturn(PortfolioFundDomainEvent domainEvent)
    {
        Apply(domainEvent, isReplay: false);
        return domainEvent;
    }

    PortfolioFundDomainEvent ChangeComposition(Guid commandId, long expectedRevision, DateTime nowUtc, string principal,
        Func<FundOrderProjectionReadModel> change)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        var order = change();
        return CommitAlreadyApplied(new FundCompositionStateChanged(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, order));
    }

    PortfolioFundDomainEvent CommitAlreadyApplied(PortfolioFundDomainEvent domainEvent)
    {
        if (!_commandIds.Add(domainEvent.CommandId)) throw new InvalidOperationException("Command was already applied.");
        Revision = domainEvent.Revision;
        return domainEvent;
    }

    void Apply(PortfolioFundDomainEvent domainEvent, bool isReplay)
    {
        if (domainEvent.Revision != Revision + 1) throw new InvalidOperationException("Fund event revision is not contiguous.");
        if (!_commandIds.Add(domainEvent.CommandId))
            throw new InvalidOperationException(isReplay ? "Duplicate command in Fund history." : "Command was already applied.");
        switch (domainEvent)
        {
            case FundMandateCreated created:
                if (Current is not null) throw new InvalidOperationException("Fund create event is duplicated.");
                Current = created.Mandate.DefensiveCopy();
                break;
            case FundMandateVersionAdded versioned:
                Current = versioned.Mandate.DefensiveCopy();
                break;
            case FundOperatingStateChanged changed:
                Current = Current! with { OperatingState = changed.State };
                break;
            case FundTradeTemplateAssigned assigned:
                _assignments.Add(assigned.Assignment.DefensiveCopy());
                break;
            case FundCompositionReserved reserved:
                _compositions.ApplyReservation(reserved.Reservation);
                break;
            case FundCompositionStateChanged changed:
                _compositions.ApplyOrder(changed.Order);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
        Revision = domainEvent.Revision;
    }

    void RequireCurrent(long expectedRevision)
    {
        if (Current is null) throw new InvalidOperationException("Fund mandate does not exist.");
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
