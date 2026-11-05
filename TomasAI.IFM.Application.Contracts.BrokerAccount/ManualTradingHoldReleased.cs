using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record ManualTradingHoldReleasedCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record ManualTradingHoldReleasedFailEvent : BrokerAccountOperationFail;
