using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record AccountQualificationAcceptedCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record AccountQualificationAcceptedFailEvent : BrokerAccountOperationFail;
