using MessagePack;
namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;
/// <summary>The original completed five-minute ATR(14) frozen at a Daily DC.</summary>
[MessagePackObject]
public sealed record DailyItiAtrThreshold(
    [property: Key(0)] string ContractId,
    [property: Key(1)] double Value,
    [property: Key(2)] DateTime ObservedAtUtc,
    [property: Key(3)] DateTime CalculatedAtUtc,
    [property: Key(4)] long SourceSequence)
{
    public bool IsUsableAt(string contract, DateTime at) => ContractId == contract && double.IsFinite(Value) && Value > 0
        && ObservedAtUtc.Kind == DateTimeKind.Utc && CalculatedAtUtc.Kind == DateTimeKind.Utc
        && ObservedAtUtc <= at && CalculatedAtUtc <= at && at - ObservedAtUtc <= TimeSpan.FromSeconds(630);
}
