using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Command.State;

/// <summary>Event-sourced lifecycle for immutable Portfolio financial-policy versions.</summary>
public sealed class PortfolioFinancialPolicyAggregate
{
    readonly Dictionary<long, PortfolioFinancialPolicyReadModel> _versions = [];
    readonly HashSet<Guid> _commandIds = [];
    bool _everActive;

    public long Revision { get; private set; }
    public PortfolioFinancialPolicyReadModel? Current { get; private set; }
    public bool IsDeleted { get; private set; }
    public IReadOnlyCollection<PortfolioFinancialPolicyReadModel> Versions => _versions.Values.OrderBy(x => x.PolicyVersion).ToArray();

    public PortfolioFinancialPolicyDomainEvent Create(Guid commandId, Guid idempotencyKey, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal)
    {
        ValidateCommand(commandId, nowUtc, principal);
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (Current is not null) throw new InvalidOperationException("Policy already exists.");
        if (policy.PolicyVersion != 1 || policy.OperatingState != PortfolioFinancialPolicyState.Draft)
            throw new ArgumentException("A new policy must begin as Draft version 1.", nameof(policy));
        ThrowIfInvalid(policy.Validate());
        return ApplyAndReturn(new PortfolioFinancialPolicyCreated(Guid.NewGuid(), commandId, 1, nowUtc, principal, policy.DefensiveCopy(), idempotencyKey));
    }

    public PortfolioFinancialPolicyDomainEvent AddVersion(Guid commandId, long expectedRevision, PortfolioFinancialPolicyReadModel policy, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (Current!.OperatingState is PortfolioFinancialPolicyState.Retired or PortfolioFinancialPolicyState.Deleted)
            throw new InvalidOperationException("A terminal policy cannot be versioned.");
        if (policy.PortfolioId != Current.PortfolioId || policy.PolicyId != Current.PolicyId)
            throw new ArgumentException("Policy ownership and identity cannot change.", nameof(policy));
        if (policy.PolicyVersion != _versions.Keys.Max() + 1 || policy.OperatingState != PortfolioFinancialPolicyState.Draft)
            throw new ArgumentException("A replacement must be the next immutable Draft version.", nameof(policy));
        ThrowIfInvalid(policy.Validate());
        return ApplyAndReturn(new PortfolioFinancialPolicyVersionAdded(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policy.DefensiveCopy()));
    }

    public PortfolioFinancialPolicyDomainEvent Activate(Guid commandId, long expectedRevision, long policyVersion, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        if (!_versions.TryGetValue(policyVersion, out var candidate) || candidate.OperatingState != PortfolioFinancialPolicyState.Draft)
            throw new InvalidOperationException("Only an existing Draft policy version can be activated.");
        ThrowIfInvalid(candidate.Validate(forActivation: true));
        if (nowUtc < candidate.EffectiveFromUtc || candidate.EffectiveUntilUtc is { } until && nowUtc >= until)
            throw new InvalidOperationException("Policy is not effective now.");
        return ApplyAndReturn(new PortfolioFinancialPolicyActivated(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policyVersion));
    }

    public PortfolioFinancialPolicyDomainEvent Retire(Guid commandId, long expectedRevision, long policyVersion, string reason, bool isReferenced, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (isReferenced) throw new InvalidOperationException("A referenced policy version cannot be retired.");
        if (!_versions.TryGetValue(policyVersion, out var policy) || policy.OperatingState is PortfolioFinancialPolicyState.Retired or PortfolioFinancialPolicyState.Deleted)
            throw new InvalidOperationException("Policy version cannot be retired.");
        return ApplyAndReturn(new PortfolioFinancialPolicyRetired(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, policyVersion, reason.Trim()));
    }

    public PortfolioFinancialPolicyDomainEvent DeleteDraft(Guid commandId, long expectedRevision, string reason, bool isReferenced, DateTime nowUtc, string principal)
    {
        RequireCurrent(expectedRevision);
        ValidateCommand(commandId, nowUtc, principal);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (_everActive || isReferenced || _versions.Values.Any(x => x.OperatingState != PortfolioFinancialPolicyState.Draft))
            throw new InvalidOperationException("Only a never-active, unreferenced Draft policy can be deleted.");
        return ApplyAndReturn(new DraftPortfolioFinancialPolicyDeleted(Guid.NewGuid(), commandId, Revision + 1, nowUtc, principal, reason.Trim()));
    }

    public void Replay(IEnumerable<PortfolioFinancialPolicyDomainEvent> events)
    {
        foreach (var domainEvent in events.OrderBy(x => x.Revision)) Apply(domainEvent, true);
    }

    PortfolioFinancialPolicyDomainEvent ApplyAndReturn(PortfolioFinancialPolicyDomainEvent domainEvent)
    {
        Apply(domainEvent, false);
        return domainEvent;
    }

    void Apply(PortfolioFinancialPolicyDomainEvent domainEvent, bool replay)
    {
        if (domainEvent.Revision != Revision + 1 || IsDeleted) throw new InvalidOperationException("Policy history is not contiguous.");
        if (!_commandIds.Add(domainEvent.CommandId)) throw new InvalidOperationException(replay ? "Duplicate command in policy history." : "Command was already applied.");
        switch (domainEvent)
        {
            case PortfolioFinancialPolicyCreated created:
                Current = created.Policy.DefensiveCopy(); _versions.Add(Current.PolicyVersion, Current); break;
            case PortfolioFinancialPolicyVersionAdded added:
                Current = added.Policy.DefensiveCopy(); _versions.Add(Current.PolicyVersion, Current); break;
            case PortfolioFinancialPolicyActivated activated:
                foreach (var existing in _versions.Where(x => x.Value.OperatingState == PortfolioFinancialPolicyState.Active).ToArray())
                    _versions[existing.Key] = existing.Value with { OperatingState = PortfolioFinancialPolicyState.Superseded, SupersededOnUtc = domainEvent.OccurredOnUtc, SupersededBy = domainEvent.Principal };
                Current = _versions[activated.PolicyVersion] with { OperatingState = PortfolioFinancialPolicyState.Active };
                _versions[activated.PolicyVersion] = Current; _everActive = true; break;
            case PortfolioFinancialPolicyRetired retired:
                Current = _versions[retired.PolicyVersion] with { OperatingState = PortfolioFinancialPolicyState.Retired };
                _versions[retired.PolicyVersion] = Current; break;
            case DraftPortfolioFinancialPolicyDeleted:
                IsDeleted = true; break;
            default: throw new ArgumentOutOfRangeException(nameof(domainEvent));
        }
        Revision = domainEvent.Revision;
    }

    void RequireCurrent(long expectedRevision)
    {
        if (Current is null || IsDeleted) throw new InvalidOperationException("Policy does not exist.");
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
