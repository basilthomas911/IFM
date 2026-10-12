using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Immutable source facts supplied by storage, never by an external accounting system.</summary>
public sealed record AccountingJournalSource(FinancialJournal Journal, long FinancialRevision, long? ReversesJournalId);
