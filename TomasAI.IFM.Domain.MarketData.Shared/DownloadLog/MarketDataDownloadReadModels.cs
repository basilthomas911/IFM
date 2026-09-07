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
    public void Validate()
    {
        if (Dataset is not (MarketDataDownloadDataset.EconomicCalendar or MarketDataDownloadDataset.TreasuryCurve)
            || Provider != "FMP" || ValueDate == default || string.IsNullOrWhiteSpace(Scope)
            || Scope != MarketDataDownloadOutcome.CanonicalScope(Scope == "ALL" ? [] : Scope.Split(','))
            || Dataset == MarketDataDownloadDataset.TreasuryCurve && Scope != "US")
            throw new ArgumentException("Invalid DownloadLog query partition.");
    }
}

[MessagePackObject]
public sealed record MarketDataDownloadCursor(
    [property: Key(0)] DateTime RequestedAtUtc,
    [property: Key(1)] Guid ImportCommandId);

[MessagePackObject]
public sealed record MarketDataDownloadLogReadModel(
    [property: Key(0)] MarketDataDownloadOutcome Outcome,
    [property: Key(1)] Guid LogCommandId,
    [property: Key(2)] string PayloadSha256,
    [property: Key(3)] DateTime ProjectedAtUtc);

[MessagePackObject]
public sealed record MarketDataDownloadLogResult([property: Key(0)] MarketDataDownloadLogReadModel? Attempt)
{
    [IgnoreMember] public bool Found => Attempt is not null;
}

[MessagePackObject]
public sealed record MarketDataDownloadHistoryResult(
    [property: Key(0)] MarketDataDownloadLogReadModel[] Attempts,
    [property: Key(1)] MarketDataDownloadCursor? Continuation);

[MessagePackObject]
public sealed record MarketDataDownloadStatusResult(
    [property: Key(0)] bool CompletionConfirmed,
    [property: Key(1)] MarketDataDownloadLogReadModel? LatestAttempt,
    [property: Key(2)] MarketDataDownloadLogReadModel? SuccessfulAttempt,
    [property: Key(3)] bool SearchExhaustive,
    [property: Key(4)] MarketDataDownloadCursor? Continuation,
    [property: Key(5)] MarketDataDownloadLogReadModel? RequiredAttempt = null);
