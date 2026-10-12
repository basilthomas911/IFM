using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record RecordBrokerAccountSnapshotCommand : BrokerAccountCommand
{
    public const string Verb = "RecordBrokerAccountSnapshot";
    [Key(4)] public BrokerEnvironment Environment { get; init; }
    [Key(5)] public BrokerAccountSnapshotEvidence Snapshot { get; init; } = new();
}
