using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[MessagePackObject]
public sealed record EconomicCalendarPageReadModel
{
    [Key(0)] public EconomicCalendarReadModel[] Items { get; init; } = [];
    [Key(1)] public string? ContinuationToken { get; init; }
    [IgnoreMember] public bool HasMore => !string.IsNullOrEmpty(ContinuationToken);
}
