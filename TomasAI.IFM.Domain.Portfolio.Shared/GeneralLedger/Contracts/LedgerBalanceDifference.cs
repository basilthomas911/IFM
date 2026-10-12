using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record LedgerBalanceDifference([property: Key(0)] int AccountId, [property: Key(1)] int? FundId,
    [property: Key(2)] decimal JournalDebits, [property: Key(3)] decimal JournalCredits,
    [property: Key(4)] decimal RecordedDebits, [property: Key(5)] decimal RecordedCredits);
