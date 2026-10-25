using MessagePack;
namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Frozen economic conventions accompanying an enriched option observation. No provider lookup is needed to reprice a retained leg.</summary>
[MessagePackObject]
public sealed record OptionTickPricingInputs
{
    [Key(0)] public string Currency { get; init; } = string.Empty;
    [Key(1)] public decimal Multiplier { get; init; }
    [Key(2)] public DateTimeOffset ExpirationUtc { get; init; }
    [Key(3)] public int DaysPerYear { get; init; }
    [Key(4)] public double AnnualContinuousRate { get; init; }
    [Key(5)] public string Exercise { get; init; } = string.Empty;
    [Key(6)] public string Premium { get; init; } = string.Empty;
    [Key(7)] public decimal BidTickIncrement { get; init; }
    [Key(8)] public decimal AskTickIncrement { get; init; }
    [Key(9)] public string ModelVersion { get; init; } = string.Empty;
}
