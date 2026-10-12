using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve array-valued serialized message and query contracts, including their MessagePack slots.")]
[MessagePackObject]
public sealed record MarketDataDownloadHistoryResult(
    [property: Key(0)] MarketDataDownloadLogReadModel[] Attempts,
    [property: Key(1)] MarketDataDownloadCursor? Continuation);
