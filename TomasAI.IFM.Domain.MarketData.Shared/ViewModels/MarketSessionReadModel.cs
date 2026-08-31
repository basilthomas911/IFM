using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Describes the current futures-market session without conflating read-only
/// application operation with access to live market-data APIs.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record MarketSessionReadModel
{
    [Key(0)] public DateOnly OperationalValueDate { get; init; }
    [Key(1)] public DateOnly? ActiveValueDate { get; init; }
    [Key(2)] public bool IsLiveSessionOpen { get; init; }
    [Key(3)] public DateTime MarketTime { get; init; }
    [Key(4)] public DateTime SessionStartUtc { get; init; }
    [Key(5)] public DateTime SessionEndUtc { get; init; }

    [IgnoreMember]
    public bool IsValid => OperationalValueDate != default
        && SessionStartUtc != default
        && SessionEndUtc > SessionStartUtc;
}
