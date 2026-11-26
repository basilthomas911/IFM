using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Original external provider request/reply or explicit transport failure; never a calculated owner result.</summary>
[MessagePackObject]
public sealed record RecordedMarketDataExchange(
    [property: Key(0)] MarketDataHostRequest Request,
    [property: Key(1)] MarketDataHostReply Reply,
    [property: Key(2)] DateTimeOffset StartedAtUtc,
    [property: Key(3)] DateTimeOffset CompletedAtUtc,
    [property: Key(4)] string? ReplyOrigin,
    [property: Key(5)] string? RequestOrigin = null,
    [property: Key(6)] string? TransportFailure = null);
