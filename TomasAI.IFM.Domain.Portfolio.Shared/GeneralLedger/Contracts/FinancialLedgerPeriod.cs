using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialLedgerPeriod([property: Key(0)] Guid PeriodId,
    [property: Key(1)] DateOnly StartDate, [property: Key(2)] DateOnly EndDate, [property: Key(3)] long Version, [property: Key(4)] string State);
