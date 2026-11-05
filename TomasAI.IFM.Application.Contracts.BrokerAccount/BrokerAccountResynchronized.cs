using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record BrokerAccountResynchronizedCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record BrokerAccountResynchronizedFailEvent : BrokerAccountOperationFail;
