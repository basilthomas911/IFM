using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record SubmitEmulatorOrderRequest([property: Key(0)] FinancialExecutionOrder Order,
    [property: Key(1)] Guid ConsumptionOperationId, [property: Key(2)] Guid ConsumptionCompletedEventId,
    [property: Key(3)] string ConsumptionInputHash);
