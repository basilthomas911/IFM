using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Mutates durable broker-account evidence, qualification, holds, and synchronization state.</summary>
public interface IBrokerAccountCommandApi
{
    /// <summary>Submits a version-bound qualification manifest for human review.</summary>
    ValueTask<ServiceResult<Guid>> SubmitQualificationEvidenceAsync(BrokerAccountId accountId,
        string manifestHash, string evidenceReference, DateTime submittedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Records an explicit human acceptance of the exact submitted manifest.</summary>
    ValueTask<ServiceResult<Guid>> AcceptQualificationAsync(BrokerAccountId accountId,
        Guid approvalId, string manifestHash, string authorizedBy, DateTime reviewedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Revokes the current account qualification and closes new-risk trading.</summary>
    ValueTask<ServiceResult<Guid>> RevokeQualificationAsync(BrokerAccountId accountId,
        string reason, string authorizedBy, DateTime revokedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Places an operator hold on new-risk trading.</summary>
    ValueTask<ServiceResult<Guid>> SetManualHoldAsync(BrokerAccountId accountId,
        string reason, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Releases an operator hold and re-evaluates the durable qualification state.</summary>
    ValueTask<ServiceResult<Guid>> ReleaseManualHoldAsync(BrokerAccountId accountId,
        string reason, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Requests an explicit broker-account resynchronization.</summary>
    ValueTask<ServiceResult<Guid>> RequestResynchronizationAsync(BrokerAccountId accountId,
        DateTime requestedAtUtc, CancellationToken cancellationToken = default);
}

/// <summary>Reads the durable qualification and operational state for one broker account.</summary>
public interface IBrokerAccountQueryApi
{
    /// <summary>Gets the current account state, or a successful result with no value when not initialized.</summary>
    ValueTask<ServiceResult<BrokerAccountDefinition>> GetAsync(
        BrokerAccountId accountId,
        CancellationToken cancellationToken = default);
}

/// <summary>Actor names for the singleton logical broker account.</summary>
public static class BrokerAccountActorNames
{
    public const string Command = "BrokerAccountCommand";
    public const string Event = "BrokerAccountEvent";
    public const string Query = "BrokerAccountQuery";
}

/// <summary>Durable identity for one configured broker account alias.</summary>
[MessagePackObject]
public readonly record struct BrokerAccountId([property: Key(0)] string AccountAlias) : IActorEntityId
{
    [IgnoreMember] public bool IsValid => !string.IsNullOrWhiteSpace(AccountAlias);

    /// <summary>Returns the exact configured account alias.</summary>
    public string Format() => AccountAlias;
}

public enum BrokerAccountQualificationStatus : byte
{
    Unknown = 0,
    EvidencePending = 1,
    ReviewPending = 2,
    Accepted = 3,
    Revoked = 4
}

public enum BrokerAccountOperationalGate : byte
{
    Closed = 0,
    ReadOnly = 1,
    Open = 2
}

[MessagePackObject]
public sealed record BrokerAccountPositionEvidence
{
    [Key(0)] public string ContractId { get; init; } = string.Empty;
    [Key(1)] public int SignedQuantity { get; init; }
    [Key(2)] public decimal AveragePrice { get; init; }
}

[MessagePackObject]
public sealed record BrokerAccountSnapshotEvidence
{
    [Key(0)] public string AccountAlias { get; init; } = string.Empty;
    [Key(1)] public string Currency { get; init; } = string.Empty;
    [Key(2)] public decimal CashBalance { get; init; }
    [Key(3)] public decimal AvailableFunds { get; init; }
    [Key(4)] public bool Complete { get; init; }
    [Key(5)] public bool NewRiskAllowed { get; init; }
    [Key(6)] public long Generation { get; init; }
    [Key(7)] public DateTime AsOfUtc { get; init; }
    [Key(8)] public BrokerAccountPositionEvidence[] Positions { get; init; } = [];

    /// <summary>Creates serializable evidence from the application broker contract.</summary>
    public static BrokerAccountSnapshotEvidence From(BrokerAccountSnapshot value) => new()
    {
        AccountAlias = value.AccountAlias,
        Currency = value.Currency,
        CashBalance = value.CashBalance,
        AvailableFunds = value.AvailableFunds,
        Complete = value.Complete,
        NewRiskAllowed = value.NewRiskAllowed,
        Generation = value.Generation,
        AsOfUtc = value.AsOfUtc,
        Positions = [.. value.Positions
            .OrderBy(static position => position.ContractId, StringComparer.Ordinal)
            .ThenBy(static position => position.SignedQuantity)
            .ThenBy(static position => position.AveragePrice)
            .Select(position => new BrokerAccountPositionEvidence
            {
                ContractId = position.ContractId,
                SignedQuantity = position.SignedQuantity,
                AveragePrice = position.AveragePrice
            })]
    };
}

[MessagePackObject]
public sealed record BrokerAccountDefinition
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public BrokerAccountId Id { get; init; }
    [Key(2)] public BrokerEnvironment Environment { get; init; }
    [Key(3)] public BrokerAccountSnapshotEvidence? Snapshot { get; init; }
    [Key(4)] public BrokerAccountQualificationStatus QualificationStatus { get; init; }
    [Key(5)] public BrokerAccountOperationalGate Gate { get; init; }
    [Key(6)] public bool ManualHold { get; init; }
    [Key(7)] public string ManifestHash { get; init; } = string.Empty;
    [Key(8)] public string EvidenceReference { get; init; } = string.Empty;
    [Key(9)] public Guid ApprovalId { get; init; }
    [Key(10)] public string AuthorizedBy { get; init; } = string.Empty;
    [Key(11)] public DateTime ChangedAtUtc { get; init; }
    [Key(12)] public int Revision { get; init; }
    [Key(13)] public string Reason { get; init; } = string.Empty;
}

/// <summary>Transports one immutable framework account snapshot through the BrokerAccount event mailbox.</summary>
[MessagePackObject]
public sealed record BrokerAccountSnapshotObservedEvent : IEvent<BrokerAccountId>
{
    public const string Actor = BrokerAccountActorNames.Event;
    public const string Verb = "BrokerAccountSnapshotObserved";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public BrokerAccountId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public BrokerEnvironment Environment { get; init; }
    [Key(9)] public BrokerAccountSnapshotEvidence Snapshot { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(BrokerAccountSnapshotObservedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

[MessagePackObject]
[Union(0, typeof(RecordBrokerAccountSnapshotCommand))]
[Union(1, typeof(SubmitAccountQualificationEvidenceCommand))]
[Union(2, typeof(AcceptAccountQualificationCommand))]
[Union(3, typeof(RevokeAccountQualificationCommand))]
[Union(4, typeof(SetManualTradingHoldCommand))]
[Union(5, typeof(ReleaseManualTradingHoldCommand))]
[Union(6, typeof(RequestBrokerAccountResynchronizationCommand))]
public abstract record BrokerAccountCommand : ICommand<BrokerAccountId>
{
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public BrokerAccountId EntityId { get; init; }
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public BoundedContextName RouteTo => BoundedContextName.BrokerAccountBoundedContext;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{BrokerAccountActorNames.Command}Actor";
    [IgnoreMember] public int ErrorCode => 25104;
}

[MessagePackObject]
public sealed record RecordBrokerAccountSnapshotCommand : BrokerAccountCommand
{
    public const string Verb = "RecordBrokerAccountSnapshot";
    [Key(4)] public BrokerEnvironment Environment { get; init; }
    [Key(5)] public BrokerAccountSnapshotEvidence Snapshot { get; init; } = new();
}

[MessagePackObject]
public sealed record SubmitAccountQualificationEvidenceCommand : BrokerAccountCommand
{
    public const string Verb = "SubmitAccountQualificationEvidence";
    [Key(4)] public string ManifestHash { get; init; } = string.Empty;
    [Key(5)] public string EvidenceReference { get; init; } = string.Empty;
    [Key(6)] public DateTime SubmittedAtUtc { get; init; }
}

[MessagePackObject]
public sealed record AcceptAccountQualificationCommand : BrokerAccountCommand
{
    public const string Verb = "AcceptAccountQualification";
    [Key(4)] public Guid ApprovalId { get; init; }
    [Key(5)] public string ManifestHash { get; init; } = string.Empty;
    [Key(6)] public string AuthorizedBy { get; init; } = string.Empty;
    [Key(7)] public DateTime ReviewedAtUtc { get; init; }
}

[MessagePackObject]
public sealed record RevokeAccountQualificationCommand : BrokerAccountCommand
{
    public const string Verb = "RevokeAccountQualification";
    [Key(4)] public string Reason { get; init; } = string.Empty;
    [Key(5)] public string AuthorizedBy { get; init; } = string.Empty;
    [Key(6)] public DateTime RevokedAtUtc { get; init; }
}

[MessagePackObject]
public sealed record SetManualTradingHoldCommand : BrokerAccountCommand
{
    public const string Verb = "SetManualTradingHold";
    [Key(4)] public string Reason { get; init; } = string.Empty;
    [Key(5)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record ReleaseManualTradingHoldCommand : BrokerAccountCommand
{
    public const string Verb = "ReleaseManualTradingHold";
    [Key(4)] public string Reason { get; init; } = string.Empty;
    [Key(5)] public DateTime EffectiveAtUtc { get; init; }
}

[MessagePackObject]
public sealed record RequestBrokerAccountResynchronizationCommand : BrokerAccountCommand
{
    public const string Verb = "RequestBrokerAccountResynchronization";
    [Key(4)] public DateTime RequestedAtUtc { get; init; }
}

[MessagePackObject]
public sealed record BrokerAccountChangedEvent : IEvent<BrokerAccountId>
{
    public const string Actor = "BrokerAccountEvent";
    public const string Verb = "BrokerAccountChanged";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public BrokerAccountId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public BrokerAccountDefinition State { get; init; } = new();
    [IgnoreMember] public string UserName => string.Empty;
    [IgnoreMember] public string EventName => nameof(BrokerAccountChangedEvent);
    [IgnoreMember] public EventType EventType => EventType.DomainEvent;
}

[MessagePackObject]
public sealed record GetBrokerAccountQuery : IQuery<BrokerAccountDefinition>
{
    public const string Actor = BrokerAccountActorNames.Query;
    public const string Verb = "GetBrokerAccount";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public BrokerAccountId BrokerAccountId { get; init; }
    [IgnoreMember] public int ErrorCode => 25212;
    [IgnoreMember] public string? QueryParams => null;
}
