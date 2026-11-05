using MessagePack;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

[MessagePackObject]
public sealed record ManualTradingHoldSetCompleteEvent : BrokerAccountOperationComplete;

[MessagePackObject]
public sealed record ManualTradingHoldSetFailEvent : BrokerAccountOperationFail;
