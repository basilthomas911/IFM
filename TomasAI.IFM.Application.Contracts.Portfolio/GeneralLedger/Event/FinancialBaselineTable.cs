using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>All business columns, canonical PostgreSQL JSON rows and their SHA-256. Physical transaction IDs are excluded.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialBaselineTable(
    [property:Key(0)] string Name,
    [property:Key(1)] string[] Columns,
    [property:Key(2)] string RowsJson,
    [property:Key(3)] string Sha256);
