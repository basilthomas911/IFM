using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

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
    /// <summary>Server-derived Development emulator exemption; persisted evidence remains unchanged.</summary>
    [Key(14)] public bool DevelopmentQualificationsExempt { get; init; }
}
