using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record AccountingExportLine([property: Key(0)] int Ordinal, [property: Key(1)] string ExternalAccountReference,
    [property: Key(2)] int? FundId, [property: Key(3)] decimal Debit, [property: Key(4)] decimal Credit);
