using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record AccountingExportPayload([property: Key(0)] Guid ExportId, [property: Key(1)] int PortfolioId,
    [property: Key(2)] int BookId, [property: Key(3)] string DestinationCompany, [property: Key(4)] long SourceRevision,
    [property: Key(5)] long MappingVersion, [property: Key(6)] string MappingHash, [property: Key(7)] AccountingExportJournal[] Journals);
