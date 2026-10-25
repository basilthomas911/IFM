using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

public sealed record PreparedLedgerPosting(long TransactionId, long? JournalId, LedgerPostingRequest Request);
