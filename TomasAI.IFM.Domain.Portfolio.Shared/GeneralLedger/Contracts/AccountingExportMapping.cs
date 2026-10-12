using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record AccountingExportMapping([property: Key(0)] string DestinationCompany, [property: Key(1)] long Version,
    [property: Key(2)] AccountingAccountMapping[] Accounts);
