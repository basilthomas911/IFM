using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

/// <summary>Pure Portfolio state machine. Persistence and transport are adapters around this aggregate.</summary>
public sealed class PortfolioAggregate
{
    readonly HashSet<int> _fundIds = [];
    readonly HashSet<Guid> _commandIds = [];
    readonly Dictionary<int, List<FundAllocationReadModel>> _allocations = [];
    readonly Dictionary<int, List<FundRiskEnvelopeReadModel>> _envelopes = [];

    public PortfolioReadModel? Current { get; private set; }
    public long Revision { get; private set; }
    public IReadOnlySet<int> FundIds => _fundIds;
    public bool Exists => Current is not null;
    public bool IsDeleted { get; private set; }
    public IReadOnlyList<FundAllocationReadModel> Allocations(int fundId) => _allocations.GetValueOrDefault(fundId) ?? [];
    public IReadOnlyList<FundRiskEnvelopeReadModel> RiskEnvelopes(int fundId) => _envelopes.GetValueOrDefault(fundId) ?? [];

    public IPortfolioDomainEvent Create(Guid commandId, PortfolioReadModel portfolio, DateTime nowUtc, string principal)
    {
        ValidateCommand(commandId, nowUtc, principal);
        if (Exists) throw new InvalidOperationException("Portfolio already exists.");
        if (portfolio.PortfolioVersion != 1) throw new ArgumentException("A new Portfolio must have version 1.", nameof(portfolio));
        if (portfolio.OperatingState != PortfolioOperatingState.Draft)
            throw new ArgumentException("A new Portfolio must begin in Draft.", nameof(portfolio));
        ThrowIfInvalid(portfolio.Validate());
        return ApplyAndReturn(new PortfolioCreatedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, portfolio.DefensiveCopy()));
    }

    public IPortfolioDomainEvent AddVersion(Guid commandId, long expectedRevision, PortfolioReadModel replacement, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (Current!.OperatingState == PortfolioOperatingState.Retired) throw new InvalidOperationException("A retired Portfolio cannot be versioned.");
        if (replacement.PortfolioId != Current.PortfolioId) throw new ArgumentException("PortfolioId cannot change.", nameof(replacement));
        if (replacement.PortfolioVersion != Current.PortfolioVersion + 1) throw new ArgumentException("PortfolioVersion must increment by one.", nameof(replacement));
        if (replacement.OperatingState != Current.OperatingState
            && !CanTransition(Current.OperatingState, replacement.OperatingState, Current.OperatingState == PortfolioOperatingState.Disabled))
            throw new InvalidOperationException($"Portfolio transition {Current.OperatingState} -> {replacement.OperatingState} is not allowed through a new version.");
        ThrowIfInvalid(replacement.Validate());
        return ApplyAndReturn(new PortfolioVersionAddedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, replacement.DefensiveCopy()));
    }

    public IPortfolioDomainEvent ChangeState(Guid commandId, long expectedRevision, PortfolioOperatingState state, string reason, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!CanTransition(Current!.OperatingState, state, throughNewVersion: false))
            throw new InvalidOperationException($"Portfolio transition {Current.OperatingState} -> {state} is not allowed.");
        if (state == PortfolioOperatingState.Active) ThrowIfInvalid((Current with { OperatingState = state }).Validate());
        return ApplyAndReturn(new PortfolioOperatingStateChangedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, state, reason.Trim()));
    }

    public IPortfolioDomainEvent AssignFinancialPolicy(Guid commandId, long expectedRevision, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PortfolioId != Current!.PortfolioId || policy.OperatingState != PortfolioFinancialPolicyState.Active)
            throw new InvalidOperationException("Portfolio can only select its own Active financial policy.");
        ThrowIfInvalid(policy.Validate(forActivation: true));
        return ApplyAndReturn(new PortfolioFinancialPolicyAssignedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policy.PolicyId, policy.PolicyVersion));
    }

    public IPortfolioDomainEvent AddFund(Guid commandId, long expectedRevision, PortfolioFundId fundId, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ThrowIfInvalid(fundId.Validate());
        if (fundId.PortfolioId != Current!.PortfolioId) throw new ArgumentException("Fund parent does not match Portfolio.", nameof(fundId));
        if (_fundIds.Contains(fundId.FundId)) throw new InvalidOperationException("Fund already belongs to Portfolio.");
        if (Current.OperatingState == PortfolioOperatingState.Retired) throw new InvalidOperationException("A retired Portfolio cannot add Funds.");
        return ApplyAndReturn(new FundAddedToPortfolioEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, fundId));
    }

    public IPortfolioDomainEvent Retire(Guid commandId, long expectedRevision, string reason, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Current!.OperatingState == PortfolioOperatingState.Retired) throw new InvalidOperationException("Portfolio is already retired.");
        return ApplyAndReturn(new PortfolioRetiredEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reason.Trim()));
    }

    public IPortfolioDomainEvent DeleteDraft(Guid commandId, long expectedRevision, string reason, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Current!.OperatingState != PortfolioOperatingState.Draft)
            throw new InvalidOperationException("Only a Draft Portfolio can be deleted.");
        return ApplyAndReturn(new DraftPortfolioDeletedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reason.Trim()));
    }

    public IPortfolioDomainEvent DelegateAllocation(Guid commandId, long expectedRevision, FundAllocationReadModel allocation, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        RequireFund(allocation.PortfolioId, allocation.FundId);
        if (allocation.PortfolioVersion != Current!.PortfolioVersion) throw new ArgumentException("Allocation PortfolioVersion is not current.", nameof(allocation));
        ThrowIfInvalid(allocation.Validate());
        var versions = _allocations.GetValueOrDefault(allocation.FundId);
        var latest = versions?.Count > 0 ? versions.Max(x => x.AllocationVersion) : 0;
        if (allocation.AllocationVersion != latest + 1) throw new ArgumentException("AllocationVersion must increment by one.", nameof(allocation));
        return ApplyAndReturn(new FundAllocationDelegatedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, allocation));
    }

    public IPortfolioDomainEvent DelegateRiskEnvelope(Guid commandId, long expectedRevision, FundRiskEnvelopeReadModel envelope, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        RequireFund(envelope.PortfolioId, envelope.FundId);
        if (envelope.PortfolioVersion != Current!.PortfolioVersion) throw new ArgumentException("Envelope PortfolioVersion is not current.", nameof(envelope));
        ThrowIfInvalid(envelope.Validate());
        var allocation = _allocations.GetValueOrDefault(envelope.FundId)?.OrderByDescending(x => x.AllocationVersion).FirstOrDefault()
            ?? throw new InvalidOperationException("A Fund allocation is required before delegating a risk envelope.");
        if (!string.Equals(allocation.Currency, envelope.Currency, StringComparison.Ordinal) || envelope.AllocatedCapital > allocation.AllocatedCapital)
            throw new InvalidOperationException("The risk envelope exceeds or mismatches its Fund allocation.");
        var versions = _envelopes.GetValueOrDefault(envelope.FundId);
        var latest = versions?.Count > 0 ? versions.Max(x => x.EnvelopeVersion) : 0;
        if (envelope.EnvelopeVersion != latest + 1) throw new ArgumentException("EnvelopeVersion must increment by one.", nameof(envelope));
        return ApplyAndReturn(new FundRiskEnvelopeDelegatedEvent(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, envelope));
    }

    public void Replay(IEnumerable<IPortfolioDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(x => x.Revision)) Apply(domainEvent, isReplay: true);
    }

    public PortfolioAggregateSnapshot CaptureSnapshot()
    {
        if (Current is null) throw new InvalidOperationException("A missing Portfolio cannot be snapshotted.");
        if (IsDeleted) throw new InvalidOperationException("A deleted Portfolio cannot be snapshotted.");
        return new PortfolioAggregateSnapshot(
            Revision,
            Current.DefensiveCopy(),
            [.. _fundIds.Order()],
            [.. _allocations.Values.SelectMany(x => x).OrderBy(x => x.FundId).ThenBy(x => x.AllocationVersion)],
            [.. _envelopes.Values.SelectMany(x => x).OrderBy(x => x.FundId).ThenBy(x => x.EnvelopeVersion)],
            [.. _commandIds.Order()]);
    }

    public void RestoreSnapshot(PortfolioAggregateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Exists || Revision != 0) throw new InvalidOperationException("A snapshot can only restore an empty Portfolio aggregate.");
        if (snapshot.Revision <= 0) throw new InvalidOperationException("Snapshot revision must be positive.");
        ThrowIfInvalid(snapshot.Current.Validate());
        Current = snapshot.Current.DefensiveCopy();
        foreach (var fundId in snapshot.FundIds)
            if (!_fundIds.Add(fundId) || fundId <= 0) throw new InvalidOperationException("Snapshot contains invalid Fund membership.");
        foreach (var item in snapshot.Allocations)
        {
            if (!_allocations.TryGetValue(item.FundId, out var values)) _allocations[item.FundId] = values = [];
            values.Add(item);
        }
        foreach (var item in snapshot.RiskEnvelopes)
        {
            if (!_envelopes.TryGetValue(item.FundId, out var values)) _envelopes[item.FundId] = values = [];
            values.Add(item);
        }
        foreach (var commandId in snapshot.AppliedCommandIds)
            if (commandId == Guid.Empty || !_commandIds.Add(commandId)) throw new InvalidOperationException("Snapshot contains invalid command history.");
        Revision = snapshot.Revision;
    }

    public static bool CanTransition(PortfolioOperatingState from, PortfolioOperatingState to, bool throughNewVersion) =>
        (from, to) switch
        {
            (PortfolioOperatingState.Draft, PortfolioOperatingState.Active or PortfolioOperatingState.Disabled or PortfolioOperatingState.Retired) => true,
            (PortfolioOperatingState.Active, PortfolioOperatingState.Paused or PortfolioOperatingState.ReduceOnly or PortfolioOperatingState.Disabled or PortfolioOperatingState.Retired) => true,
            (PortfolioOperatingState.Paused, PortfolioOperatingState.Active or PortfolioOperatingState.Disabled or PortfolioOperatingState.Retired) => true,
            (PortfolioOperatingState.ReduceOnly, PortfolioOperatingState.Active or PortfolioOperatingState.Paused or PortfolioOperatingState.Disabled or PortfolioOperatingState.Retired) => true,
            (PortfolioOperatingState.Disabled, PortfolioOperatingState.Active) => throughNewVersion,
            (PortfolioOperatingState.Disabled, PortfolioOperatingState.Retired) => true,
            _ => false,
        };

    IPortfolioDomainEvent ApplyAndReturn(IPortfolioDomainEvent domainEvent)
    {
        Apply(domainEvent, isReplay: false);
        return domainEvent;
    }

    void Apply(IPortfolioDomainEvent domainEvent, bool isReplay)
    {
        if (domainEvent.Revision != Revision + 1) throw new InvalidOperationException("Portfolio event revision is not contiguous.");
        if (IsDeleted) throw new InvalidOperationException("Portfolio event history cannot continue after Draft deletion.");
        if (!_commandIds.Add(domainEvent.CommandId))
        {
            if (isReplay) throw new InvalidOperationException("Duplicate command in Portfolio history.");
            throw new InvalidOperationException("Command was already applied.");
        }
        switch (domainEvent)
        {
            case PortfolioCreatedEvent created:
                if (Current is not null) throw new InvalidOperationException("Portfolio create event is duplicated.");
                Current = created.Portfolio.DefensiveCopy();
                break;
            case PortfolioVersionAddedEvent versionAdded:
                Current = versionAdded.Portfolio.DefensiveCopy();
                break;
            case PortfolioOperatingStateChangedEvent changed:
                Current = Current! with { OperatingState = changed.State };
                break;
            case PortfolioFinancialPolicyAssignedEvent assigned:
                Current = Current! with
                {
                    PortfolioVersion = Current.PortfolioVersion + 1,
                    ActivePolicyId = assigned.PolicyId,
                    ActivePolicyVersion = assigned.PolicyVersion,
                    CreatedOnUtc = assigned.OccurredOnUtc,
                    CreatedBy = assigned.Principal,
                };
                break;
            case FundAddedToPortfolioEvent fundAdded:
                if (!_fundIds.Add(fundAdded.FundId.FundId)) throw new InvalidOperationException("Fund membership event is duplicated.");
                break;
            case PortfolioRetiredEvent:
                Current = Current! with { OperatingState = PortfolioOperatingState.Retired };
                break;
            case DraftPortfolioDeletedEvent:
                if (Current is null || Current.OperatingState != PortfolioOperatingState.Draft)
                    throw new InvalidOperationException("Only a Draft Portfolio can apply a deletion tombstone.");
                IsDeleted = true;
                break;
            case FundAllocationDelegatedEvent delegated:
                if (!_allocations.TryGetValue(delegated.Allocation.FundId, out var allocations)) _allocations[delegated.Allocation.FundId] = allocations = [];
                allocations.Add(delegated.Allocation);
                break;
            case FundRiskEnvelopeDelegatedEvent delegated:
                if (!_envelopes.TryGetValue(delegated.Envelope.FundId, out var envelopes)) _envelopes[delegated.Envelope.FundId] = envelopes = [];
                envelopes.Add(delegated.Envelope);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
        Revision = domainEvent.Revision;
    }

    void RequireCurrent(long expectedRevision)
    {
        if (Current is null) throw new InvalidOperationException("Portfolio does not exist.");
        if (IsDeleted) throw new InvalidOperationException("Portfolio draft was deleted.");
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

    void RequireFund(int portfolioId, int fundId)
    {
        if (portfolioId != Current!.PortfolioId || !_fundIds.Contains(fundId))
            throw new InvalidOperationException("Fund is not a member of this Portfolio.");
    }
}
