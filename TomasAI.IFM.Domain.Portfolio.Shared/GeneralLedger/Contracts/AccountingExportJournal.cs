using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record AccountingExportJournal([property: Key(0)] long JournalId, [property: Key(1)] string JournalHash,
    [property: Key(2)] DateOnly AccountingDate, [property: Key(3)] long? ReversesJournalId, [property: Key(4)] AccountingExportLine[] Lines);
