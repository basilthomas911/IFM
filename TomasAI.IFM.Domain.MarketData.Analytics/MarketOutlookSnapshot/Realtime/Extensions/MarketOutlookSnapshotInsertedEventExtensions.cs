using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;

/// <summary>Defines the intentional realtime no-op for a durably projected Market Outlook snapshot.</summary>
internal static class MarketOutlookSnapshotInsertedEventExtensions
{
    internal static ValueTask ExecuteAsync(
        this MarketOutlookSnapshotInsertedEvent _,
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> __)
        => ValueTask.CompletedTask;
}
