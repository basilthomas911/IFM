using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record EmulatorOrderReceipt([property: Key(0)] Guid ExecutionId, [property: Key(1)] FinancialExecutionOrder Order,
    [property: Key(2)] string BrokerOrderReference, [property: Key(3)] long FinancialRevision, [property: Key(4)] DateTime SubmittedAtUtc,
    [property: Key(5)] Guid CompletedEventId);
