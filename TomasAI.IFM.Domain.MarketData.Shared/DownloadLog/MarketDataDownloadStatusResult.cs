using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

[MessagePackObject]
public sealed record MarketDataDownloadStatusResult(
    [property: Key(0)] bool CompletionConfirmed,
    [property: Key(1)] MarketDataDownloadLogReadModel? LatestAttempt,
    [property: Key(2)] MarketDataDownloadLogReadModel? SuccessfulAttempt,
    [property: Key(3)] bool SearchExhaustive,
    [property: Key(4)] MarketDataDownloadCursor? Continuation,
    [property: Key(5)] MarketDataDownloadLogReadModel? RequiredAttempt = null);
