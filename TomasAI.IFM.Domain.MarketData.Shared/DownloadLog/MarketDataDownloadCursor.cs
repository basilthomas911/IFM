using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record MarketDataDownloadCursor(
    [property: Key(0)] DateTime RequestedAtUtc,
    [property: Key(1)] Guid ImportCommandId);
