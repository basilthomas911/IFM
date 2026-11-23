using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

[MessagePackObject]
public sealed record IntrinsicTimeFundAssignment(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] string FundName,
    [property: Key(3)] string Family,
    [property: Key(4)] string InstrumentRoot,
    [property: Key(5)] TimeFrameType Timeframe,
    [property: Key(6)] CatalogKey Deployment,
    [property: Key(7)] long MandateVersion,
    [property: Key(8)] DateTime EffectiveFromUtc,
    [property: Key(9)] DateTime? EffectiveUntilUtc)
{
    public string QueueSuffix() => FormattableString.Invariant($"~{PortfolioId}~{FundId}~{Family}");

    public bool MatchesContract(string contract)
    {
        if (!contract.StartsWith(InstrumentRoot, StringComparison.Ordinal))
            return false;
        var suffix = contract.AsSpan(InstrumentRoot.Length);
        return (suffix.Length == 8 && suffix.IndexOfAnyExceptInRange('0', '9') < 0)
            || (suffix.Length is >= 2 and <= 5 && "FGHJKMNQUVXZ".Contains(suffix[0], StringComparison.Ordinal)
                && suffix[1..].IndexOfAnyExceptInRange('0', '9') < 0);
    }
}
