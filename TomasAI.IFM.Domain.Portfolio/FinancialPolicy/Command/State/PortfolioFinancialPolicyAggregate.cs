using TomasAI.IFM.Domain.Portfolio.FinancialPolicy.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

/// <summary>Event-sourced lifecycle for immutable Portfolio financial-policy versions.</summary>
public sealed class PortfolioFinancialPolicyAggregate
{
    readonly Dictionary<long, PortfolioFinancialPolicyReadModel> _financialPolicyVersions = [];
    readonly HashSet<Guid> _commandIds = [];
    bool _everActive;

    public long Revision { get; private set; }
    /// <summary>Gets the authoritative FinancialPolicy applied from source events.</summary>
    public PortfolioFinancialPolicyReadModel? FinancialPolicy { get; private set; }
    /// <summary>Gets the existing read-only compatibility view of the business state.</summary>
    public PortfolioFinancialPolicyReadModel? Current => FinancialPolicy;
    public bool IsDeleted { get; private set; }
    public IReadOnlyCollection<PortfolioFinancialPolicyReadModel> Versions => _financialPolicyVersions.Values.OrderBy(x => x.PolicyVersion).ToArray();

    public IPortfolioFinancialPolicyDomainEvent Create(Guid commandId, Guid idempotencyKey, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeCreate(commandId, idempotencyKey, policy, nowUtc, principal)));

    /// <summary>Computes Create without changing authoritative state.</summary>
    internal IPortfolioFinancialPolicyChange ComputeCreate(Guid commandId, Guid idempotencyKey, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal) {
        ValidateCommand(commandId, nowUtc, principal);
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (FinancialPolicy is not null) throw new InvalidOperationException("Policy already exists.");
        if (policy.PolicyVersion != 1 || policy.OperatingState != PortfolioFinancialPolicyState.Draft)
            throw new ArgumentException("A new policy must begin as Draft version 1.", nameof(policy));
        ThrowIfInvalid(policy.Validate());
        return new PortfolioFinancialPolicyCreatedCompute(Guid.NewGuid(), commandId, 1, nowUtc, principal, policy.DefensiveCopy(), idempotencyKey);
    }

    public IPortfolioFinancialPolicyDomainEvent AddVersion(Guid commandId, long expectedRevision, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeAddVersion(commandId, expectedRevision, policy, nowUtc, principal)));

    /// <summary>Computes AddVersion without changing authoritative state.</summary>
    internal IPortfolioFinancialPolicyChange ComputeAddVersion(Guid commandId, long expectedRevision, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (FinancialPolicy!.OperatingState is PortfolioFinancialPolicyState.Retired or PortfolioFinancialPolicyState.Deleted)
            throw new InvalidOperationException("A terminal policy cannot be versioned.");
        if (policy.PortfolioId != FinancialPolicy.PortfolioId || policy.PolicyId != FinancialPolicy.PolicyId)
            throw new ArgumentException("Policy ownership and identity cannot change.", nameof(policy));
        if (policy.PolicyVersion != _financialPolicyVersions.Keys.Max() + 1 || policy.OperatingState != PortfolioFinancialPolicyState.Draft)
            throw new ArgumentException("A replacement must be the next immutable Draft version.", nameof(policy));
        ThrowIfInvalid(policy.Validate());
        return new PortfolioFinancialPolicyVersionAddedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policy.DefensiveCopy());
    }

    public IPortfolioFinancialPolicyDomainEvent Activate(Guid commandId, long expectedRevision, long policyVersion, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeActivate(commandId, expectedRevision, policyVersion, nowUtc, principal)));

    /// <summary>Computes Activate without changing authoritative state.</summary>
    internal IPortfolioFinancialPolicyChange ComputeActivate(Guid commandId, long expectedRevision, long policyVersion, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (!_financialPolicyVersions.TryGetValue(policyVersion, out var candidate) || candidate.OperatingState != PortfolioFinancialPolicyState.Draft)
            throw new InvalidOperationException("Only an existing Draft policy version can be activated.");
        ThrowIfInvalid(candidate.Validate(forActivation: true));
        if (nowUtc < candidate.EffectiveFromUtc || candidate.EffectiveUntilUtc is { } until && nowUtc >= until)
            throw new InvalidOperationException("Policy is not effective now.");
        return new PortfolioFinancialPolicyActivatedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policyVersion);
    }

    public IPortfolioFinancialPolicyDomainEvent Retire(Guid commandId, long expectedRevision, long policyVersion, string reason, bool isReferenced, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeRetire(commandId, expectedRevision, policyVersion, reason, isReferenced, nowUtc, principal)));

    /// <summary>Computes Retire without changing authoritative state.</summary>
    internal IPortfolioFinancialPolicyChange ComputeRetire(Guid commandId, long expectedRevision, long policyVersion, string reason, bool isReferenced, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (isReferenced) throw new InvalidOperationException("A referenced policy version cannot be retired.");
        if (!_financialPolicyVersions.TryGetValue(policyVersion, out var policy) || policy.OperatingState is PortfolioFinancialPolicyState.Retired or PortfolioFinancialPolicyState.Deleted)
            throw new InvalidOperationException("Policy version cannot be retired.");
        return new PortfolioFinancialPolicyRetiredCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policyVersion, reason.Trim());
    }

    public IPortfolioFinancialPolicyDomainEvent DeleteDraft(Guid commandId, long expectedRevision, string reason, bool isReferenced, DateTime nowUtc, string principal) =>
        ApplyAndReturn(PortfolioComputedEvents.Create(ComputeDeleteDraft(commandId, expectedRevision, reason, isReferenced, nowUtc, principal)));

    /// <summary>Computes DeleteDraft without changing authoritative state.</summary>
    internal IPortfolioFinancialPolicyChange ComputeDeleteDraft(Guid commandId, long expectedRevision, string reason, bool isReferenced, DateTime nowUtc, string principal) {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (_everActive || isReferenced || _financialPolicyVersions.Values.Any(x => x.OperatingState != PortfolioFinancialPolicyState.Draft))
            throw new InvalidOperationException("Only a never-active, unreferenced Draft policy can be deleted.");
        return new DraftPortfolioFinancialPolicyDeletedCompute(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reason.Trim());
    }

    public void Replay(IEnumerable<IPortfolioFinancialPolicyDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(x => x.Revision)) Apply(domainEvent, true);
    }

    /// <summary>Gets the event accepted by the current command for its existing durable append boundary.</summary>
    internal IPortfolioFinancialPolicyDomainEvent? PendingEvent { get; private set; }

    /// <summary>Applies one validated event; command identity and revision must match before mutation.</summary>
    internal bool Update(IPortfolioFinancialPolicyDomainEvent domainEvent, TomasAI.IFM.Shared.EventSourcing.ICommand command)
    {
        if (domainEvent.CommandId != command.CommandId || domainEvent.Revision != Revision + 1) return false;
        Apply(domainEvent, replay: false);
        PendingEvent = domainEvent;
        return true;
    }

    IPortfolioFinancialPolicyDomainEvent ApplyAndReturn(IPortfolioFinancialPolicyDomainEvent domainEvent)
    {
        Apply(domainEvent, false);
        return domainEvent;
    }

    void Apply(IPortfolioFinancialPolicyDomainEvent domainEvent, bool replay)
    {
        if (domainEvent.Revision != Revision + 1 || IsDeleted) throw new InvalidOperationException("Policy history is not contiguous.");
        if (!_commandIds.Add(domainEvent.CommandId)) throw new InvalidOperationException(replay ? "Duplicate command in policy history." : "Command was already applied.");
        switch (domainEvent)
        {
            case PortfolioFinancialPolicyCreatedEvent created:
                FinancialPolicy = created.Policy.DefensiveCopy(); _financialPolicyVersions.Add(FinancialPolicy.PolicyVersion, FinancialPolicy); break;
            case PortfolioFinancialPolicyVersionAddedEvent added:
                FinancialPolicy = added.Policy.DefensiveCopy(); _financialPolicyVersions.Add(FinancialPolicy.PolicyVersion, FinancialPolicy); break;
            case PortfolioFinancialPolicyActivatedEvent activated:
                foreach (var existing in _financialPolicyVersions.Where(x => x.Value.OperatingState == PortfolioFinancialPolicyState.Active).ToArray())
                    _financialPolicyVersions[existing.Key] = existing.Value with { OperatingState = PortfolioFinancialPolicyState.Superseded, SupersededOnUtc = domainEvent.OccurredOnUtc, SupersededBy = domainEvent.Principal };
                FinancialPolicy = _financialPolicyVersions[activated.PolicyVersion] with { OperatingState = PortfolioFinancialPolicyState.Active };
                _financialPolicyVersions[activated.PolicyVersion] = FinancialPolicy; _everActive = true; break;
            case PortfolioFinancialPolicyRetiredEvent retired:
                FinancialPolicy = _financialPolicyVersions[retired.PolicyVersion] with { OperatingState = PortfolioFinancialPolicyState.Retired };
                _financialPolicyVersions[retired.PolicyVersion] = FinancialPolicy; break;
            case DraftPortfolioFinancialPolicyDeletedEvent:
                IsDeleted = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
        Revision = domainEvent.Revision;
    }

    void RequireCurrent(long expectedRevision)
    {
        if (FinancialPolicy is null || IsDeleted) throw new InvalidOperationException("Policy does not exist.");
        if (Revision != expectedRevision) throw new InvalidOperationException($"Expected revision {expectedRevision}, current revision is {Revision}.");
    }

    void ValidateCommand(Guid commandId, DateTime nowUtc, string principal)
    {
        if (commandId == Guid.Empty || _commandIds.Contains(commandId)) throw new InvalidOperationException("A new CommandId is required.");
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Command time must be UTC.", nameof(nowUtc));
        ArgumentException.ThrowIfNullOrWhiteSpace(principal);
    }

    static void ThrowIfInvalid(IReadOnlyList<string> errors)
    {
        if (errors.Count != 0) throw new ArgumentException(string.Join("; ", errors));
    }
}
