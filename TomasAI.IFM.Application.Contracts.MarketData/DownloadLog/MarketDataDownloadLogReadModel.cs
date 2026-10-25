using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record MarketDataDownloadLogReadModel(
    [property: Key(0)] MarketDataDownloadOutcome Outcome,
    [property: Key(1)] Guid LogCommandId,
    [property: Key(2)] string PayloadSha256,
    [property: Key(3)] DateTime ProjectedAtUtc);
