using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record BrokerAccountSnapshotRecordedCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record BrokerAccountSnapshotRecordedFailEvent : BrokerAccountOperationFail;
