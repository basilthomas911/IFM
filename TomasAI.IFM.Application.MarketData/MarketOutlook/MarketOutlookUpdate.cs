using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

/// <summary>
/// Base contract for an in-process Market Outlook update. This contract deliberately implements no
/// actor-message interface and is never serialized through NATS.
/// </summary>
public abstract record MarketOutlookUpdate
{
    /// <summary>Originating disposable actor lifetime, carried only inside this process.</summary>
    public TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration? ActorGeneration { get; init; }
    internal long QueueSequence { get; init; }
    public required Guid UpdateId { get; init; }
    public abstract MarketOutlookUpdateKind Kind { get; }
    public required MarketOutlookEntityId EntityId { get; init; }
    public required DateTime ReceivedAtUtc { get; init; }
    public required DateTime MarketDataAsOfUtc { get; init; }
    public Guid CommandId { get; init; }
    public string AggregateId { get; init; } = string.Empty;
    public string EventSource { get; init; } = string.Empty;
    public long SourceSequence { get; init; }
    public Guid StreamEpochId { get; init; }
    public long StreamOrdinal { get; init; }
}
