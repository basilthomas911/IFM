using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Contains a bounded page of persisted open-position projections and its continuation.</summary>
[MessagePackObject]
public sealed record ScheduledMarketPositionPage
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(0)] public StrategyPositionSnapshot[] Positions { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(1)] public byte[]? PagingState { get; init; }
}
