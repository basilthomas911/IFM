using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

/// <summary>Calculates prior-day Daily Bollinger signals from normalized Databento EOD history.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetFuturesBollingerBandHistoryQuery : IQuery<FuturesBbSignalReadModel[]>
{
    [IgnoreMember] public const string Actor = "MarketOutlookSnapshotQuery";
    [IgnoreMember] public const string Verb = "GetFuturesBollingerBandHistory";
    [IgnoreMember] public const int ErrorId = 1041;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = new GetFuturesBollingerBandHistoryParameter();
    [IgnoreMember] public int ErrorCode { get; init; } = ErrorId;
    [IgnoreMember] public string? QueryParams { get; init; }
    [Key(2)] public string RootSymbol { get; init; } = string.Empty;
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public int MaxDays { get; init; }

    public GetFuturesBollingerBandHistoryQuery() { }

    public GetFuturesBollingerBandHistoryQuery(string rootSymbol, DateOnly valueDate, int maxDays)
    {
        RootSymbol = rootSymbol ?? string.Empty;
        ValueDate = valueDate;
        MaxDays = maxDays;
        EntityId = new GetFuturesBollingerBandHistoryParameter(RootSymbol, valueDate, maxDays);
    }

    [SerializationConstructor]
    public GetFuturesBollingerBandHistoryQuery(
        ActorSubject subject,
        IActorEntityId entityId,
        string rootSymbol,
        DateOnly valueDate,
        int maxDays)
        : this(rootSymbol, valueDate, maxDays) => Subject = subject;
}
