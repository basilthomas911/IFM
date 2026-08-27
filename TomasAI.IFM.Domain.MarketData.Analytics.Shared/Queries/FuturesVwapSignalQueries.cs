using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>Requests the latest projected futures-session VWAP.</summary>
[MessagePackObject]
public sealed record GetLatestFuturesVwapSignalQuery : IQuery<FuturesVwapSignalReadModel?>
{
    public const string Actor = "FuturesVwapSignalQuery";
    public const string Verb = "GetLatest";
    public const int ErrorId = 26420;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = default!;
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public string ConfigurationId { get; init; } = string.Empty;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}

/// <summary>Requests projected updates for one futures-contract session.</summary>
[MessagePackObject]
public sealed record GetFuturesVwapSignalHistoryQuery : IQuery<FuturesVwapSignalReadModel[]>
{
    public const string Verb = "GetHistory";
    public const int ErrorId = 26421;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = default!;
    [Key(2)] public string ContractId { get; init; } = string.Empty;
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public string ConfigurationId { get; init; } = string.Empty;
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
}
