using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record AccountingExportRequest([property: Key(0)] Guid ExportId, [property: Key(1)] int PortfolioId,
    [property: Key(2)] int BookId, [property: Key(3)] long SourceRevision, [property: Key(4)] long[] JournalIds,
    [property: Key(5)] AccountingExportMapping Mapping);
