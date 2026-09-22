using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record AccountingAccountMapping([property:Key(0)] int AccountId,[property:Key(1)] long AccountVersion,
    [property:Key(2)] string ExternalAccountReference);
[MessagePackObject]
public sealed record AccountingExportMapping([property:Key(0)] string DestinationCompany,[property:Key(1)] long Version,
    [property:Key(2)] AccountingAccountMapping[] Accounts);
[MessagePackObject]
public sealed record AccountingExportRequest([property:Key(0)] Guid ExportId,[property:Key(1)] int PortfolioId,
    [property:Key(2)] int BookId,[property:Key(3)] long SourceRevision,[property:Key(4)] long[] JournalIds,
    [property:Key(5)] AccountingExportMapping Mapping);
[MessagePackObject]
public sealed record AccountingExportLine([property:Key(0)] int Ordinal,[property:Key(1)] string ExternalAccountReference,
    [property:Key(2)] int? FundId,[property:Key(3)] decimal Debit,[property:Key(4)] decimal Credit);
[MessagePackObject]
public sealed record AccountingExportJournal([property:Key(0)] long JournalId,[property:Key(1)] string JournalHash,
    [property:Key(2)] DateOnly AccountingDate,[property:Key(3)] long? ReversesJournalId,[property:Key(4)] AccountingExportLine[] Lines);
[MessagePackObject]
public sealed record AccountingExportPayload([property:Key(0)] Guid ExportId,[property:Key(1)] int PortfolioId,
    [property:Key(2)] int BookId,[property:Key(3)] string DestinationCompany,[property:Key(4)] long SourceRevision,
    [property:Key(5)] long MappingVersion,[property:Key(6)] string MappingHash,[property:Key(7)] AccountingExportJournal[] Journals);
[MessagePackObject]
public sealed record AccountingExportReceipt([property:Key(0)] AccountingExportPayload Payload,[property:Key(1)] string PayloadHash,
    [property:Key(2)] string RequestHash,[property:Key(3)] string DeliveryStatus,[property:Key(4)] int Attempts,
    [property:Key(5)] string? ExternalReceipt);

/// <summary>Immutable source facts supplied by storage, never by an external accounting system.</summary>
public sealed record AccountingJournalSource(FinancialJournal Journal,long FinancialRevision,long? ReversesJournalId);
