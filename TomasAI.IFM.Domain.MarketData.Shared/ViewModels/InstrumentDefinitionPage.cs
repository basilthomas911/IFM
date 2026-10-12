using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
[MessagePackObject]
public sealed record InstrumentDefinitionPage(
    [property: Key(0)] Guid SnapshotId,
    [property: Key(1)] DateTime SnapshotCompletedUtc,
    [property: Key(2)] InstrumentDefinitionSelection[] Items,
    [property: Key(3)] string? ContinuationToken);
