using TomasAI.IFM.Domain.Portfolio.Command.Model;
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
    readonly Dictionary<int, List<FundAllocationReadModel>> _fundAllocations = [];
    readonly Dictionary<int, List<FundRiskEnvelopeReadModel>> _fundRiskEnvelopes = [];

    /// <summary>Gets the authoritative PortfolioDefinition applied from source events.</summary>
    public PortfolioReadModel? PortfolioDefinition { get; private set; }
    /// <summary>Gets the existing read-only compatibility view of the business state.</summary>
    public PortfolioReadModel? Current => PortfolioDefinition;
    public long Revision { get; private set; }
    public IReadOnlySet<int> FundIds => _fundIds;
    public bool Exists => PortfolioDefinition is not null;
    public bool IsDeleted { get; private set; }
    public IReadOnlyList<FundAllocationReadModel> Allocations(int fundId) => _fundAllocations.GetValueOrDefault(fundId) ?? [];
    public IReadOnlyList<FundRiskEnvelopeReadModel> RiskEnvelopes(int fundId) => _fundRiskEnvelopes.GetValueOrDefault(fundId) ?? [];

    public IPortfolioDomainEvent Create(Guid commandId, PortfolioReadModel portfolio, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeCreate(commandId, portfolio, nowUtc, principal)));

    /// <summary>Computes Create without changing authoritative state.</summary>
    internal IPortfolioChange ComputeCreate(Guid commandId, PortfolioReadModel portfolio, DateTime nowUtc, string principal) {
        ValidateCommand(commandId, nowUtc, principal);
        if (Exists) throw new InvalidOperationException("Portfolio already exists.");
        if (portfolio.PortfolioVersion != 1) throw new ArgumentException("A new Portfolio must have version 1.", nameof(portfolio));
        if (portfolio.OperatingState != PortfolioOperatingState.Draft)
            throw new ArgumentException("A new Portfolio must begin in Draft.", nameof(portfolio));
        ThrowIfInvalid(portfolio.Validate());
        return new PortfolioCreatedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, portfolio.DefensiveCopy());
    }

    public IPortfolioDomainEvent AddVersion(Guid commandId, long expectedRevision, PortfolioReadModel replacement, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAddVersion(commandId, expectedRevision, replacement, nowUtc, principal)));

    /// <summary>Computes AddVersion without changing authoritative state.</summary>
    internal IPortfolioChange ComputeAddVersion(Guid commandId, long expectedRevision, PortfolioReadModel replacement, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (PortfolioDefinition!.OperatingState == PortfolioOperatingState.Retired) throw new InvalidOperationException("A retired Portfolio cannot be versioned.");
        if (replacement.PortfolioId != PortfolioDefinition.PortfolioId) throw new ArgumentException("PortfolioId cannot change.", nameof(replacement));
        if (replacement.PortfolioVersion != PortfolioDefinition.PortfolioVersion + 1) throw new ArgumentException("PortfolioVersion must increment by one.", nameof(replacement));
        if (replacement.OperatingState != PortfolioDefinition.OperatingState
            && !CanTransition(PortfolioDefinition.OperatingState, replacement.OperatingState, PortfolioDefinition.OperatingState == PortfolioOperatingState.Disabled))
            throw new InvalidOperationException($"Portfolio transition {PortfolioDefinition.OperatingState} -> {replacement.OperatingState} is not allowed through a new version.");
        ThrowIfInvalid(replacement.Validate());
        return new PortfolioVersionAddedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, replacement.DefensiveCopy());
    }

    public IPortfolioDomainEvent ChangeState(Guid commandId, long expectedRevision, PortfolioOperatingState state, string reason, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeChangeState(commandId, expectedRevision, state, reason, nowUtc, principal)));

    /// <summary>Computes ChangeState without changing authoritative state.</summary>
    internal IPortfolioChange ComputeChangeState(Guid commandId, long expectedRevision, PortfolioOperatingState state, string reason, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (!CanTransition(PortfolioDefinition!.OperatingState, state, throughNewVersion: false))
            throw new InvalidOperationException($"Portfolio transition {PortfolioDefinition.OperatingState} -> {state} is not allowed.");
        if (state == PortfolioOperatingState.Active) ThrowIfInvalid((PortfolioDefinition with { OperatingState = state }).Validate());
        return new PortfolioOperatingStateChangedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, state, reason.Trim());
    }

    public IPortfolioDomainEvent AssignFinancialPolicy(Guid commandId, long expectedRevision, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAssignFinancialPolicy(commandId, expectedRevision, policy, nowUtc, principal)));

    /// <summary>Computes AssignFinancialPolicy without changing authoritative state.</summary>
    internal IPortfolioChange ComputeAssignFinancialPolicy(Guid commandId, long expectedRevision, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PortfolioId != PortfolioDefinition!.PortfolioId || policy.OperatingState != PortfolioFinancialPolicyState.Active)
            throw new InvalidOperationException("Portfolio can only select its own Active financial policy.");
        ThrowIfInvalid(policy.Validate(forActivation: true));
        return new PortfolioFinancialPolicyAssignedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policy.PolicyId, policy.PolicyVersion);
    }

    public IPortfolioDomainEvent AddFund(Guid commandId, long expectedRevision, PortfolioFundId fundId, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAddFund(commandId, expectedRevision, fundId, nowUtc, principal)));

    /// <summary>Computes AddFund without changing authoritative state.</summary>
    internal IPortfolioChange ComputeAddFund(Guid commandId, long expectedRevision, PortfolioFundId fundId, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ThrowIfInvalid(fundId.Validate());
        if (fundId.PortfolioId != PortfolioDefinition!.PortfolioId) throw new ArgumentException("Fund parent does not match Portfolio.", nameof(fundId));
        if (_fundIds.Contains(fundId.FundId)) throw new InvalidOperationException("Fund already belongs to Portfolio.");
        if (PortfolioDefinition.OperatingState == PortfolioOperatingState.Retired) throw new InvalidOperationException("A retired Portfolio cannot add Funds.");
        return new FundAddedToPortfolioCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, fundId);
    }

    public IPortfolioDomainEvent Retire(Guid commandId, long expectedRevision, string reason, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeRetire(commandId, expectedRevision, reason, nowUtc, principal)));

    /// <summary>Computes Retire without changing authoritative state.</summary>
    internal IPortfolioChange ComputeRetire(Guid commandId, long expectedRevision, string reason, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (PortfolioDefinition!.OperatingState == PortfolioOperatingState.Retired) throw new InvalidOperationException("Portfolio is already retired.");
        return new PortfolioRetiredCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reason.Trim());
    }

    public IPortfolioDomainEvent DeleteDraft(Guid commandId, long expectedRevision, string reason, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeDeleteDraft(commandId, expectedRevision, reason, nowUtc, principal)));

    /// <summary>Computes DeleteDraft without changing authoritative state.</summary>
    internal IPortfolioChange ComputeDeleteDraft(Guid commandId, long expectedRevision, string reason, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (PortfolioDefinition!.OperatingState != PortfolioOperatingState.Draft)
            throw new InvalidOperationException("Only a Draft Portfolio can be deleted.");
        return new DraftPortfolioDeletedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reason.Trim());
    }

    public IPortfolioDomainEvent DelegateAllocation(Guid commandId, long expectedRevision, FundAllocationReadModel allocation, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeDelegateAllocation(commandId, expectedRevision, allocation, nowUtc, principal)));

    /// <summary>Computes DelegateAllocation without changing authoritative state.</summary>
    internal IPortfolioChange ComputeDelegateAllocation(Guid commandId, long expectedRevision, FundAllocationReadModel allocation, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        RequireFund(allocation.PortfolioId, allocation.FundId);
        if (allocation.PortfolioVersion != PortfolioDefinition!.PortfolioVersion) throw new ArgumentException("Allocation PortfolioVersion is not current.", nameof(allocation));
        ThrowIfInvalid(allocation.Validate());
        var versions = _fundAllocations.GetValueOrDefault(allocation.FundId);
        var latest = versions?.Count > 0 ? versions.Max(x => x.AllocationVersion) : 0;
        if (allocation.AllocationVersion != latest + 1) throw new ArgumentException("AllocationVersion must increment by one.", nameof(allocation));
        return new FundAllocationDelegatedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, allocation);
    }

    public IPortfolioDomainEvent DelegateRiskEnvelope(Guid commandId, long expectedRevision, FundRiskEnvelopeReadModel envelope, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeDelegateRiskEnvelope(commandId, expectedRevision, envelope, nowUtc, principal)));

    /// <summary>Computes DelegateRiskEnvelope without changing authoritative state.</summary>
    internal IPortfolioChange ComputeDelegateRiskEnvelope(Guid commandId, long expectedRevision, FundRiskEnvelopeReadModel envelope, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        RequireFund(envelope.PortfolioId, envelope.FundId);
        if (envelope.PortfolioVersion != PortfolioDefinition!.PortfolioVersion) throw new ArgumentException("Envelope PortfolioVersion is not current.", nameof(envelope));
        ThrowIfInvalid(envelope.Validate());
        var allocation = _fundAllocations.GetValueOrDefault(envelope.FundId)?.OrderByDescending(x => x.AllocationVersion).FirstOrDefault()
            ?? throw new InvalidOperationException("A Fund allocation is required before delegating a risk envelope.");
        if (!string.Equals(allocation.Currency, envelope.Currency, StringComparison.Ordinal) || envelope.AllocatedCapital > allocation.AllocatedCapital)
            throw new InvalidOperationException("The risk envelope exceeds or mismatches its Fund allocation.");
        var versions = _fundRiskEnvelopes.GetValueOrDefault(envelope.FundId);
        var latest = versions?.Count > 0 ? versions.Max(x => x.EnvelopeVersion) : 0;
        if (envelope.EnvelopeVersion != latest + 1) throw new ArgumentException("EnvelopeVersion must increment by one.", nameof(envelope));
        return new FundRiskEnvelopeDelegatedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, envelope);
    }

    public void Replay(IEnumerable<IPortfolioDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(x => x.Revision)) Apply(domainEvent, isReplay: true);
    }

    public PortfolioAggregateSnapshot CaptureSnapshot()
    {
        if (PortfolioDefinition is null) throw new InvalidOperationException("A missing Portfolio cannot be snapshotted.");
        if (IsDeleted) throw new InvalidOperationException("A deleted Portfolio cannot be snapshotted.");
        return new PortfolioAggregateSnapshot(
            Revision,
            PortfolioDefinition.DefensiveCopy(),
            [.. _fundIds.Order()],
            [.. _fundAllocations.Values.SelectMany(x => x).OrderBy(x => x.FundId).ThenBy(x => x.AllocationVersion)],
            [.. _fundRiskEnvelopes.Values.SelectMany(x => x).OrderBy(x => x.FundId).ThenBy(x => x.EnvelopeVersion)],
            [.. _commandIds.Order()]);
    }

    public void RestoreSnapshot(PortfolioAggregateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (Exists || Revision != 0) throw new InvalidOperationException("A snapshot can only restore an empty Portfolio aggregate.");
        if (snapshot.Revision <= 0) throw new InvalidOperationException("Snapshot revision must be positive.");
        ThrowIfInvalid(snapshot.Current.Validate());
        PortfolioDefinition = snapshot.Current.DefensiveCopy();
        foreach (var fundId in snapshot.FundIds)
            if (!_fundIds.Add(fundId) || fundId <= 0) throw new InvalidOperationException("Snapshot contains invalid Fund membership.");
        foreach (var item in snapshot.Allocations)
        {
            if (!_fundAllocations.TryGetValue(item.FundId, out var values)) _fundAllocations[item.FundId] = values = [];
            values.Add(item);
        }
        foreach (var item in snapshot.RiskEnvelopes)
        {
            if (!_fundRiskEnvelopes.TryGetValue(item.FundId, out var values)) _fundRiskEnvelopes[item.FundId] = values = [];
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

    /// <summary>Gets the event accepted by the current command for its existing durable append boundary.</summary>
    internal IPortfolioDomainEvent? PendingEvent { get; private set; }

    /// <summary>Applies one validated event; command identity and revision must match before mutation.</summary>
    internal bool Update(IPortfolioDomainEvent domainEvent, TomasAI.IFM.Shared.EventSourcing.ICommand command)
    {
        if (domainEvent.CommandId != command.CommandId || domainEvent.Revision != Revision + 1) return false;
        Apply(domainEvent, isReplay: false);
        PendingEvent = domainEvent;
        return true;
    }

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
                if (PortfolioDefinition is not null) throw new InvalidOperationException("Portfolio create event is duplicated.");
                PortfolioDefinition = created.Portfolio.DefensiveCopy();
                break;
            case PortfolioVersionAddedEvent versionAdded:
                PortfolioDefinition = versionAdded.Portfolio.DefensiveCopy();
                break;
            case PortfolioOperatingStateChangedEvent changed:
                PortfolioDefinition = PortfolioDefinition! with { OperatingState = changed.State };
                break;
            case PortfolioFinancialPolicyAssignedEvent assigned:
                PortfolioDefinition = PortfolioDefinition! with
                {
                    PortfolioVersion = PortfolioDefinition.PortfolioVersion + 1,
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
                PortfolioDefinition = PortfolioDefinition! with { OperatingState = PortfolioOperatingState.Retired };
                break;
            case DraftPortfolioDeletedEvent:
                if (PortfolioDefinition is null || PortfolioDefinition.OperatingState != PortfolioOperatingState.Draft)
                    throw new InvalidOperationException("Only a Draft Portfolio can apply a deletion tombstone.");
                IsDeleted = true;
                break;
            case FundAllocationDelegatedEvent delegated:
                if (!_fundAllocations.TryGetValue(delegated.Allocation.FundId, out var allocations)) _fundAllocations[delegated.Allocation.FundId] = allocations = [];
                allocations.Add(delegated.Allocation);
                break;
            case FundRiskEnvelopeDelegatedEvent delegated:
                if (!_fundRiskEnvelopes.TryGetValue(delegated.Envelope.FundId, out var envelopes)) _fundRiskEnvelopes[delegated.Envelope.FundId] = envelopes = [];
                envelopes.Add(delegated.Envelope);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
        Revision = domainEvent.Revision;
    }

    void RequireCurrent(long expectedRevision)
    {
        if (PortfolioDefinition is null) throw new InvalidOperationException("Portfolio does not exist.");
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
        if (portfolioId != PortfolioDefinition!.PortfolioId || !_fundIds.Contains(fundId))
            throw new InvalidOperationException("Fund is not a member of this Portfolio.");
    }
}
