using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
/// <summary>Contains a bounded page of persisted open-position projections and its continuation.</summary>
[MessagePackObject]
public sealed record ScheduledMarketPositionPage
{
    [Key(0)] public StrategyPositionSnapshot[] Positions { get; init; } = [];
    [Key(1)] public byte[]? PagingState { get; init; }
}
