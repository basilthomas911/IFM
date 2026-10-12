using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record MarketDataDownloadPartition(
    [property: Key(0)] MarketDataDownloadDataset Dataset,
    [property: Key(1)] string Provider,
    [property: Key(2)] string Scope,
    [property: Key(3)] DateOnly ValueDate) : IActorEntityId
{
    public string Format() => $"{Dataset}.{Provider}.{Scope.Replace(',', '-')}.{ValueDate:yyyyMMdd}";
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
    public void Validate()
    {
        if (Dataset is not (MarketDataDownloadDataset.EconomicCalendar or MarketDataDownloadDataset.TreasuryCurve)
            || !MarketDataDownloadOutcome.IsSupportedProvider(Dataset, Provider) || ValueDate == default || string.IsNullOrWhiteSpace(Scope)
            || (!string.Equals((Scope),(MarketDataDownloadOutcome.CanonicalScope((string.Equals((Scope),("ALL"),StringComparison.Ordinal)) ? [] : Scope.Split(','))),StringComparison.Ordinal))
            || Dataset == MarketDataDownloadDataset.TreasuryCurve && (!string.Equals((Scope),("US"),StringComparison.Ordinal)))
            throw new ArgumentException("Invalid DownloadLog query partition.");
    }
}
